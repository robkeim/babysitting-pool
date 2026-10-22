#!/usr/bin/env bash
# Cheap doc hygiene (ADR 0012), run first by scripts/check.sh:
#   - AGENTS.md stays a router of at most 60 lines
#   - every docs/adr/NNNN-*.md is listed in docs/adr/README.md
#   - every relative Markdown link in the repo's docs resolves
set -euo pipefail
cd "$(dirname "$0")/.."

fail=0
err() { echo "docs: $*" >&2; fail=1; }

lines=$(wc -l <AGENTS.md)
((lines <= 60)) || err "AGENTS.md has $lines lines (max 60): move detail into docs/ and link to it"

for adr in docs/adr/[0-9]*.md; do
  grep -qF "($(basename "$adr"))" docs/adr/README.md || err "$adr is not listed in docs/adr/README.md"
done

for file in README.md AGENTS.md GLOSSARY.md $(find docs -name '*.md' -not -path 'docs/research/*'); do
  dir=$(dirname "$file")
  while IFS= read -r target; do
    path=${target%%#*}
    [[ -z $path ]] && continue
    [[ -e $dir/$path ]] || err "$file: broken link to $target"
  done < <(grep -oE '\]\([^)[:space:]]+\)' "$file" | sed -E 's/^\]\((.*)\)$/\1/' | grep -vE '^(https?:|mailto:|#)' || true)
done

exit $fail
