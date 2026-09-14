# Starts the Worker and API in their own windows, then runs the frontend dev server in
# this one. Run from the repo root: powershell -File scripts/start-all.ps1
#
# The Worker is a long-running BackgroundService — once started it re-syncs itself every
# day at 7 AM ET on its own (see NflWorker.RunScheduleLoopAsync), so on most days you do
# NOT need to restart it manually; leaving the window open is enough. Only restart it when
# you've pulled/changed Worker code, or want an immediate sync instead of waiting for 7 AM.
$root = Split-Path -Parent $PSScriptRoot

Start-Process powershell -WorkingDirectory $root -ArgumentList `
    '-NoExit', '-Command', 'dotnet run --project src/NbaTracker.Worker'

Start-Process powershell -WorkingDirectory $root -ArgumentList `
    '-NoExit', '-Command', 'dotnet run --project src/NbaTracker.Api'

Set-Location (Join-Path $root 'frontend')
npm run dev
