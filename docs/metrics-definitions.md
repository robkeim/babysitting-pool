# Delivery metrics: definitions and blind spots

`node scripts/delivery-metrics.mjs --out docs/metrics.md` regenerates the dated snapshot in [metrics.md](metrics.md). Without `--out` it prints the Markdown. Aggregation is unit-tested with fixtures in `scripts/delivery-metrics.test.mjs`, run by `scripts/check.sh`.

| Metric | Definition | Blind spots |
|---|---|---|
| Throughput | Merged PRs per week (UTC, weeks start Monday), by merge time. | Counts PRs, not size; a one-line fix equals a feature. |
| Cycle time | Issue opened to PR merged (the issue is the first `Closes #n` in the PR body), and PR opened to merged. Median and p90. | Issue time includes time spent waiting in the backlog. PRs without `Closes #n` only feed the PR series. |
| Rework per ticket | Commits whose committer date is after the PR opened, plus `fix` issues (title starting `fix`) that mention `#<ticket>`. | The PR is opened after the full gate, so most work lands before it and rework is under-counted. Follow-ups are matched by text, so a fix that omits the number is missed. |
| Gate-failure rate | Failed runs over all runs per level, from `<common git dir>/check-history.log`, one `<UTC timestamp> <fast\|full\|standard> <pass\|fail>` line per real run (stamped skips are not logged). | Local to this machine, and only runs since the logging was added. Shows "n/a" when the file is absent. Includes failures from environment problems, not just code. |
| Tokens and cost per ticket | Sum of `message.usage` (input, output, cache read, cache creation) from `~/.claude/projects/**/*.jsonl`, subagent transcripts included, deduplicated by message id. A transcript maps to a ticket by the leading number of `gitBranch` (`<issue>-<slug>` or `robkeim/<issue>-<slug>`). | Transcripts are local to this machine and may be pruned. Branches without a leading issue number land under `unmapped`. Cost is an estimate from the `PRICES` constants at the top of the script, which must be kept current by hand; unknown models fall back to the Sonnet price. Work done outside Claude Code is not counted. |
