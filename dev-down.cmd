@echo off
REM Derruba o stack do Opportunity OS.
setlocal
cd /d "%~dp0"

echo Fechando API / Worker / Frontend...
taskkill /FI "WINDOWTITLE eq OpportunityOS API*"    /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq OpportunityOS Worker*" /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq OpportunityOS Web*"    /T /F >nul 2>&1

echo Parando PostgreSQL...
docker compose stop postgres

echo Pronto.
endlocal
