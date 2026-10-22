# Agent Instructions

A community babysitting co-op web app powered by a time-credit ledger. This file is a router: rules live in the docs it links to (ADR 0013), never restated here.

## Stack
- **Backend:** .NET 10 / C#, SQLite, Hexagonal Architecture, DDD, CQRS. The domain layer is pure C# with no framework or database dependencies. Tests: xUnit + AwesomeAssertions.
- **Frontend:** React, TypeScript, Tailwind CSS, shadcn/ui. Tests: Vitest + React Testing Library.

## Read first
Read only the row that matches your task. Skip files the ticket's **Read first:** line does not name.

| Task | Read |
|---|---|
| Domain logic or business rules | the relevant section of [`docs/specs/`](docs/specs/credit-engine.md) (`credit-engine.md` for credits and requests, `sign-in.md` for invitations and sign-in), then [`GLOSSARY.md`](GLOSSARY.md) |
| Backend structure, persistence | [ADR index](docs/adr/README.md), then the ADRs tagged `backend` |
| API change | [ADR 0006](docs/adr/0006-rest-contract.md), [`docs/development.md`](docs/development.md) (regenerating the contract) |
| Frontend | ADRs tagged `frontend` in the [ADR index](docs/adr/README.md) |
| Tests | [`docs/testing.md`](docs/testing.md) |
| Run or configure locally | [`docs/development.md`](docs/development.md) |
| Issues, labels, PRs | [`docs/agents/`](docs/agents/issue-tracker.md) |

`docs/research/` and generated files (`schema.gen.ts`, Postman collection, EF migrations, lockfiles) are denied for reading in `.claude/settings.json`. Grep `docs/api/openapi.json` instead of reading it.

## Rules
1. Never write code without first checking the relevant specification section.
2. Domain models strictly enforce business invariants (e.g. balance floor > 0).
3. Update the spec, glossary or an ADR in the same PR when behaviour or a decision changes. ADRs are append-only: supersede with a new ADR, or amend with a dated note.
4. Commit messages are Conventional Commits (`type(scope): description`).

## Definition of done
- `scripts/check.sh` is green (`--fast` while iterating, `--e2e` when you touch a user journey). It is the single gate for git hooks, Claude Code hooks and CI ([ADR 0008](docs/adr/0008-enforcement-loop.md)).
- Do not edit lint config, `.editorconfig`, `.claude/`, `.husky/` or workflows to make a failure pass unless the issue says so. Never use `--no-verify` or force-push.
- Open PRs as drafts; branch `<issue>-<slug>`, Conventional Commit title, body ends in `Closes #n` (use the `pr` skill).

## Tickets
Issues live in GitHub (`robkeim/babysitting-pool`) via `gh`: [`docs/agents/issue-tracker.md`](docs/agents/issue-tracker.md). Labels: [`docs/agents/triage-labels.md`](docs/agents/triage-labels.md). Domain docs: [`docs/agents/domain.md`](docs/agents/domain.md). One ticket is one vertical slice, one PR, one context window.
