#!/usr/bin/env bash
# Restore drill (ADR 0018): restore the latest off-host snapshot into a scratch database and check that it opens
# and the Ledger triggers exist. Run it before launch and then quarterly (docs/development.md).
#
#   scripts/restore-drill.sh
#
# Needs the same BACKUP_REMOTE and RCLONE_CONFIG_<REMOTE>_* variables as scripts/backup.sh, so on Fly run it with
# `fly ssh console -C /app/scripts/restore-drill.sh`. It only reads from the remote and writes to a temp directory.
set -euo pipefail

remote=${BACKUP_REMOTE:?BACKUP_REMOTE is not set}
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

latest=$(rclone lsf --files-only "$remote" | grep -E '^babysitting-[0-9]{8}T[0-9]{6}Z\.db\.gz$' | sort | tail -n 1 || true)
[[ -n $latest ]] || { echo "drill: FAILED, no snapshots in $remote" >&2; exit 1; }
echo "drill: restoring $latest"

rclone copyto "$remote/$latest" "$work/$latest"
gunzip "$work/$latest"
scratch=$work/${latest%.gz}

fail=0
check=$(sqlite3 "$scratch" "PRAGMA integrity_check;") || check="cannot open"
if [[ $check == ok ]]; then echo "drill: ok  opens and passes integrity_check"; else echo "drill: FAILED integrity_check: $check" >&2; fail=1; fi

for trigger in LedgerEntries_no_update LedgerEntries_no_delete; do
  found=$(sqlite3 "$scratch" "SELECT count(*) FROM sqlite_master WHERE type = 'trigger' AND name = '$trigger';") || found=0
  if [[ $found == 1 ]]; then echo "drill: ok  trigger $trigger exists"; else echo "drill: FAILED trigger $trigger is missing" >&2; fail=1; fi
done

((fail == 0)) || exit 1
entries=$(sqlite3 "$scratch" "SELECT count(*) FROM LedgerEntries;")
echo "drill: PASSED ($latest, $entries Ledger entries)"
