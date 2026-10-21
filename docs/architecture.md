# Architecture

## Dependency rule

`Api → Infrastructure → Application → Domain`. Inner layers never reference outer ones;
`Mastera.ArchitectureTests` fails the build if they do. Domain and Application never reference
EF Core, Npgsql or ASP.NET Core.

## Multi-tenancy

- One PostgreSQL database; every tenant-owned table has `tenant_id`.
- Tenant resolved from the subdomain (`{salon}.mastera.ge`) for the public site, or from the user's
  active membership in the dashboard.
- Tenant-owned entities implement `ITenantOwned`. `MasteraDbContext` adds a query filter
  (`TenantId == CurrentTenantId`) to each of them automatically; with no current tenant every query is empty.
- `TenantSaveChangesInterceptor` stamps new rows with the current tenant and throws
  `TenantIsolationException` on any insert, update or delete of another tenant's row.
- `TenantConnectionInterceptor` sets the PostgreSQL setting `app.tenant_id` on every connection open.
  Every tenant-owned table gets a row-level security policy on it: call
  `migrationBuilder.EnableTenantRowLevelSecurity("TableName")` in the migration that creates the table.
- **Superusers bypass row-level security.** In staging and production the API must connect as a normal role
  (`NOSUPERUSER NOBYPASSRLS`), not as the database owner. The local docker-compose user is a superuser,
  so locally only the EF Core filter protects you; the integration tests cover the database policy.

## Time

All instants are stored as UTC (`timestamptz`, NodaTime `Instant`). Schedules are defined in the
salon's local time (`LocalDate`, `LocalTime`) and converted with the salon's time zone.

## Double-booking protection

A PostgreSQL exclusion constraint (`btree_gist`) on `(master_id, period)` for active bookings
(Epic 5.2). The API maps the violation to `409 Conflict`.

## Notifications

- Entities raise domain events (`BookingCreatedEvent`, `…Cancelled`, `…Rescheduled`, `…Confirmed`).
  `OutboxInterceptor` turns them into `OutboxMessages` rows in the **same transaction** as the change.
- `NotificationWorker` (a hosted service) runs `OutboxDispatcher` every few seconds. Rows are taken with
  `FOR UPDATE SKIP LOCKED`, so several app instances never handle the same message. Each message is handled in
  its own scope inside its salon; failures are retried up to 10 times.
- `BookingNotificationHandler` sends: a live SignalR message (`/hubs/notifications?tenant={slug}`; groups for
  the front desk and per master), Telegram to the master, SMS/email to the client. Every send is first
  recorded in `NotificationLog`, so a message delivered twice never texts anyone twice.
- Reminders (24 h and 2 h before) are rows in `ScheduledReminders`, replaced when a booking moves and deleted
  when it is cancelled; the same worker sends the due ones. (The plan said Quartz.NET; one table-driven loop
  is simpler and needs no extra library.)
- Clients' "change or cancel" links carry the booking id signed with HMAC-SHA256 (`App:LinkSecret`), so no
  secret is stored and the same link works in every SMS.
- Configuration: `Sms:Url`/`Sms:ApiKey` (a generic JSON gateway) and `Telegram:BotToken`/`BotName`/`WebhookSecret`.
  Without them, SMS and Telegram messages are only written to the log. The Telegram webhook is
  `POST /api/telegram/webhook` (set it with `setWebhook` and the same `secret_token`).
