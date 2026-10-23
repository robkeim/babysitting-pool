#!/usr/bin/env bash
# Conventional Commits (AGENTS.md rule 4), run by .husky/commit-msg now that GitHub CI no longer lints titles (ADR 0015):
#   type(scope)?!?: description
# Merge, Revert and fixup!/squash! subjects are left alone.
set -euo pipefail

subject=$(head -n 1 "$1")
types='feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert'

if [[ $subject =~ ^(Merge|Revert|fixup!|squash!)[[:space:]] ]] || [[ $subject =~ ^($types)(\([a-z0-9._/-]+\))?!?:[[:space:]]+[^[:space:]] ]]; then
  exit 0
fi

echo "commit-msg: '$subject' is not a Conventional Commit: type(scope): description" >&2
echo "commit-msg: types: ${types//|/, }" >&2
exit 1
