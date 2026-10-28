#!/usr/bin/env bash
# Container entrypoint (ADR 0018): start the daily backup loop when BACKUP_REMOTE is set, then become the API.
set -euo pipefail

if [[ -n ${BACKUP_REMOTE:-} ]]; then
  /app/scripts/backup-daily.sh &
else
  echo "entrypoint: BACKUP_REMOTE is not set, so no off-host backups will run" >&2
fi
exec dotnet BabysittingPool.Api.dll
