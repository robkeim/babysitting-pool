#!/usr/bin/env bash
# Stop: block finishing while scripts/check.sh is red (ADR 0008). stop_hook_active stops an endless loop.
input=$(cat)
[[ $(jq -r '.stop_hook_active // false' <<<"$input") == true ]] && exit 0
cd "$CLAUDE_PROJECT_DIR" || exit 0
if ! output=$(scripts/check.sh all --fast --stamped --quiet 2>&1); then
  printf 'scripts/check.sh is red; fix it before finishing.\n\n%s\n' "$(tail -40 <<<"$output")" >&2
  exit 2
fi
