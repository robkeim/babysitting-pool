# Onboarding: your first week

A reading order, not a rule set. Each step names the doc that owns the topic; read it there. Stop reading a layer when you have what the day's task needs (ADR 0013).

## Day 1: run it

1. [`README.md`](../README.md), then [`docs/development.md`](development.md): start the stack with `scripts/dev.sh` and sign in as a seeded Member.
2. Run the fast gate once so you know it is green on your machine ([`CONTRIBUTING.md`](../CONTRIBUTING.md#branch-commit-gate)).
3. Follow [`docs/demo-script.md`](demo-script.md) to see the product end to end.

## Day 2: the domain

1. [`GLOSSARY.md`](../GLOSSARY.md): the vocabulary used in code, tickets and specs.
2. [`docs/specs/credit-engine.md`](specs/credit-engine.md) (credits and requests), then [`docs/specs/sign-in.md`](specs/sign-in.md) (invitations and sign-in).

## Day 3: how the code is organized

1. [`docs/adr/README.md`](adr/README.md): scan the one-line index, then read the ADRs tagged for the side you will touch (`backend` or `frontend`).
2. [`docs/testing.md`](testing.md), and [`docs/design.md`](design.md) if you will touch the UI.

## Day 4: how work flows

1. [`AGENTS.md`](../AGENTS.md) and [ADR 0013](adr/0013-documentation-structure-and-token-economy.md): why docs are a router plus read-first lines.
2. [ADR 0008](adr/0008-enforcement-loop.md), [ADR 0015](adr/0015-local-two-level-gate-replaces-github-ci.md) and [`docs/agents/`](agents/issue-tracker.md): the gate, the hooks, the tracker and the review step.
3. [`CONTRIBUTING.md`](../CONTRIBUTING.md): ticket, gate, review and decision flow.

## Day 5: ship something small

Pick a `ready-for-agent` or `ready-for-human` ticket ([`CONTRIBUTING.md`](../CONTRIBUTING.md#pick-up-a-ticket)) and take it through to a merged PR. Skim [`docs/metrics.md`](metrics.md) afterwards to see what your ticket added to throughput and cycle time.
