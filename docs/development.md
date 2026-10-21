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
