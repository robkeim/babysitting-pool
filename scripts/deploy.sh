#!/usr/bin/env bash
# Deploys to Fly.io (ADR 0018). Refuses to run unless the full-gate stamp matches the current tree (ADR 0015).
#
#   scripts/deploy.sh [fly deploy args...]
#
# Run `scripts/check.sh all --full` first; any change to the tree afterwards makes the stamp stale.
set -euo pipefail
cd "$(dirname "$0")/.."

# Hash of the working tree (tracked and untracked, not ignored) without touching the real index; same as check.sh.
tree_hash() {
  local index
  index=$(mktemp)
  cp "$(git rev-parse --git-path index)" "$index"
  GIT_INDEX_FILE=$index git add -A >/dev/null 2>&1
  GIT_INDEX_FILE=$index git write-tree
  rm -f "$index"
}

stamp=$(git rev-parse --git-path check-stamp-full)
if [[ ! -f $stamp ]]; then
  echo "deploy: no full-gate stamp; run scripts/check.sh all --full first" >&2
  exit 1
fi
if [[ "$(cat "$stamp")" != "$(tree_hash)" ]]; then
  echo "deploy: the full-gate stamp is stale for this tree; run scripts/check.sh all --full first" >&2
  exit 1
fi

exec fly deploy "$@"
