# Skrypt do kopiowania plików Kalibrator do lokalizacji sieciowej
# Uruchom: .\deploy-to-network.ps1

param(
    [string]$Configuration = "Debug",
    [string]$NetworkLocation = "\\DiskStation\Public\Kalibrator"
)

# Debugowanie parametrów
Write-Host "=== DEBUG PARAMETRY ===" -ForegroundColor Magenta
Write-Host "Configuration: '$Configuration'" -ForegroundColor Magenta
Write-Host "NetworkLocation: '$NetworkLocation'" -ForegroundColor Magenta
Write-Host "=========================" -ForegroundColor Magenta

Write-Host "=== Kopiowanie Kalibrator do lokalizacji sieciowej ===" -ForegroundColor Green
Write-Host "Konfiguracja: $Configuration" -ForegroundColor Yellow
Write-Host "Lokalizacja docelowa: $NetworkLocation" -ForegroundColor Yellow

# Krok 1: Wykonanie dotnet publish
Write-Host "`n=== KROK 1: Publikowanie aplikacji ===" -ForegroundColor Cyan
Write-Host "Wykonywanie: dotnet publish Calibrator --configuration $Configuration" -ForegroundColor Yellow

try {
    $publishResult = dotnet publish Calibrator --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Błąd podczas publikowania aplikacji!" -ForegroundColor Red
        Write-Host "Kod błędu: $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
    Write-Host "Publikowanie zakończone sukcesem!" -ForegroundColor Green
}
catch {
    Write-Host "Błąd podczas wykonywania dotnet publish: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Krok 2: Automatyczne wykrywanie katalogu z plikami (teraz zawsze publish)
$PublishPath = "Calibrator\bin\$Configuration\net9.0-windows\publish\"

if (Test-Path $PublishPath) {
    $SourcePath = $PublishPath
    Write-Host "`n=== KROK 2: Kopiowanie plików ===" -ForegroundColor Cyan
    Write-Host "Znaleziono katalog publish: $PublishPath" -ForegroundColor Green
}
else {
    Write-Host "Błąd: Nie znaleziono katalogu publish po wykonaniu dotnet publish!" -ForegroundColor Red
    Write-Host "Sprawdzona ścieżka: $PublishPath" -ForegroundColor Red
    exit 1
}

$ProjectPath = "Calibrator\"

# Tworzenie katalogu docelowego jeśli nie istnieje
if (-not (Test-Path $NetworkLocation)) {
    Write-Host "Tworzenie katalogu docelowego: $NetworkLocation" -ForegroundColor Yellow
    try {
        New-Item -ItemType Directory -Path $NetworkLocation -Force | Out-Null
        Write-Host "Katalog utworzony pomyślnie." -ForegroundColor Green
    }
    catch {
        Write-Host "Błąd podczas tworzenia katalogu: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# Kopiowanie plików wykonywalnych i zależności
Write-Host "Kopiowanie plików aplikacji..." -ForegroundColor Yellow
try {
    Copy-Item -Path "$SourcePath\*" -Destination $NetworkLocation -Recurse -Force
    Write-Host "Pliki aplikacji skopiowane pomyślnie." -ForegroundColor Green
}
catch {
    Write-Host "Błąd podczas kopiowania plików aplikacji: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Kopiowanie plików konfiguracyjnych
Write-Host "Kopiowanie plików konfiguracyjnych..." -ForegroundColor Yellow

$ConfigFiles = @(
    "calibration_history.xml",
    "welder_settings.json"
)

foreach ($file in $ConfigFiles) {
    $sourceFile = Join-Path $ProjectPath $file
    if (Test-Path $sourceFile) {
        try {
            Copy-Item -Path $sourceFile -Destination $NetworkLocation -Force
            Write-Host "Skopiowano: $file" -ForegroundColor Green
        }
        catch {
            Write-Host "Błąd podczas kopiowania $file : $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    else {
        Write-Host "Plik nie istnieje: $file" -ForegroundColor Yellow
    }
}

# Sprawdzenie wyników
Write-Host "`n=== Podsumowanie ===" -ForegroundColor Green
$copiedFiles = Get-ChildItem -Path $NetworkLocation -File | Measure-Object
Write-Host "Liczba skopiowanych plików: $($copiedFiles.Count)" -ForegroundColor Cyan
Write-Host "Lokalizacja docelowa: $NetworkLocation" -ForegroundColor Cyan

Write-Host "`nDeployment zakończony sukcesem!" -ForegroundColor Green

exit 0 