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

- **Database:** a SQLite file `babysitting.db` is created next to the API and migrated at startup (ADR 0004). Delete it to start fresh. Rate-limit counters sit in `babysitting.db.rate-limits` beside it (ADR 0014); delete that file to reset the limits.
- **Sign-in:** passwordless (ADR 0012, `docs/specs/sign-in.md`). In Development the API seeds an Admin (`admin@example.test`, or `Bootstrap:AdminEmail`) and three Members (`ada@`, `ben@`, `cleo@example.test`, holding 10.0, 0.0 and 25.5 credits), and logs the Admin's sign-in link to the console at startup. By default nothing is sent: for any other seeded Member, request a link at `/sign-in` and read it from the console or `GET /api/dev/outbox?to=<email>`, then open it.
- **Real email in Development (optional):** set the SMTP secrets once and sign-in and invitation emails are delivered for real, on top of the console and outbox. Gmail needs an [app password](https://support.google.com/accounts/answer/185833) (2-Step Verification on), not the account password.

  ```sh
  cd src/backend/BabysittingPool.Api
  dotnet user-secrets set "Email:Smtp:Address" "<your base address>"
  dotnet user-secrets set "Email:Smtp:Password" "<app password>"
  ```

  The seed then uses plus-addresses of that base address, all landing in one inbox: `<local>+admin@<domain>` (Admin) and `+user1`, `+user2`, `+user3` (10.0, 0.0 and 25.5 credits). The startup Admin link is never emailed, so restarts send nothing; request one at `/sign-in`. Delete `babysitting.db` to reseed after switching. `Email:Smtp:Host` and `Email:Smtp:Port` override `smtp.gmail.com:587` (STARTTLS). E2E and backend tests ignore these secrets and always use the fake outbox; without them Development behaves as above.
- **Configuration:** `appsettings*.json`, then environment variables (`Section__Key`), then `dotnet user-secrets`; no `.env` files. Only deployment values go here (`Bootstrap:AdminEmail`, email provider, database path, `ForwardedHeaders:KnownProxies` set to the reverse proxy's address, otherwise every client is limited under the proxy's IP). Co-op rules such as Minimum Lead Time are defaults in the `CoopSettings` record, not configuration (ADR 0011).
- **Feature flags:** declared in `FeatureFlagRegistry.Production` (ADR 0016). `Features__<Name>=true|false` sets a flag's first value and is ignored once the flag has a row; an undeclared name stops startup. After that an Admin changes it with `PUT /api/admin/feature-flags`, with no restart. Gate an endpoint with `.RequireFeature("name")`; in `E2E` each declared flag also gets `GET /api/test/gated/{name}`. The SPA reads `GET /api/feature-flags` (resolved for the caller, anonymous-safe, ETag/304 for clients that send `If-None-Match`; `/api` stays `no-store`, ADR 0014) with `useFeatureFlag("name")` from `features/flags`, which is off while loading or for an unknown name. Unit tests wrap components in `FeatureFlagsTestProvider flags={{ name: true }}`.
- **API contract:** after changing an endpoint, run `UPDATE_OPENAPI=1 dotnet test --filter OpenApiDocumentTests`, then `npm run generate:api` and `npm run generate:postman` in `src/frontend` and commit the generated files (`schema.gen.ts`, `docs/api/postman_collection.json`).
- **Migrations:** `dotnet dotnet-ef migrations add <Name> --project src/backend/BabysittingPool.Infrastructure --output-dir Persistence/Migrations`. For the rate-limit file add `--context RateLimitDbContext` and `--output-dir Persistence/RateLimitMigrations`.

## Checks

```sh
scripts/check.sh [backend|frontend|all] [--fast] [--e2e] [--full] [--stamped] [--quiet]
```

The one definition of green, at two levels (ADR 0015). The **fast gate** (`all --fast`) is for iterating: docs hygiene, a gitleaks scan of the working tree, backend `dotnet format --verify-no-changes`, build and the Domain and Application tests, frontend `eslint --max-warnings 0`, `prettier --check`, `tsc -b` and `vitest run`. The **full gate** (`all --full`) adds the backend API and Infrastructure tests, the Playwright journeys a check that `docs/api/postman_collection.json` is current (it is regenerated in place when stale), and the security scans of ADR 0014: gitleaks over git history, Semgrep with the rules vendored in `.semgrep/`, `dotnet list package --vulnerable` and `npm audit --audit-level=high`. The scans need `brew install gitleaks semgrep`; the gate fails with that hint when a tool is missing. Suppress a Semgrep false positive inline with `// nosemgrep: <rule-id> -- <reason>`. A passing `all` run stamps the tree in `.git/check-stamp-fast`, a passing `--full` run also in `.git/check-stamp-full`; `--stamped` skips the re-run when the stamp for that level matches. `--quiet` prints one line when green, or the last 40 lines and the log path (`.git/check.log`) when red.

Husky hooks install on `npm install` in `src/frontend`: pre-commit formats staged files; commit-msg rejects a subject that is not a Conventional Commit (`scripts/check-commit-msg.sh`); pre-push runs `scripts/check.sh all --full --stamped --quiet`. Claude Code runs the fast gate on `Stop` and the full gate before `git push` and `gh pr create`. Nothing runs on GitHub.

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
- **Test-only endpoints** live under `/api/test` and are mapped only in `E2E` (a backend test asserts 404 elsewhere); they are not in the OpenAPI document. `POST /api/test/members` seeds a Member with Grants given in quarter-credits, `POST /api/test/sessions` returns a session token for a Member, `PUT /api/test/clock` sets the server clock (forward or back, since a worker's backend outlives its tests), `POST /api/test/settle` runs Settle. `POST /api/test/feature-flags` overrides a flag (body: `name`, `enabled`, optional `rolloutPercent`, `allowedMemberIds`, `deniedMemberIds`, `roleRollouts`); it also needs the `E2E:Token` setting and a matching `X-E2E-Token` header, and answers 404 otherwise. Production refuses to start when `E2E:Token` is set. The fake outbox is readable at `GET /api/dev/outbox` (Development and E2E).
- Tests call `backend.setClock(...)` first, seed uniquely owned Members, and use `backend.signIn(page.context(), memberId)` to skip email. The sign-in journey itself starts from the bootstrap Admin Invitation (`Bootstrap__AdminEmail`) and reads links with `backend.latestLink(email)`.
- Traces are kept on failure under `src/frontend/test-results`; open one with `npx playwright show-trace <trace.zip>`.
