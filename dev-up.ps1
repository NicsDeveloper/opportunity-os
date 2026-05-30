# Sobe o stack completo (Postgres + API + Worker + Frontend) em janelas separadas.
# Uso: powershell -ExecutionPolicy Bypass -File dev-up.ps1
$root = $PSScriptRoot
Write-Host "[1/5] PostgreSQL (Docker)..."
docker compose up -d postgres
Write-Host "[2/5] Compilando a solucao (uma vez)..."
dotnet build OpportunityOS.slnx -c Debug
if ($LASTEXITCODE -ne 0) { Write-Host "Build falhou. Feche apps usando os DLLs e tente de novo."; exit 1 }
Write-Host "[3/5] API (http://localhost:5077)..."
Start-Process cmd "/k dotnet run --project src/OpportunityOS.Api --no-build" -WorkingDirectory $root
Write-Host "[4/5] Worker (Hangfire)..."
Start-Process cmd "/k dotnet run --project src/OpportunityOS.Worker --no-build" -WorkingDirectory $root
Write-Host "[5/5] Frontend (http://localhost:5173)..."
Start-Process cmd "/k npm install && npm run dev" -WorkingDirectory (Join-Path $root "frontend")
Write-Host "`nAbra: http://localhost:5173"
