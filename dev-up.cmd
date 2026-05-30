@echo off
REM Sobe o stack completo do Opportunity OS (Postgres + API + Worker + Frontend).
REM Uso: dê duplo-clique ou rode "dev-up" na raiz do repo.
setlocal
cd /d "%~dp0"

echo [1/4] Subindo PostgreSQL (Docker)...
docker compose up -d postgres

echo [2/4] Iniciando API (http://localhost:5077)...
start "OpportunityOS API" cmd /k dotnet run --project src/OpportunityOS.Api

echo [3/4] Iniciando Worker (Hangfire)...
start "OpportunityOS Worker" cmd /k dotnet run --project src/OpportunityOS.Worker

echo [4/4] Iniciando Frontend (http://localhost:5173)...
start "OpportunityOS Web" cmd /k "cd /d %~dp0frontend && npm install && npm run dev"

echo.
echo Tudo subindo em janelas separadas. Abra: http://localhost:5173
echo (A primeira vez instala dependencias do frontend e pode demorar.)
endlocal
