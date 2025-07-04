# Skrypt do uruchamiania monitora na komputerze KALIBRATOR
# Uruchamia monitor-and-start.ps1 w tle

Write-Output "=== URUCHAMIANIE MONITORA KALIBRATOR ==="
Write-Output "Ten skrypt uruchamia monitor aplikacji Calibrator na komputerze KALIBRATOR"
Write-Output ""

# Sprawdź czy monitor-and-start.ps1 istnieje
$monitorScript = Join-Path $PSScriptRoot "monitor-and-start.ps1"
if (-not (Test-Path $monitorScript)) {
    Write-Output "BŁĄD: Nie znaleziono monitor-and-start.ps1"
    Write-Output "Upewnij się, że plik znajduje się w tym samym katalogu."
    exit 1
}

Write-Output "Znaleziono monitor-and-start.ps1"
Write-Output "Uruchamiam monitor w tle..."

# Uruchom monitor w tle
$job = Start-Job -ScriptBlock {
    param($scriptPath)
    & $scriptPath
} -ArgumentList $monitorScript

Write-Output "Monitor został uruchomiony w tle (Job ID: $($job.Id))"
Write-Output ""
Write-Output "INSTRUKCJE:"
Write-Output "1. Monitor będzie działał w tle i automatycznie uruchamiał aplikację Calibrator"
Write-Output "2. Aby sprawdzić status monitora, uruchom: Get-Job -Id $($job.Id)"
Write-Output "3. Aby zobaczyć logi monitora, uruchom: Receive-Job -Id $($job.Id)"
Write-Output "4. Aby zatrzymać monitor, uruchom: Stop-Job -Id $($job.Id)"
Write-Output ""
Write-Output "Monitor jest aktywny i gotowy do pracy!" 