@echo off
setlocal

cd /d "%~dp0"

set "APP_URL=http://localhost:5034"
set "BIND_URL=http://0.0.0.0:5034"
set "ASPNETCORE_ENVIRONMENT=Development"
set "DOTNET_ENVIRONMENT=Development"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERRORE: .NET SDK non trovato nel PATH.
    echo Installa .NET SDK oppure apri il progetto da un terminale dove dotnet e disponibile.
    pause
    exit /b 1
)

echo.
echo Avvio Gestione Prenotazioni in locale...
echo URL locale: %APP_URL%
echo Ascolto rete: %BIND_URL%
echo Ambiente: %ASPNETCORE_ENVIRONMENT%
echo Da altri dispositivi usa: http://IP_DEL_PC:5034
echo.
echo Per fermare l'applicazione premi CTRL+C in questa finestra.
echo.

start "" powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Sleep -Seconds 4; Start-Process '%APP_URL%'"

dotnet run --project "GestionePrenotazioni.Web\GestionePrenotazioni.Web.csproj" --no-launch-profile --urls "%BIND_URL%"

echo.
echo Applicazione terminata.
pause
