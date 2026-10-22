# Architecture decisions

One line per ADR so you can pick what to open (ADR 0013). Read an ADR only if its area matches your task. ADRs are append-only: a changed decision gets a new ADR and the old one is marked `Superseded by`.

| ADR | Decision | Area |
|---|---|---|
| [0001](0001-credits-stored-as-quarter-credit-integers.md) | Credits are a value object stored as integer quarter-credits | backend, domain |
| [0002](0002-plain-handlers-no-mediator.md) | Use cases are plain handler classes, not a mediator | backend |
| [0003](0003-frontend-stack.md) | Client-only SPA on TanStack Router and Query, ESLint as the single lint gate | frontend |
| [0004](0004-sqlite-persistence-and-test-strategy.md) | SQLite with a concurrency token, one transaction per handler, tested on real SQLite | backend, testing |
| [0005](0005-frontend-feature-slices.md) | Frontend feature slices with lint-enforced one-way dependencies | frontend |
| [0006](0006-rest-contract.md) | OpenAPI-first REST contract, cookie sessions, single origin | backend, frontend, api |
| [0007](0007-settle-time-driven-processes.md) | Time-driven changes applied by one idempotent Settle run | backend, domain |
| [0008](0008-enforcement-loop.md) | One local gate command for hooks, agents and CI; CI minutes are a budget | ops |
| [0009](0009-react-intl-for-frontend-i18n.md) | react-intl with key-based catalogs and a pseudo-locale | frontend, i18n |
| [0010](0010-structured-logging-and-correlation.md) | Structured Serilog, W3C trace correlation, never-log list | backend, ops |
| [0011](0011-local-dev-configuration-and-bootstrap.md) | One-command dev, co-op rules as code, first Admin bootstrapped by configuration | ops, backend |
| [0012](0012-passwordless-auth.md) | Passwordless auth: single-use hashed links and server-side sessions | backend, auth |
| [0013](0013-documentation-structure-and-token-economy.md) | Doc layers, router-style AGENTS.md, token budget, one-slice tickets | docs, ops |
| [0014](0014-security-and-privacy-baseline.md) | Deny-by-default authorization, rate limits and hardening headers, and personal-data erasure separate from Offboarding | backend, auth, ops |
