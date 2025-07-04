@echo off
REM Skrypt do kopiowania plików Kalibrator do lokalizacji sieciowej
REM Uruchom: deploy-to-network.bat

echo === Deployment Kalibrator do lokalizacji sieciowych ===
echo.

REM Sprawdź czy PowerShell jest dostępny
powershell -Command "Get-Host" >nul 2>&1
if %errorlevel% neq 0 (
    echo Błąd: PowerShell nie jest dostępny na tym systemie.
    echo Upewnij się, że PowerShell jest zainstalowany i dostępny.
    pause
    exit /b 1
)

REM Sprawdź czy aplikacja jest uruchomiona w lokalizacjach docelowych
echo === Sprawdzanie uruchomionych aplikacji ===

REM Lokalizacje docelowe (można zmienić według potrzeb)
set LOCATION1=\\DiskStation\Public\Kalibrator
set LOCATION2=\\KALIBRATOR\CalibratorPublic\Kalibrator

REM Sprawdź pierwszą lokalizację
if exist "%LOCATION1%\running.txt" (
    echo Wykryto uruchomioną aplikację w %LOCATION1%
    echo Wysyłam żądanie zamknięcia...
    
    REM Utwórz plik żądania zamknięcia
    echo %date% %time% > "%LOCATION1%\request-to-close.txt"
    
    REM Czekaj na zamknięcie aplikacji (maksymalnie 10 sekund)
    set /a timeout=100
    set /a elapsed=0
    :wait_loop1
    if exist "%LOCATION1%\running.txt" (
        if %elapsed% geq %timeout% (
            echo Timeout podczas oczekiwania na zamknięcie aplikacji w %LOCATION1%
            goto :continue1
        )
        timeout /t 1 /nobreak >nul
        set /a elapsed+=10
        goto :wait_loop1
    ) else (
        echo Aplikacja została zamknięta pomyślnie w %LOCATION1%
    )
    
    REM Usuń plik żądania zamknięcia
    if exist "%LOCATION1%\request-to-close.txt" del "%LOCATION1%\request-to-close.txt"
    :continue1
)

REM Sprawdź drugą lokalizację
if exist "%LOCATION2%\running.txt" (
    echo Wykryto uruchomioną aplikację w %LOCATION2%
    echo Wysyłam żądanie zamknięcia...
    
    REM Utwórz plik żądania zamknięcia
    echo %date% %time% > "%LOCATION2%\request-to-close.txt"
    
    REM Czekaj na zamknięcie aplikacji (maksymalnie 10 sekund)
    set /a timeout=100
    set /a elapsed=0
    :wait_loop2
    if exist "%LOCATION2%\running.txt" (
        if %elapsed% geq %timeout% (
            echo Timeout podczas oczekiwania na zamknięcie aplikacji w %LOCATION2%
            goto :continue2
        )
        timeout /t 1 /nobreak >nul
        set /a elapsed+=10
        goto :wait_loop2
    ) else (
        echo Aplikacja została zamknięta pomyślnie w %LOCATION2%
    )
    
    REM Usuń plik żądania zamknięcia
    if exist "%LOCATION2%\request-to-close.txt" del "%LOCATION2%\request-to-close.txt"
    :continue2
)

echo === Uruchamiam skrypt PowerShell ===
echo.

REM Wywołaj skrypt PowerShell
powershell -ExecutionPolicy Bypass -File "deploy-to-network.ps1" %*

REM Sprawdź wynik
if %errorlevel% equ 0 (
    echo.
    echo === Deployment zakończony sukcesem! ===
) else (
    echo.
    echo === Deployment zakończony z błędami! ===
    echo Kod błędu: %errorlevel%
)

echo.
pause 