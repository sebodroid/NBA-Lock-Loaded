#!/usr/bin/env bash
# macOS/Linux equivalent of start-all.ps1. Starts the Worker and API in the background
# (logged to files), then runs the frontend dev server in this terminal.
# Run from the repo root: bash scripts/start-all.sh
#
# The Worker is a long-running BackgroundService — once started it re-syncs itself every
# day at 7 AM ET on its own (see NflWorker.RunScheduleLoopAsync), so on most days you do
# NOT need to restart it manually; leaving it running is enough. Only restart it when
# you've pulled/changed Worker code, or want an immediate sync instead of waiting for 7 AM.
set -e
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG_DIR="$ROOT/.run-logs"
mkdir -p "$LOG_DIR"

echo "Starting Worker (log: .run-logs/worker.log)..."
(cd "$ROOT" && dotnet run --project src/NbaTracker.Worker > "$LOG_DIR/worker.log" 2>&1 &)

echo "Starting API (log: .run-logs/api.log)..."
(cd "$ROOT" && dotnet run --project src/NbaTracker.Api > "$LOG_DIR/api.log" 2>&1 &)

echo
echo "Worker and API are starting in the background."
echo "  Tail logs:  tail -f .run-logs/worker.log   /   tail -f .run-logs/api.log"
echo "  Stop them:  pkill -f NbaTracker.Worker; pkill -f NbaTracker.Api"
echo

cd "$ROOT/frontend"
npm run dev
