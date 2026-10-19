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

Domain events → outbox table (same transaction) → background processor → SignalR, Telegram, SMS,
email. Reminders are Quartz.NET jobs.
