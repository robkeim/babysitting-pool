#!/usr/bin/env bash
# The single gate (ADR 0008): git hooks, Claude Code hooks and CI all call this, so "green" means one thing.
#
#   scripts/check.sh [backend|frontend|all] [--fast] [--e2e] [--full] [--stamped] [--quiet]
#
# Two levels (ADR 0015): the fast gate is for iterating (Stop hook), the full gate runs before anything is pushed.
#   --fast     skip the slow tests: backend API and Infrastructure tests (the Domain and Application tests still run)
#              and the slow scans (git history, Semgrep, dependency audits)
#   --e2e      also run the Playwright journeys (frontend only; needs `npx playwright install chromium`)
#   --full     the full gate: every test, the Playwright journeys, the generated-contract drift check and the scans
#   --stamped  exit 0 at once if a passing `all` run is already stamped for this exact tree and level
#   --quiet    print one line when green, or the last 40 lines and the log path when red (keeps agent output small)
#
# A passing `all` run writes the tree hash to .git/check-stamp-fast, proving "tested at this exact content";
# a passing --full run also writes .git/check-stamp-full. A full stamp satisfies a fast request, not the reverse.
set -euo pipefail
cd "$(dirname "$0")/.."

scope=all
fast=0
e2e=0
full=0
stamped=0
quiet=0
forward=()
for arg in "$@"; do
  case "$arg" in
    backend | frontend | all) scope=$arg ;;
    --fast) fast=1 ;;
    --e2e) e2e=1 ;;
    --full) full=1 ;;
    --stamped) stamped=1 ;;
    --quiet) quiet=1; continue ;;
    *) echo "usage: scripts/check.sh [backend|frontend|all] [--fast] [--e2e] [--full] [--stamped] [--quiet]" >&2; exit 2 ;;
  esac
  forward+=("$arg")
done
if [[ $full == 1 ]]; then
  fast=0
  e2e=1
fi

if [[ $quiet == 1 ]]; then
  log=$(git rev-parse --git-path check.log)
  if "$0" "${forward[@]+"${forward[@]}"}" >"$log" 2>&1; then
    tail -1 "$log"
    exit 0
  fi
  tail -40 "$log"
  echo "check: red; full log in $log"
  exit 1
fi

# Hash of the working tree (tracked and untracked, not ignored) without touching the real index.
tree_hash() {
  local index
  index=$(mktemp)
  cp "$(git rev-parse --git-path index)" "$index"
  GIT_INDEX_FILE=$index git add -A >/dev/null 2>&1
  GIT_INDEX_FILE=$index git write-tree
  rm -f "$index"
}
stamp_fast=$(git rev-parse --git-path check-stamp-fast)
stamp_full=$(git rev-parse --git-path check-stamp-full)
# A full stamp satisfies any request; a fast stamp satisfies only a fast one.
wanted_stamps=("$stamp_full")
[[ $full == 1 ]] || wanted_stamps+=("$stamp_fast")

if [[ $stamped == 1 ]]; then
  current=$(tree_hash)
  for stamp in "${wanted_stamps[@]}"; do
    if [[ -f $stamp && "$(cat "$stamp")" == "$current" ]]; then
      echo "check: already green for this tree, skipping"
      exit 0
    fi
  done
fi

step() { printf '\n\033[1m==> %s\033[0m\n' "$*"; }

need() {
  command -v "$1" >/dev/null || { echo "check: $1 is not installed; run: $2" >&2; return 1; }
}

# Local security scanning (ADR 0014): GitHub's secret scanning and CodeQL need a paid plan on a private repo.
scan_secrets() {
  need gitleaks "brew install gitleaks"
  step "secrets: gitleaks (working tree)"
  gitleaks dir . --config .gitleaks.toml --no-banner --redact
  if [[ $fast == 0 ]]; then
    step "secrets: gitleaks (git history)"
    gitleaks git . --config .gitleaks.toml --no-banner --redact
  fi
}

scan_code() {
  need semgrep "brew install semgrep"
  step "code: semgrep"
  semgrep scan --config .semgrep --error --quiet --metrics off --disable-version-check \
    --exclude node_modules --exclude bin --exclude obj --exclude dist --exclude '*.gen.ts' \
    --exclude Migrations --exclude RateLimitMigrations --exclude BabysittingPool.Tests src
  step "dependencies: dotnet vulnerable packages"
  local out
  out=$(dotnet list src/backend/BabysittingPool.sln package --vulnerable --include-transitive 2>&1)
  echo "$out"
  if grep -q "has the following vulnerable packages" <<<"$out"; then return 1; fi
  step "dependencies: npm audit"
  (cd src/frontend && npm audit --audit-level=high)
}

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
    npx playwright test --reporter=line
  fi
  if [[ $full == 1 ]]; then
    step "frontend: Postman collection is up to date"
    local collection=../../docs/api/postman_collection.json before
    before=$(mktemp)
    cp "$collection" "$before"
    npm run generate:postman --silent
    if ! cmp -s "$before" "$collection"; then
      rm -f "$before"
      echo "$collection was stale and has been regenerated; commit it" >&2
      return 1
    fi
    rm -f "$before"
  fi
}

step "docs"
scripts/check-docs.sh

scan_secrets
[[ $fast == 1 ]] || scan_code

case "$scope" in
  backend) (backend) ;;
  frontend) (frontend) ;;
  # Not `(backend) && (frontend)`: in an && list `set -e` is off inside the subshell, so a failure there
  # was swallowed and the run stamped green.
  all) (backend); (frontend) ;;
esac

if [[ $scope == all ]]; then
  tree_hash >"$stamp_fast"
  [[ $full == 1 ]] && tree_hash >"$stamp_full"
fi
printf '\n\033[1;32mcheck: green (%s%s%s%s)\033[0m\n' "$scope" "$([[ $fast == 1 ]] && echo ', fast')" "$([[ $e2e == 1 ]] && echo ', e2e')" "$([[ $full == 1 ]] && echo ', full')"
