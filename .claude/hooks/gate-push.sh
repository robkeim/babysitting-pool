#!/usr/bin/env bash
# PreToolUse on Bash: block `git push` and `gh pr create` while the full gate is red (ADR 0015).
command=$(jq -r '.tool_input.command // ""')
if [[ ! $command =~ (^|[;&|[:space:]])(git[[:space:]]+push|gh[[:space:]]+pr[[:space:]]+create)([[:space:]]|$) ]]; then
  exit 0
fi
cd "$CLAUDE_PROJECT_DIR" || exit 0
if ! output=$(scripts/check.sh all --full --stamped --quiet 2>&1); then
  printf 'scripts/check.sh is red; fix it before pushing or opening a PR.\n\n%s\n' "$(tail -40 <<<"$output")" >&2
  exit 2
fi
