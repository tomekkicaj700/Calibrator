@echo off
echo === Kopiowanie Kalibrator do lokalizacji sieciowej ===
echo.

REM Sprawdzenie czy PowerShell jest dostępny
powershell -Command "Get-Host" >nul 2>&1
if errorlevel 1 (
    echo Błąd: PowerShell nie jest dostępny.
    pause
    exit /b 1
)

REM Uruchomienie skryptu PowerShell
powershell -ExecutionPolicy Bypass -File "deploy-to-network.ps1" %*

echo.
echo Naciśnij dowolny klawisz, aby zamknąć...
pause >nul 