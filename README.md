# Mastera

Multi-tenant booking platform for barber shops, beauty, nail and make-up salons.
Clients book a free slot with a master; masters get notified; salon admins manage
masters, services, bookings and see analytics. Each salon gets its own branded page.

## Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core, Clean Architecture, EF Core 10, PostgreSQL 17 (Npgsql + NodaTime) |
| Frontend | React 19, TypeScript, Vite, Tailwind CSS 4, TanStack Query, i18next (ka / ru / en) |
| Tests | xUnit v3, Shouldly, NSubstitute, Testcontainers, NetArchTest · Vitest, Testing Library, MSW, Playwright |

Only free, open-source libraries are used. CI fails if a package with another license is added
(see `build/allowed-licenses.json`).

## Repository layout

```
src/
  Mastera.Domain          Entities, value objects, domain services. No dependencies.
  Mastera.Application     Use cases, ports (interfaces), validation. Depends on Domain.
  Mastera.Infrastructure  EF Core, PostgreSQL, external services. Implements Application ports.
  Mastera.Api             HTTP endpoints, auth, middleware. Composition root.
tests/
  Mastera.Domain.UnitTests
  Mastera.Application.UnitTests
  Mastera.Infrastructure.IntegrationTests   real PostgreSQL via Testcontainers (needs Docker)
  Mastera.Api.FunctionalTests               in-memory API via WebApplicationFactory
  Mastera.ArchitectureTests                 enforces the layer dependency rule
web/
  apps/dashboard          salon admin / master / receptionist app   (http://localhost:5173)
  apps/booking            public booking site                       (http://localhost:5174)
  packages/ui             shared React components
  packages/i18n           translations (ka, ru, en)
  packages/api-client     typed API client
  e2e/                    Playwright tests
build/                    license guard
docs/                     architecture and workflow notes
```

## Prerequisites

- .NET SDK 10 (`dotnet --version`)
- Node.js 22+ and pnpm 10 (`corepack enable`)
- Docker Desktop (local services and integration tests)

## Run locally

```powershell
docker compose up -d                      # Postgres, SeaweedFS (S3), Mailpit
dotnet run --project src/Mastera.Api      # API on http://localhost:5080
cd web
pnpm install
pnpm dev:dashboard                        # http://localhost:5173
pnpm dev:booking                          # http://localhost:5174
```

Useful local URLs: OpenAPI `http://localhost:5080/openapi/v1.json`, health `http://localhost:5080/health`,
Mailpit inbox `http://localhost:8025`.

## Tests

```powershell
dotnet test Mastera.slnx                  # all backend tests (Docker must be running)
cd web
pnpm test                                 # unit + component tests
pnpm e2e                                  # Playwright (first time: pnpm exec playwright install chromium)
pnpm lint; pnpm typecheck
pnpm test:build; pnpm check:licenses      # license guard
```

## How we work: TDD

Every change is **red → green → refactor**. See [docs/tdd.md](docs/tdd.md).
