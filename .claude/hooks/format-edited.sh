#!/usr/bin/env bash
# PostToolUse on Edit|Write: format the edited file so style never reaches the gate (ADR 0008).
file=$(jq -r '.tool_input.file_path // ""')
[[ -f $file ]] || exit 0
cd "$CLAUDE_PROJECT_DIR" || exit 0
case "$file" in
  *src/frontend/src/components/ui/*) ;;
  *src/frontend/*.ts | *src/frontend/*.tsx | *src/frontend/*.js | *src/frontend/*.mjs | *src/frontend/*.css | *src/frontend/*.json)
    (cd src/frontend && npx --no-install prettier --write "$file" >/dev/null 2>&1) ;;
  *.cs)
    dotnet format src/backend/BabysittingPool.sln --include "$file" >/dev/null 2>&1 ;;
esac
exit 0
