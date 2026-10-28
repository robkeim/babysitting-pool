#!/usr/bin/env bash
# One off-host snapshot of the main database (ADR 0018). The rate-limit file needs no backup (ADR 0014).
#
#   scripts/backup.sh
#
# Takes a consistent copy with sqlite3 `.backup` (safe while the API is writing), checks it, gzips it, uploads it
# with rclone as babysitting-<UTC timestamp>.db.gz and keeps the newest BACKUP_KEEP snapshots.
#
# Environment:
#   BACKUP_REMOTE  rclone destination, e.g. offhost:coop-backups (required). The remote itself is configured with
#                  RCLONE_CONFIG_<REMOTE>_* variables; see docs/development.md.
#   BACKUP_DB      database file (default: the Data Source in ConnectionStrings__Default, else /data/babysitting.db)
#   BACKUP_KEEP    snapshots to keep (default 30)
set -euo pipefail

remote=${BACKUP_REMOTE:?BACKUP_REMOTE is not set}
keep=${BACKUP_KEEP:-30}
db=${BACKUP_DB:-}
if [[ -z $db ]]; then
  conn=${ConnectionStrings__Default:-}
  db=${conn#*Data Source=}
  db=${db%%;*}
  [[ -n $conn && -n $db ]] || db=/data/babysitting.db
fi
[[ -f $db ]] || { echo "backup: $db does not exist" >&2; exit 1; }

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
name=babysitting-$(date -u +%Y%m%dT%H%M%SZ).db

sqlite3 "$db" ".timeout 10000" ".backup '$work/$name'"
check=$(sqlite3 "$work/$name" "PRAGMA integrity_check;")
[[ $check == ok ]] || { echo "backup: snapshot failed integrity_check: $check" >&2; exit 1; }
gzip "$work/$name"

rclone copyto "$work/$name.gz" "$remote/$name.gz"
echo "backup: uploaded $name.gz to $remote"

# Names sort by timestamp, so everything before the newest $keep is old.
stale=$(rclone lsf --files-only "$remote" | grep -E '^babysitting-[0-9]{8}T[0-9]{6}Z\.db\.gz$' | sort | head -n "-$keep" || true)
while IFS= read -r old; do
  [[ -n $old ]] || continue
  rclone deletefile "$remote/$old"
  echo "backup: pruned $old"
done <<<"$stale"
