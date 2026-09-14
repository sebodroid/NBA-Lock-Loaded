#!/usr/bin/env bash
# macOS/Linux equivalent of dump-stat-names.ps1. Prints every distinct
# (Category, StatName) pair captured in PlayerGameStats, so the prop-market keyword
# mapping in PropMarketMapping.cs can be verified against Highlightly's real stat-name
# vocabulary. Reads the connection string from .env. Requires the psql client
# (macOS: `brew install libpq` and add it to PATH, or `brew install postgresql`).
set -e
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="$ROOT/.env"

CS=$(grep '^ConnectionStrings__Default=' "$ENV_FILE" | cut -d'=' -f2-)
if [ -z "$CS" ]; then
  echo "ConnectionStrings__Default not found in .env" >&2
  exit 1
fi

field() { echo "$CS" | tr ';' '\n' | grep "^$1=" | cut -d'=' -f2-; }

HOST=$(field Host)
PORT=$(field Port)
DB=$(field Database)
DBUSER=$(field Username)
PASS=$(field Password)

PGPASSWORD="$PASS" psql "host=$HOST port=$PORT dbname=$DB user=$DBUSER sslmode=require" \
  -c 'SELECT "Category", "StatName", COUNT(*) AS rows FROM "PlayerGameStats" GROUP BY 1,2 ORDER BY 1,2;'
