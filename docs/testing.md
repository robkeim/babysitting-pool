# Testing strategy

Decisions and rationale: [ADR 0004](adr/0004-sqlite-persistence-and-test-strategy.md), [ADR 0003](adr/0003-frontend-stack.md).

## Backend (xUnit + AwesomeAssertions)

From many tests to few:

1. **Domain unit tests**: pure C#, no I/O, no clock (pass `now` in). The bulk of the tests.
2. **Application handler tests**: real handlers on real SQLite (fresh in-memory database per test, built from the real migrations). Fake only the email and clock ports. The fixture builds its container with `AddCoopCore`, the same entry point `ApiHost` uses, varying only the database path, email sender and clock. Per feature: the happy path and the main failure.
3. **API tests**: `WebApplicationFactory` over HTTP, for the contract surface only (status codes, ProblemDetails, auth).
4. **Architecture tests** (`Architecture/LayerBoundaryTests.cs`, NetArchTest): layer boundaries, in the fast gate. They fail on any type that references a forbidden namespace:

| Layer | Must not reference |
|---|---|
| Domain | Application, Infrastructure, Api, TestSupport, E2E, ASP.NET Core, EF Core, `Microsoft.Data`, `Microsoft.Extensions`, `System.Data`, `System.Net.Http`, MailKit, MimeKit, Serilog |
| Application | Infrastructure, Api, TestSupport, E2E, ASP.NET Core, EF Core, `Microsoft.Data`, `System.Data`, `System.Net.Http`, MailKit, MimeKit, Serilog (DI and logging abstractions are allowed) |
| Infrastructure | Api, TestSupport, E2E |
| Api | TestSupport, E2E (it is the composition root, so it may reference Infrastructure) |
| A handler (`*Handler` in Application) | any other handler (ADR 0002); share logic through a service such as `InvitationIssuer` or a view factory |

Rules:
- No hand-written in-memory repositories. An in-memory SQLite database lives only while its connection is open, so the fixture holds one connection for the test's lifetime (or uses a temp file).
- Clock: inject `TimeProvider`; tests use `FakeTimeProvider`. Never call `DateTime.UtcNow`.
- Concurrency (first claim wins) has an integration test with two concurrent claims; the loser gets 409.
- A test asserts the Ledger triggers reject `UPDATE`/`DELETE`.

## Frontend (Vitest + RTL + user-event + MSW)

1. Pure logic unit tests.
2. Component and route tests with MSW mocking REST (the bulk).
3. Playwright E2E (below).

Time-dependent frontend code is display formatting only (countdown labels, co-op time zone). The backend is the authority on every time rule. Component tests use Vitest fake timers with an injectable "now".

## End-to-end (Playwright, Chromium, part of the full gate)

- Real API plus SPA, no API mocks. A worker-scoped fixture starts one backend per Playwright worker (the E2E host: own port, temp-file SQLite, own `FakeTimeProvider`, `E2E` environment, random token).
- Test-only endpoints, in the `BabysittingPool.TestSupport` project that only the `BabysittingPool.E2E` host references (API tests assert 404 in Production and that the API assembly does not reference it), each needing the per-worker `E2E:Token`: set the server clock, read the email outbox, seed state through application code.
- Each test sets the clock to an absolute time at its start and creates uniquely named Members. Browser time uses `page.clock`, kept in step with the server clock.
- Sign-in flows read the magic link from the fake outbox.
- Journeys, one file each: Invitation to account (10.0 Credits); Admin invites, re-sends and the person joins (own stack via the `ownBackend` fixture, because it consumes the bootstrap Admin Invitation that the shared stack keeps for the sign-in journey); post, claim and complete a Request with balances on both sides; cancel with refund; claim race (two browser contexts); Demurrage after a time jump.
- Traces kept on failure. If the suite exceeds about 5 minutes, move slow journeys to nightly.

## Coverage

The full gate checks diff coverage, not a global percentage ([ADR 0021](adr/0021-diff-coverage-gate.md)): at least 80% of the executable lines a branch changes must be hit by the Vitest or xUnit tests (Playwright does not count). Cover a line with an assertion, not just a call; the number cannot tell the difference. Generated code, migrations and `components/ui` are excluded in `src/backend/coverage.runsettings` and `vite.config.ts`.
