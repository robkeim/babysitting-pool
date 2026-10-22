# Agent Instructions & Project Context

## Project Overview
A web application for a community babysitting co-op powered by a time-credit ledger.

## Architecture Guidelines
- **Backend:** .NET 10 / C#, SQLite, Hexagonal Architecture (Ports & Adapters), DDD, CQRS principles.
  - Domain layer must remain pure C# with zero dependencies on frameworks/databases.
  - Test runner: xUnit + AwesomeAssertions.
- **Frontend:** React, TypeScript, Tailwind CSS, shadcn/ui.
  - Test runner: Vitest + React Testing Library.
- **Documentation:** Functional specs reside in `/docs/specs/`. Always check these files for business rules before implementing domain logic.

## Execution Rules
1. Never write code without first checking the relevant specification in `/docs/specs/`.
2. Ensure domain models strictly enforce business invariants (e.g., balance floor >0).
3. When adding or modifying features, keep docs updated if architectural decisions change.
4. Write commit messages as Conventional Commits (`type(scope): description`, e.g. `feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`).

## Definition of done
- `scripts/check.sh` is green (`--fast` while iterating, `--e2e` when you touch a user journey). It is the single gate that git hooks, Claude Code hooks and CI all call (ADR `docs/adr/0008-enforcement-loop.md`).
- Do not edit lint config, `.editorconfig`, `.claude/`, `.husky/` or workflows to make a failure pass unless the issue says so. Never use `--no-verify` or force-push.
- Open PRs as drafts; branch `<issue>-<slug>`, Conventional Commit title, body ends in `Closes #n` (use the `pr` skill).

## Agent skills

### Issue tracker

Issues are tracked in GitHub Issues (robkeim/babysitting-pool) via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default vocabulary: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
