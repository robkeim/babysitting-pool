# Local development

Run the API and the SPA side by side; the Vite dev server proxies `/api` to the API, so the browser sees one origin (ADR 0006).

```sh
dotnet run --project src/backend/BabysittingPool.Api --launch-profile http   # API on :5120
cd src/frontend && npm run dev                                               # SPA on :5173
```

- **Database:** a SQLite file `babysitting.db` is created next to the API and migrated at startup (ADR 0004). Delete it to start fresh.
- **Identity:** there is no sign-in yet. Outside Production the caller names the current Member in an `X-Dev-Member-Id` header; the Vite proxy sends the Member that the API seeds in Development (10.0 credits, see `DevIdentity`).
- **API contract:** after changing an endpoint, run `UPDATE_OPENAPI=1 dotnet test --filter OpenApiDocumentTests`, then `npm run generate:api` in `src/frontend` and commit both generated files.
- **Migrations:** `dotnet dotnet-ef migrations add <Name> --project src/backend/BabysittingPool.Infrastructure --output-dir Persistence/Migrations`.

## End-to-end tests (Playwright)

```sh
cd src/frontend
npx playwright install chromium   # once
npm run test:e2e                  # or: npx playwright test
```

- Needs the .NET SDK on the path. Nothing else to start: global setup builds the API once, and each Playwright worker then starts its own API (free port, temp-file SQLite, `E2E` environment, `FakeTimeProvider`) and its own Vite dev server pointed at that API. No API mocks. The worker fixture is in `src/frontend/e2e/fixtures.ts`.
- **Test-only endpoints** live under `/api/test` and are mapped only in `E2E` (a backend test asserts 404 elsewhere); they are not in the OpenAPI document. `POST /api/test/members` seeds a Member with Grants given in quarter-credits, `PUT /api/test/clock` sets the server clock.
- Tests call `backend.setClock(...)` first, seed uniquely owned Members, and use `signInAs(page, memberId)` (the `X-Dev-Member-Id` header) until real sign-in exists.
- Traces are kept on failure under `src/frontend/test-results`; open one with `npx playwright show-trace <trace.zip>`.
