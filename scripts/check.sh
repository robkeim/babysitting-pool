#!/usr/bin/env bash
# The single gate (ADR 0008): git hooks, Claude Code hooks and CI all call this, so "green" means one thing.
#
#   scripts/check.sh [backend|frontend|all] [--fast] [--e2e] [--stamped]
#
#   --fast     skip the slow tests: backend API and Infrastructure tests (the Domain and Application tests still run)
#   --e2e      also run the Playwright journeys (frontend only; needs `npx playwright install chromium`)
#   --stamped  exit 0 at once if a passing `all` run is already stamped for this exact tree
#
# A passing `all` run writes the tree hash to .git/check-stamp, proving "tested at this exact content".
set -euo pipefail
cd "$(dirname "$0")/.."

scope=all
fast=0
e2e=0
stamped=0
for arg in "$@"; do
  case "$arg" in
    backend | frontend | all) scope=$arg ;;
    --fast) fast=1 ;;
    --e2e) e2e=1 ;;
    --stamped) stamped=1 ;;
    *) echo "usage: scripts/check.sh [backend|frontend|all] [--fast] [--e2e] [--stamped]" >&2; exit 2 ;;
  esac
done

# Hash of the working tree (tracked and untracked, not ignored) without touching the real index.
tree_hash() {
  local index
  index=$(mktemp)
  cp "$(git rev-parse --git-path index)" "$index"
  GIT_INDEX_FILE=$index git add -A >/dev/null 2>&1
  GIT_INDEX_FILE=$index git write-tree
  rm -f "$index"
}
stamp_file=$(git rev-parse --git-path check-stamp)

if [[ $stamped == 1 && -f $stamp_file && "$(cat "$stamp_file")" == "$(tree_hash)" ]]; then
  echo "check: already green for this tree, skipping"
  exit 0
fi

step() { printf '\n\033[1m==> %s\033[0m\n' "$*"; }

backend() {
  cd src/backend
  step "backend: dotnet format"
  dotnet format BabysittingPool.sln --verify-no-changes
  step "backend: build"
  dotnet build BabysittingPool.sln --nologo -v quiet
  step "backend: test"
  if [[ $fast == 1 ]]; then
    dotnet test BabysittingPool.sln --no-build --nologo \
      --filter "FullyQualifiedName!~.Api.&FullyQualifiedName!~.Infrastructure."
  else
    dotnet test BabysittingPool.sln --no-build --nologo
  fi
}

frontend() {
  cd src/frontend
  [[ -d node_modules ]] || { step "frontend: npm ci"; npm ci; }
  step "frontend: eslint"
  npx eslint . --max-warnings 0
  step "frontend: prettier"
  npx prettier --check .
  step "frontend: tsc"
  npx tsc -b
  step "frontend: vitest"
  npx vitest run
  if [[ $e2e == 1 ]]; then
    step "frontend: playwright"
    npx playwright test
  fi
}

step "docs"
scripts/check-docs.sh

case "$scope" in
  backend) (backend) ;;
  frontend) (frontend) ;;
  all) (backend) && (frontend) ;;
esac

if [[ $scope == all ]]; then
  tree_hash >"$stamp_file"
fi
printf '\n\033[1;32mcheck: green (%s%s%s)\033[0m\n' "$scope" "$([[ $fast == 1 ]] && echo ', fast')" "$([[ $e2e == 1 ]] && echo ', e2e')"
