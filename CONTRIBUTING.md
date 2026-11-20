# Contributing

This project has one human maintainer, with coding agents as the other engineers. The process is built so a second human could join without changing it. Each rule lives in one doc and this file links to it (ADR 0013). New here? Follow [`docs/onboarding.md`](docs/onboarding.md).

## Set up

- Run the app, configuration and seeded sign-ins: [`docs/development.md`](docs/development.md).
- Install the git hooks with `npm install` in `src/frontend`. The gate is only trusted where they are installed ([ADR 0015](docs/adr/0015-local-two-level-gate-replaces-github-ci.md)).

## Pick up a ticket

- **Ready** means labeled `ready-for-agent` or `ready-for-human`, and no open blocker; agent tickets also carry a **Read first:** line ([`docs/agents/triage-labels.md`](docs/agents/triage-labels.md), [`docs/agents/issue-tracker.md`](docs/agents/issue-tracker.md)).
- Ticket sizing: [ADR 0013](docs/adr/0013-documentation-structure-and-token-economy.md). What to read before coding: [`AGENTS.md`](AGENTS.md).

## Branch, commit, gate

- Branch, commit, PR and definition of done: [`AGENTS.md`](AGENTS.md). What the hooks lint: [ADR 0015](docs/adr/0015-local-two-level-gate-replaces-github-ci.md).
- Fast gate while iterating, full gate before pushing: [ADR 0015](docs/adr/0015-local-two-level-gate-replaces-github-ci.md), [`docs/development.md`](docs/development.md#checks). Tests: [`docs/testing.md`](docs/testing.md), [ADR 0021](docs/adr/0021-diff-coverage-gate.md).

## Review and merge

- A fresh-context reviewer runs before the PR opens ([`docs/agents/review.md`](docs/agents/review.md), [`docs/review-checklist.md`](docs/review-checklist.md)). It is advisory; the maintainer reviews before merge. The limits are accepted in [ADR 0015](docs/adr/0015-local-two-level-gate-replaces-github-ci.md).
- The `ship` skill opens and merges PRs; the `pr` skill writes the body.

## Decisions and accepted risks

- A decision a newcomer would ask "why" about gets an ADR in the PR that makes it, with an index line in [`docs/adr/README.md`](docs/adr/README.md). ADRs are append-only ([`AGENTS.md`](AGENTS.md#rules)).
- Known gaps are written down, not hidden: no GitHub CI ([ADR 0015](docs/adr/0015-local-two-level-gate-replaces-github-ci.md)), no uptime alerting ([ADR 0018](docs/adr/0018-deployment-on-fly-io.md)).
- Delivery numbers: [`docs/metrics.md`](docs/metrics.md).
