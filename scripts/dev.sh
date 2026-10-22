#!/usr/bin/env bash
# Starts the API (:5120) and the SPA (:5173) together; Ctrl-C stops both.
set -euo pipefail
cd "$(dirname "$0")/.."

trap 'kill 0' EXIT INT TERM

dotnet run --project src/backend/BabysittingPool.Api --launch-profile http &
(cd src/frontend && npm run dev) &
wait
