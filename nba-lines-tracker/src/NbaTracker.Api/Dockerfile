FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Copy solution and all .csproj files first — maximizes layer cache for dotnet restore
COPY NbaTracker.sln .
COPY src/NbaTracker.Api/NbaTracker.Api.csproj ./src/NbaTracker.Api/
COPY src/NbaTracker.Data/NbaTracker.Data.csproj ./src/NbaTracker.Data/
COPY src/NbaTracker.Worker/NbaTracker.Worker.csproj ./src/NbaTracker.Worker/
RUN dotnet restore src/NbaTracker.Api/NbaTracker.Api.csproj

# Copy all source and publish
COPY src/ ./src/
WORKDIR /source/src/NbaTracker.Api
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "NbaTracker.Api.dll"]
