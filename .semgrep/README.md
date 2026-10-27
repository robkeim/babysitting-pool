# Semgrep rules

Vendored copies of the Semgrep registry rulesets `p/csharp`, `p/typescript` and `p/react`, so the full gate (ADR 0014) runs offline and does not change under us.

Refresh: `for p in csharp typescript react; do curl -sfL https://semgrep.dev/c/p/$p -o .semgrep/$p.yml; done`, then run `scripts/check.sh all --full` and triage new findings.

`project.yml` is ours, not vendored: keep it out of the refresh above.
