# Babysitting Pool

A web app for a community babysitting co-op. Members earn and spend time credits instead of money: one hour of standard babysitting is 1.0 credit, tracked in a ledger.

- **Backend:** .NET 10 / C#, SQLite, hexagonal architecture
- **Frontend:** React, TypeScript, Tailwind CSS, shadcn/ui

## Start

```sh
scripts/dev.sh               # API and SPA together
scripts/check.sh --fast      # the fast gate: lint, build and quick tests
scripts/check.sh --full      # the full gate: every test, Playwright and drift checks (run before pushing)
```

Details, configuration and the API contract workflow: [docs/development.md](docs/development.md).

## Where things live

- Business rules: [docs/specs/](docs/specs/credit-engine.md) and the [glossary](GLOSSARY.md)
- Decisions: [docs/adr/](docs/adr/README.md)
- Testing: [docs/testing.md](docs/testing.md)
- Working with coding agents: [AGENTS.md](AGENTS.md)
