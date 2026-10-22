# Local development

Run the API and the SPA together; the Vite dev server proxies `/api` to the API, so the browser sees one origin (ADR 0006).

```sh
scripts/dev.sh        # both, Ctrl-C stops both
```

Or in two terminals:

```sh
dotnet run --project src/backend/BabysittingPool.Api --launch-profile http   # API on :5120
cd src/frontend && npm run dev                                               # SPA on :5173
```

- **Database:** a SQLite file `babysitting.db` is created next to the API and migrated at startup (ADR 0004). Delete it to start fresh.
- **Identity:** there is no sign-in yet. Outside Production the caller names the current Member in an `X-Dev-Member-Id` header; the Vite proxy sends the Member that the API seeds in Development (10.0 credits, see `DevIdentity`).
- **Configuration:** `appsettings*.json`, then environment variables (`Section__Key`), then `dotnet user-secrets`; no `.env` files. Only deployment values go here (`Bootstrap:AdminEmail`, email provider, database path). Co-op rules such as Minimum Lead Time are defaults in the `CoopSettings` record, not configuration (ADR 0011).
- **API contract:** after changing an endpoint, run `UPDATE_OPENAPI=1 dotnet test --filter OpenApiDocumentTests`, then `npm run generate:api` and `npm run generate:postman` in `src/frontend` and commit the generated files (`schema.gen.ts`, `docs/api/postman_collection.json`).
- **Migrations:** `dotnet dotnet-ef migrations add <Name> --project src/backend/BabysittingPool.Infrastructure --output-dir Persistence/Migrations`.

## Checks

```sh
scripts/check.sh [backend|frontend|all] [--fast] [--e2e]
```

The one definition of green (ADR 0008): backend `dotnet format --verify-no-changes`, build, tests; frontend `eslint --max-warnings 0`, `prettier --check`, `tsc -b`, `vitest run`. `--fast` skips the backend API and Infrastructure tests, `--e2e` adds Playwright. A passing `all` run stamps the tree in `.git/check-stamp`; the pre-push hook and the Claude Code push gate skip the re-run when the stamp matches. Husky hooks install on `npm install` in `src/frontend`; pre-commit formats staged files, pre-push runs `scripts/check.sh all --fast --stamped`.

## Postman

1. Run `scripts/dev.sh`.
2. In Postman, import `docs/api/postman_collection.json`. Its `baseUrl` is `http://localhost:5173` (the Vite origin, not the API port) and it sends `X-Requested-With` on every request.
3. Today the Vite proxy names the seeded Member for you, so requests work immediately. Once sign-in exists (ADR 0011) you sign in by requesting a link, reading it from the console or `GET /api/dev/outbox`, and opening it; Postman keeps the session cookie.
4. After an endpoint change, regenerate with `npm run generate:postman` in `src/frontend`.

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
