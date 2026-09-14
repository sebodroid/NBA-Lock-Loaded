Terminal 1 — Worker (background sync: NBA/MLB/NFL games, scores, odds, player stats)
dotnet run --project src/NbaTracker.Worker

Terminal 2 — API (serves data to the frontend, port pinned to match the frontend's dev proxy)
dotnet run --project src/NbaTracker.Api --urls http://localhost:5000

Terminal 3 — Frontend (the web UI)
cd frontend
npm run dev