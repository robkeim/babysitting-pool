# Verification moves from GitHub CI to a local two-level gate

CI minutes and cache egress on the free private plan kept blocking PRs (ADR 0008 treated them as a budget). `.github/workflows/ci.yml` is removed; nothing runs on GitHub when a PR is opened or `main` is pushed. `scripts/check.sh` stays the one definition of green, now at two levels:

- **Fast gate**, for iterating: `scripts/check.sh all --fast --stamped --quiet`. Docs hygiene, backend format, build and the Domain and Application tests, frontend lint, format, types and unit tests. The Claude Code `Stop` hook runs it and blocks finishing while red.
- **Full gate**, before anything leaves the machine: `scripts/check.sh all --full --stamped --quiet`. The fast checks plus every backend test (API and Infrastructure, including the OpenAPI drift and endpoint-authorization tests), the Playwright journeys and the Postman collection drift check. The git `pre-push` hook and the Claude Code `PreToolUse` hook on `git push` and `gh pr create` run it.

Stamps are per level (`.git/check-stamp-fast`, `.git/check-stamp-full`) and hold the tree hash, so an unchanged tree is never re-run. A full stamp satisfies a fast request; a fast stamp never satisfies a full one. `--quiet` writes the whole output to `.git/check.log` and prints one line when green or the last 40 lines when red, which keeps an agent's token use per run small.

**Consequences, accepted.** The gate can be bypassed on a machine where the hooks are not installed, and no clean-environment run exists any more. `ci-ok` is no longer a check, and the PR title is no longer linted (Conventional Commits rely on the `pr` skill). The repository has one maintainer on one machine (ADR 0008 amendment), so this is the same trust boundary the hooks already relied on. Revisit with a second contributor or a paid plan.

Supersedes the CI parts of ADR 0008 (the `ci.yml` workflow, `ci-ok`, caches, draft-PR rules and the minutes budget); its single gate command, hooks and agent guardrails stand.

Amendment (2026-10-23): the `commit-msg` hook (`scripts/check-commit-msg.sh`) now lints each commit subject as a Conventional Commit. The squash-merge title on GitHub is the PR title, which nothing checks; the `pr` skill sets it.

Amendment (2026-11-03): PRs open ready for review, not as drafts. The full gate has already passed before `gh pr create`, so a draft state carries no information. The `PreToolUse` hook also covers `gh pr ready` and `gh pr merge`, and the `ship` skill defines opening and merging (merge `origin/main`, resolve conflicts, full gate green, squash-merge).

Amendment (2026-11-10): the `ship` skill's Open step runs one fresh-context reviewer agent after the full gate and before `gh pr create`, driven by [`docs/review-checklist.md`](../review-checklist.md) and built on the `code-review` skill (Standards and Spec axes). It posts its findings (finding, severity, resolved or accepted) as a PR comment; blocking findings are fixed or accepted in the PR body. It covers spec adherence, domain invariants, test quality, guardrail bypasses, docs and ADR updates and OpenAPI drift, the things the gate cannot judge. It does **not** replace an independent human: it shares the model family (and so its blind spots) with the agent that wrote the change, it sees only the diff and ticket, and its verdict is advisory. Accepted because the repository has one maintainer, who still reviews the PR before merge, and a logged review with a stated limit beats none. Follow-up, out of scope here: specialist reviewers (security, tests).
