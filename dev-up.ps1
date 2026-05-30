# Sobe o stack completo (Postgres + API + Worker + Frontend) em janelas separadas.
# Uso: powershell -ExecutionPolicy Bypass -File dev-up.ps1
$root = $PSScriptRoot
Write-Host "[1/4] PostgreSQL (Docker)..."
docker compose up -d postgres
Write-Host "[2/4] API (http://localhost:5077)..."
Start-Process cmd "/k dotnet run --project src/OpportunityOS.Api" -WorkingDirectory $root
Write-Host "[3/4] Worker (Hangfire)..."
Start-Process cmd "/k dotnet run --project src/OpportunityOS.Worker" -WorkingDirectory $root
Write-Host "[4/4] Frontend (http://localhost:5173)..."
Start-Process cmd "/k npm install && npm run dev" -WorkingDirectory (Join-Path $root "frontend")
Write-Host "`nAbra: http://localhost:5173"
