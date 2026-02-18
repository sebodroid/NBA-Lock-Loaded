FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY NbaTracker.sln .
COPY src/NbaTracker.Api/NbaTracker.Api.csproj ./src/NbaTracker.Api/
COPY src/NbaTracker.Data/NbaTracker.Data.csproj ./src/NbaTracker.Data/
COPY src/NbaTracker.Worker/NbaTracker.Worker.csproj ./src/NbaTracker.Worker/
RUN dotnet restore src/NbaTracker.Worker/NbaTracker.Worker.csproj

COPY src/ ./src/
WORKDIR /source/src/NbaTracker.Worker
RUN dotnet publish -c Release -o /app --no-restore

# Worker uses runtime (not aspnet) — no HTTP server needed
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "NbaTracker.Worker.dll"]
