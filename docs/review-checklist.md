# Review checklist

Driven by the fresh-context reviewer that the `ship` skill runs before `gh pr create` ([ADR 0015](adr/0015-local-two-level-gate-replaces-github-ci.md), 2026-11-10 amendment). The reviewer sees only the diff, the ticket and this file, never how the change was written. Each item is pass, finding or not applicable. Skip anything `scripts/check.sh` already enforces (format, lint, types, test results).

1. **Spec matched.** The diff cites and follows the spec section the ticket's **Read first:** line names ([`docs/specs/`](specs/credit-engine.md)). Quote the spec line for any deviation.
2. **Domain invariants enforced.** Business rules live in domain models, not in handlers, controllers or the UI. A new state or amount path cannot bypass an invariant (for example the balance floor).
3. **Tests assert behavior.** New or changed behavior has a test that fails without it. Tests assert outcomes, not implementation details; no assertion-free tests, no skipped or weakened tests ([`docs/testing.md`](testing.md)).
4. **No bypass.** No `--no-verify`, force-push, lint or `.editorconfig` suppressions, disabled analyzers, or edits to `.claude/`, `.husky/` or workflows that the ticket did not authorize.
5. **Docs and ADRs updated.** A behavior or decision change updates the spec, glossary or an ADR in the same PR. ADRs are amended or superseded, never rewritten.
6. **OpenAPI drift.** An API change regenerates `docs/api/openapi.json` and the generated client and Postman collection ([`docs/development.md`](development.md)), and follows [ADR 0006](adr/0006-rest-contract.md).
7. **Scope.** Nothing outside the ticket's slice: no unrelated refactors, speculative options or dead code.

## Severity

- **blocking**: breaks an invariant, the spec or the contract, or bypasses a guardrail. Fix it, or accept it explicitly in the PR body with a reason.
- **non-blocking**: worth fixing, safe to ship.

## Findings comment

The reviewer's output is posted on the PR as one comment with this table, one row per finding (or the line `No findings.`):

| Finding | Severity | Status |
|---|---|---|
| `path:line` what is wrong | blocking / non-blocking | resolved / accepted |

`resolved` means a commit fixed it; `accepted` means the PR body states why it ships anyway.
