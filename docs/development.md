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
- **Sign-in:** passwordless (ADR 0012, `docs/specs/sign-in.md`). In Development the API seeds an Admin (`admin@example.test`, or `Bootstrap:AdminEmail`) and three Members (`ada@`, `ben@`, `cleo@example.test`, holding 10.0, 0.0 and 25.5 credits), and logs the Admin's sign-in link to the console at startup. Nothing is sent: for any other seeded Member, request a link at `/sign-in` and read it from the console or `GET /api/dev/outbox?to=<email>`, then open it.
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
3. Sign in: `POST /api/auth/sign-in-links` with `{ "email": "admin@example.test" }`, read the link from the console or `GET /api/dev/outbox`, then `POST /api/auth/sessions` with the `token` from the link. Postman keeps the session cookie.
4. After an endpoint change, regenerate with `npm run generate:postman` in `src/frontend`.

## End-to-end tests (Playwright)

```sh
cd src/frontend
npx playwright install chromium   # once
npm run test:e2e                  # or: npx playwright test
```

- Needs the .NET SDK on the path. Nothing else to start: global setup builds the API once, and each Playwright worker then starts its own API (free port, temp-file SQLite, `E2E` environment, `FakeTimeProvider`) and its own Vite dev server pointed at that API. No API mocks. The worker fixture is in `src/frontend/e2e/fixtures.ts`.
- **Test-only endpoints** live under `/api/test` and are mapped only in `E2E` (a backend test asserts 404 elsewhere); they are not in the OpenAPI document. `POST /api/test/members` seeds a Member with Grants given in quarter-credits, `POST /api/test/sessions` returns a session token for a Member, `PUT /api/test/clock` sets the server clock. The fake outbox is readable at `GET /api/dev/outbox` (Development and E2E).
- Tests call `backend.setClock(...)` first, seed uniquely owned Members, and use `backend.signIn(page.context(), memberId)` to skip email. The sign-in journey itself starts from the bootstrap Admin Invitation (`Bootstrap__AdminEmail`) and reads links with `backend.latestLink(email)`.
- Traces are kept on failure under `src/frontend/test-results`; open one with `npx playwright show-trace <trace.zip>`.
