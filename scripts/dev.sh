#!/usr/bin/env bash
# Starts the API (:5120) and the SPA (:5173) together; Ctrl-C stops both.
set -euo pipefail
cd "$(dirname "$0")/.."

trap 'kill 0' EXIT INT TERM

(cd src/backend && dotnet run --project BabysittingPool.Api --launch-profile http) &
(cd src/frontend && npm run dev) &
wait
