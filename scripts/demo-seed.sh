#!/usr/bin/env bash
# Brings a running local stack to the state docs/demo-script.md starts from (see docs/development.md).
#
#   scripts/demo-seed.sh      needs the API from scripts/dev.sh, started with Settle__Enabled=false
#
# The Development seed (ADR 0011) already creates the Admin, Ryan, Sarah and Oscar. This adds two Requests from Oscar:
# one Posted for the Admin to cancel when offboarding (Scenario 9) and one Claimed by Sarah whose start has already
# passed, so Run settle pays out (Scenario 7). Requests cannot be posted in the past, so the second one is posted
# through the API and then moved back in time in the local SQLite file. Safe to run again: it does nothing when the
# Requests are there. Refuses anything that is not a local Development API.
set -euo pipefail
cd "$(dirname "$0")/.."

api_url=${DEMO_API_URL:-http://localhost:5120}
db=${DEMO_DB:-src/backend/BabysittingPool.Api/babysitting.db}

# Accepts only http(s)://localhost, 127.0.0.1 or [::1], with an optional port. Userinfo and look-alike hosts do not match.
[[ $api_url =~ ^https?://(localhost|127\.0\.0\.1|\[::1\])(:[0-9]{1,5})?$ ]] \
  || { echo "demo-seed: refusing non-local API '$api_url'" >&2; exit 1; }
[[ ${ASPNETCORE_ENVIRONMENT:-Development} == Development ]] \
  || { echo "demo-seed: refusing ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT" >&2; exit 1; }
for tool in curl python3 sqlite3; do
  command -v "$tool" >/dev/null || { echo "demo-seed: $tool is required" >&2; exit 1; }
done
[[ -f $db ]] || { echo "demo-seed: no database at $db; start scripts/dev.sh first (DEMO_DB overrides the path)" >&2; exit 1; }

jar=$(mktemp -d "${TMPDIR:-/tmp}/demo-seed.XXXXXX")
trap 'rm -rf "$jar"' EXIT

api() { # method path [json-body] [cookie-jar]
  local args=(-fsS -X "$1" "$api_url$2" -H 'Content-Type: application/json' -H 'X-Requested-With: fetch')
  [[ -n ${3:-} ]] && args+=(-d "$3")
  [[ -n ${4:-} ]] && args+=(-b "$jar/$4" -c "$jar/$4")
  curl "${args[@]}"
}

# Production has no outbox endpoint, so this also proves the target is a Development API.
curl -fs -o /dev/null "$api_url/api/dev/outbox" \
  || { echo "demo-seed: $api_url is not a running Development API (no /api/dev/outbox)" >&2; exit 1; }

# Mail goes to plus-addresses of the SMTP base address when it is set (docs/development.md), else to example.test.
member_email() { # tag
  sqlite3 "$db" "select Email from Members where ClosedAt is null and DisplayName like '${1}%' limit 1"
}
oscar=$(member_email Oscar)
sarah=$(member_email Sarah)
[[ -n $oscar && -n $sarah ]] || { echo "demo-seed: Oscar and Sarah are not in $db; delete it and restart the API to reseed" >&2; exit 1; }

sign_in() { # email jar-name
  api POST /api/auth/sign-in-links "{\"email\":\"$1\"}" >/dev/null
  local link token
  link=$(api GET "/api/dev/outbox?to=$1" | python3 -c 'import json, sys; print(json.load(sys.stdin)[-1]["link"])')
  token=$(python3 -c 'import sys, urllib.parse as u; print(u.parse_qs(u.urlparse(sys.argv[1]).query).get("token", [u.urlparse(sys.argv[1]).fragment])[0])' "$link")
  api POST /api/auth/sessions "{\"token\":\"$token\"}" "$2" >/dev/null
}

oscar_id=$(sqlite3 "$db" "select lower(Id) from Members where Email = '$oscar' and ClosedAt is null")
sitter_id=$(sqlite3 "$db" "select lower(Id) from Members where Email = '$sarah' and ClosedAt is null")
settled_ready=$(sqlite3 "$db" "select count(*) from Requests where lower(RequesterId) = '$oscar_id' and lower(SitterId) = '$sitter_id' and Status = 'Claimed' and StartsAt <= datetime('now')")
if ((settled_ready > 0)); then
  echo "demo-seed: already seeded ($settled_ready claimed Request past its start)"
  exit 0
fi

sign_in "$oscar" oscar
sign_in "$sarah" sarah

post() { # start-offset-hours
  local starts
  starts=$(python3 -c 'import datetime, sys; t = datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(hours=float(sys.argv[1])); print(t.replace(minute=t.minute // 5 * 5, second=0, microsecond=0).strftime("%Y-%m-%dT%H:%M:%SZ"))' "$1")
  api POST /api/requests '{"startsAt":"'"$starts"'","durationMinutes":120,"childCount":2,"location":"SittersHouse","transport":"None","lunch":false,"snack":true,"dinner":false,"notes":"Demo seed"}' oscar \
    | python3 -c 'import json, sys; print(json.load(sys.stdin)["id"])'
}

post 48 >/dev/null
past_id=$(post 8)
api POST "/api/requests/$past_id/claim" "" sarah >/dev/null

# Move the claimed Request into the past (and its claim cutoff, which must precede the start). Status stays Claimed, so
# the next Settle pays Sarah out. Keep the SQLite text format EF Core writes.
sqlite3 "$db" "update Requests set StartsAt = strftime('%Y-%m-%d %H:%M:%f', 'now', '-3 hours'), ClaimCutoffAt = strftime('%Y-%m-%d %H:%M:%f', 'now', '-6 hours') where lower(Id) = '$past_id'"

echo "demo-seed: Oscar has a Posted Request and a Claimed one (by Sarah) that started 3 hours ago."
echo "demo-seed: Run settle now, before the API's own minute tick does, unless it runs with Settle__Enabled=false."
