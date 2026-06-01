@echo off
REM Sobe o stack completo do Opportunity OS (Postgres + API + Worker + Frontend).
REM Uso: dê duplo-clique, ou rode ".\dev-up.cmd" (PowerShell) / "dev-up" (cmd.exe).
setlocal
cd /d "%~dp0"

echo [1/5] Subindo PostgreSQL (Docker)...
docker compose up -d postgres

echo [2/5] Compilando a solucao (uma vez, para os runs nao colidirem)...
dotnet build OpportunityOS.slnx -c Debug
if errorlevel 1 (
  echo.
  echo Build falhou. Feche apps que estejam usando os DLLs e tente de novo.
  pause
  exit /b 1
)

echo [3/5] Iniciando API (http://localhost:5077)...
start "OpportunityOS API" cmd /k dotnet run --project src/OpportunityOS.Api --no-build

echo [4/5] Iniciando Worker (Hangfire)...
start "OpportunityOS Worker" cmd /k dotnet run --project src/OpportunityOS.Worker --no-build

echo [5/5] Iniciando Frontend (http://localhost:5173)...
start "OpportunityOS Web" cmd /k "cd /d %~dp0frontend && npm install && npm run dev"

echo.
echo Tudo subindo em janelas separadas. Abra: http://localhost:5173
endlocal
