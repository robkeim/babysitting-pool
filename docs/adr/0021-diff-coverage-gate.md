# The full gate checks that changed lines are covered by tests

The rule "new code has tests" is about the diff, so the gate measures the diff. `scripts/diff-coverage.mjs` takes the lines a branch adds or changes (merge-base with `origin/main` to the working tree, plus untracked files) and requires at least 80% of the executable ones to be covered. Lines coverage does not track (comments, types, excluded files) are ignored; a diff with no executable lines passes.

- **Full gate only** (ADR 0015). The fast gate skips the API and Infrastructure tests, so coverage from it would under-report. The full gate runs `dotnet test` with `--collect:"XPlat Code Coverage"` (`src/backend/coverage.runsettings`, Cobertura) and `vitest run --coverage` (lcov), then runs the script over both. Playwright journeys do not count.
- **No global floor.** A global percentage lets old covered code hide new untested code and fails unrelated PRs when it drifts. Where coverage is thin overall is an audit question, answered once by a person, not a gate.
- **Exclusions** live in the runsettings (EF migrations, rate-limit migrations, `Program.cs`, `[ExcludeFromCodeCoverage]`, `[GeneratedCode]`, auto-properties; only the four production assemblies are measured) and in `vite.config.ts` (tests, `*.gen.ts`, `components/ui`, `main.tsx`).
- **Own script, not `diff-cover`.** The repo already needs Node; `diff-cover` would add a Python install to the gate's prerequisites. The script is about 130 lines and reads both formats.
- **Escape hatches.** `DIFF_COVERAGE_MIN=n` sets the threshold for one run; `DIFF_COVERAGE=off` skips the check. Line coverage proves a line ran, not that anything was asserted; it is a floor on effort, not a measure of test quality.
