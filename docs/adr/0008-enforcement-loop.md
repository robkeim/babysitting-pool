# Enforcement is one local gate command, called by hooks, agents and CI, with CI minutes treated as a budget

One command per side defines "green": `scripts/check.sh [backend|frontend|all]` (`--fast` skips the slow tests, `--e2e` adds Playwright). Backend: `dotnet format --verify-no-changes`, build, tests. Frontend: `eslint --max-warnings 0`, `prettier --check`, `tsc -b`, `vitest run`. Git hooks, Claude Code hooks and CI all call the same underlying commands, so local and CI cannot drift.

Backend style lives in a root `.editorconfig` with rules at `warning` severity, so the existing `TreatWarningsAsErrors` makes them build-failing (`AnalysisMode=All`, `EnforceCodeStyleInBuild`, shared by `src/` and `tests/`), plus central package management and a pinned `global.json`. No third-party analyzers until agents drift. Frontend stays on npm with Node pinned in `.nvmrc` (also read by `actions/setup-node`), `prettier-plugin-tailwindcss`, and oxlint removed as ADR 0003 decided.

Local hooks are Husky + lint-staged: pre-commit formats and lints staged files only; pre-push runs `scripts/check --fast`. Hooks are the fast first line, never trusted: CI is authoritative.

CI is one `ci.yml` on `pull_request` and `push` to `main` (not every branch push, which would double-run each PR commit). A `changes` job (`dorny/paths-filter`) gates the backend, frontend and E2E jobs, and an always-running `ci-ok` aggregate job is the only required check, so a skipped path-filtered job never leaves a required check pending. Ubuntu only, `cancel-in-progress` per ref, `timeout-minutes` on every job, caches for NuGet, npm and the Playwright Chromium (by version). E2E builds the backend itself in its own job rather than sharing an artifact. Draft PRs run only the cheap jobs; E2E runs once the PR is ready for review. A push to `main` runs the cheap jobs only, and "branch must be up to date" stays off so parallel agents do not force re-runs. The Actions spending limit is $0. Napkin budget: about 12 billed minutes per full run, so about 160 full runs a month on the 2,000 free minutes; measure real job times after the first slice and retune.

PRs: one branch per issue (`<issue>-<slug>`), squash-merge only, PR title is a Conventional Commit and is linted in CI, body ends in `Closes #n`, branches deleted on merge. A repository ruleset requires `ci-ok` and blocks direct pushes to `main`, admins included, with no required human review (sole maintainer). Agents open PRs as drafts, may enable auto-merge once green, and use the `pr` skill for the body.

Agents are kept inside the guidelines by deterministic layers, not prompts alone, strongest first:

1. A committed `PreToolUse` hook on `git push` and `gh pr create` runs `scripts/check --fast` and blocks the command while red.
2. Permission denies for `--no-verify`, `-n` and force-push, so the git hook cannot be skipped.
3. A pass stamp: a successful `scripts/check` writes the tree hash to `.git/check-stamp`; the push gate skips the re-run when it matches, proving "tested at this exact content" for free.
4. A `PostToolUse` hook formats edited files; a `Stop` hook runs `scripts/check --fast` and blocks finishing while red.
5. `AGENTS.md` carries a short definition of done pointing at `scripts/check` and the ADRs; agents run `check --e2e` when they touch a user journey (PR template checkbox).
6. Agents must not edit lint config, `.editorconfig` or workflows to make a failure pass unless the issue says so; CODEOWNERS on those files makes any change visible.

We chose this over relying on CI alone because CI minutes are the scarce resource on a private repo: every failure that reaches CI that a local gate could have caught is wasted budget. Lefthook and third-party analyzers were considered; Husky is kept because Node is already required, and analyzers add noise before there is evidence of drift.

Amendment (2026-10): the repository is private on the free GitHub plan, which offers neither rulesets nor branch protection. The ruleset is therefore not applied. Instead the pre-push hook and a Claude Code deny rule refuse pushes to `main`, and the repo allows squash-merge only with branches deleted on merge. `ci-ok` still runs and reports on every PR, but nothing server-side stops a merge while it is red. This is accepted because there is one maintainer on one machine; revisit (GitHub Pro or a public repo) if that changes.

Amendment (2026-10-23): superseded by [ADR 0015](0015-local-two-level-gate-replaces-github-ci.md) for everything about GitHub CI. `ci.yml`, `ci-ok`, the caches, the draft-PR rules and the minutes budget are gone; verification is a local fast gate and full gate.
