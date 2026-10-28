#!/usr/bin/env bash
# Runs scripts/backup.sh once a day at BACKUP_HOUR_UTC (default 03) inside the container (ADR 0018).
# A failed run is logged and retried an hour later; it never stops the API.
set -uo pipefail

hour=$((10#${BACKUP_HOUR_UTC:-03}))
backup=$(dirname "$0")/backup.sh

while true; do
  now=$(date -u +%s)
  next=$(date -u -d "today $(printf '%02d' "$hour"):00" +%s)
  ((next > now)) || next=$(date -u -d "tomorrow $(printf '%02d' "$hour"):00" +%s)
  sleep $((next - now))
  until "$backup"; do
    echo "backup: failed, retrying in an hour" >&2
    sleep 3600
  done
done
