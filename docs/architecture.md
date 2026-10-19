# Architecture

## Dependency rule

`Api → Infrastructure → Application → Domain`. Inner layers never reference outer ones;
`Mastera.ArchitectureTests` fails the build if they do. Domain and Application never reference
EF Core, Npgsql or ASP.NET Core.

## Multi-tenancy

- One PostgreSQL database; every tenant-owned table has `tenant_id`.
- Tenant resolved from the subdomain (`{salon}.mastera.ge`) for the public site, or from the user's
  active membership in the dashboard.
- EF Core global query filter + SaveChanges interceptor (Epic 1.3), plus PostgreSQL Row-Level
  Security as a second safety layer (Epic 1.4).

## Time

All instants are stored as UTC (`timestamptz`, NodaTime `Instant`). Schedules are defined in the
salon's local time (`LocalDate`, `LocalTime`) and converted with the salon's time zone.

## Double-booking protection

A PostgreSQL exclusion constraint (`btree_gist`) on `(master_id, period)` for active bookings
(Epic 5.2). The API maps the violation to `409 Conflict`.

## Notifications

Domain events → outbox table (same transaction) → background processor → SignalR, Telegram, SMS,
email. Reminders are Quartz.NET jobs.
