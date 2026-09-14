# Prints every distinct (Category, StatName) pair captured in PlayerGameStats,
# so the prop-market keyword mapping in PropMarketMapping.cs can be verified against
# Highlightly's real stat-name vocabulary. Reads the connection string from .env.
$ErrorActionPreference = 'Stop'

$line = Get-Content .env | Select-String '^ConnectionStrings__Default='
if (-not $line) { throw "ConnectionStrings__Default not found in .env" }
$cs = $line.ToString() -replace '^ConnectionStrings__Default=', ''

$kv = @{}
foreach ($p in $cs -split ';') {
    if ($p -match '=') { $k, $v = $p -split '=', 2; $kv[$k.Trim()] = $v.Trim() }
}

$env:PGPASSWORD = $kv['Password']
& psql "host=$($kv['Host']) port=$($kv['Port']) dbname=$($kv['Database']) user=$($kv['Username']) sslmode=require" `
    -c 'SELECT "Category", "StatName", COUNT(*) AS rows FROM "PlayerGameStats" GROUP BY 1,2 ORDER BY 1,2;'
