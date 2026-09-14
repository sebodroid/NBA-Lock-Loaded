Terminal 1 — Worker (background sync: NBA/MLB/NFL games, scores, odds, player stats)
dotnet run --project src/NbaTracker.Worker

Terminal 2 — API (serves data to the frontend, port pinned to match the frontend's dev proxy)
dotnet run --project src/NbaTracker.Api --urls http://localhost:5000

Terminal 3 — Frontend (the web UI)
cd frontend
npm run dev


Runnnig on MAC
What you will need to redo on the Mac, since none of it lives in git:

Install the .NET 9 SDK and Node — standard installs, nothing project-specific.
Recreate .env by hand — it's gitignored on purpose (it holds your DB connection string, Odds API key, Highlightly key, JWT secret). .env.example in the repo root lists which keys it needs; copy the real values over yourself (not through git — email/AirDrop/password manager, whatever you're comfortable with).
The two .ps1 helper scripts (start-all.ps1, dump-stat-names.ps1) are Windows-only. Since you mentioned the Mac, I went ahead and wrote .sh equivalents (scripts/start-all.sh, scripts/dump-stat-names.sh) so you're not stuck rewriting them later — same behavior, just bash. dump-stat-names.sh needs psql on the Mac too (brew install libpq, or brew install postgresql).
