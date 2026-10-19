# TDD workflow

1. **Red** – write the smallest test that describes the next behaviour. Run it and watch it fail
   for the right reason (an assertion, not a typo). Commit: `test: ... (red)`.
2. **Green** – write the minimum code that makes it pass. Commit: `feat: ... (green)`.
3. **Refactor** – clean up with all tests green. Commit: `refactor: ...`.

## Which test where

| Level | Project / folder | Use for |
|---|---|---|
| Domain unit | `tests/Mastera.Domain.UnitTests` | Rules, state machines, the availability engine. No I/O. |
| Application unit | `tests/Mastera.Application.UnitTests` | Handlers, validation, authorization with fakes (NSubstitute). |
| Integration | `tests/Mastera.Infrastructure.IntegrationTests` | EF mappings, query filters, RLS, constraints, migrations on real PostgreSQL. |
| Functional | `tests/Mastera.Api.FunctionalTests` | HTTP endpoints end to end, auth, tenant isolation. |
| Architecture | `tests/Mastera.ArchitectureTests` | Layer dependency rules. |
| Component | `web/**/src/*.test.tsx` | Forms, calendar, booking flow (Vitest + Testing Library + MSW). |
| End-to-end | `web/e2e` | The few critical journeys (Playwright). |

## Conventions

- Test names describe behaviour: `Unknown_route_returns_404_as_ProblemDetails`.
- Arrange / act / assert separated by blank lines.
- Pass `TestContext.Current.CancellationToken` to async calls in xUnit v3 tests.
- Time is never read from the system clock in domain code: inject NodaTime `IClock` and use `FakeClock` in tests.
- Frontend tests query by role and label (what the user sees), not by CSS class or test id.
