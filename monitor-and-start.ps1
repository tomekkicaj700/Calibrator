# Skrypt monitorujący dla komputera KALIBRATOR
# Uruchamia aplikację Calibrator po wykryciu zmian w folderze

param(
    [string]$AppFolder = "C:\CalibratorPublic\Kalibrator",
    [int]$CheckInterval = 30  # Sprawdzaj co 30 sekund
)

Write-Output "[monitor-and-start.ps1] Rozpoczynam monitorowanie folderu: $AppFolder"
Write-Output "[monitor-and-start.ps1] Interwał sprawdzania: $CheckInterval sekund"

# Sprawdź czy folder aplikacji istnieje
if (-not (Test-Path $AppFolder)) {
    Write-Output "[monitor-and-start.ps1] BŁĄD: Folder aplikacji nie istnieje: $AppFolder"
    exit 1
}

$exePath = Join-Path $AppFolder "Calibrator.exe"
if (-not (Test-Path $exePath)) {
    Write-Output "[monitor-and-start.ps1] BŁĄD: Nie znaleziono Calibrator.exe w: $exePath"
    exit 1
}

Write-Output "[monitor-and-start.ps1] Znaleziono aplikację: $exePath"
Write-Output "[monitor-and-start.ps1] Rozpoczynam monitorowanie..."

# Pętla monitorowania
while ($true) {
    try {
        # Sprawdź czy aplikacja jest uruchomiona
        $runningProcesses = Get-Process -Name "Calibrator" -ErrorAction SilentlyContinue
        
        if ($runningProcesses.Count -eq 0) {
            Write-Output "[monitor-and-start.ps1] Aplikacja nie jest uruchomiona. Uruchamiam..."
            
            # Sprawdź czy plik running.txt istnieje (oznacza, że aplikacja powinna być uruchomiona)
            $runningFile = Join-Path $AppFolder "running.txt"
            if (Test-Path $runningFile) {
                Write-Output "[monitor-and-start.ps1] Wykryto running.txt - aplikacja powinna być uruchomiona"
                
                # Sprawdź czy nie ma request-to-close.txt
                $requestCloseFile = Join-Path $AppFolder "request-to-close.txt"
                if (Test-Path $requestCloseFile) {
                    Write-Output "[monitor-and-start.ps1] Wykryto request-to-close.txt - nie uruchamiam aplikacji"
                    Start-Sleep -Seconds $CheckInterval
                    continue
                }
                
                # Uruchom aplikację
                Start-Process -FilePath $exePath
                Write-Output "[monitor-and-start.ps1] Aplikacja została uruchomiona"
            } else {
                Write-Output "[monitor-and-start.ps1] Brak running.txt - aplikacja nie powinna być uruchomiona"
            }
        } else {
            Write-Output "[monitor-and-start.ps1] Aplikacja jest uruchomiona (PID: $($runningProcesses[0].Id))"
        }
        
        # Sprawdź czy aplikacja powinna zostać zamknięta
        $requestCloseFile = Join-Path $AppFolder "request-to-close.txt"
        if (Test-Path $requestCloseFile) {
            Write-Output "[monitor-and-start.ps1] Wykryto request-to-close.txt - zamykam aplikację..."
            
            foreach ($process in $runningProcesses) {
                Write-Output "[monitor-and-start.ps1] Zamykam proces Calibrator (PID: $($process.Id))"
                $process.Kill()
            }
            
            # Usuń pliki kontrolne
            if (Test-Path $runningFile) {
                Remove-Item $runningFile -Force
                Write-Output "[monitor-and-start.ps1] Usunięto running.txt"
            }
            if (Test-Path $requestCloseFile) {
                Remove-Item $requestCloseFile -Force
                Write-Output "[monitor-and-start.ps1] Usunięto request-to-close.txt"
            }
        }
        
    } catch {
        Write-Output "[monitor-and-start.ps1] Błąd podczas monitorowania: $($_.Exception.Message)"
    }
    
    # Czekaj przed kolejnym sprawdzeniem
    Start-Sleep -Seconds $CheckInterval
} 