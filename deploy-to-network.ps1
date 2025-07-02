# Skrypt do kopiowania plikow Kalibrator do lokalizacji sieciowej
# Uruchom: .\deploy-to-network.ps1

param(
    [string]$Configuration = "Debug",
    [string]$NetworkLocation = "\\DiskStation\Public\Kalibrator",
    [string]$NetworkLocation2 = "\\KALIBRATOR\CalibratorPublic\Kalibrator"
)

# Debugowanie parametrow
Write-Output "=== DEBUG PARAMETRY ==="
Write-Output "Configuration: '$Configuration'"
Write-Output "NetworkLocation: '$NetworkLocation'"
Write-Output "NetworkLocation2: '$NetworkLocation2'"
Write-Output "========================="

Write-Output "=== Kopiowanie Kalibrator do lokalizacji sieciowych ==="
Write-Output "Konfiguracja: $Configuration"
Write-Output "Lokalizacja docelowa 1: $NetworkLocation"
Write-Output "Lokalizacja docelowa 2: $NetworkLocation2"

# Krok 1: Wykonanie dotnet publish
Write-Output ""
Write-Output "=== KROK 1: Publikowanie aplikacji ==="
Write-Output "Wykonywanie: dotnet publish Calibrator --configuration $Configuration"

try {
    $publishResult = dotnet publish Calibrator --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Output "Blad podczas publikowania aplikacji!"
        Write-Output "Kod bledu: $LASTEXITCODE"
        exit 1
    }
    Write-Output "Publikowanie zakonczone sukcesem!"
}
catch {
    Write-Output "Blad podczas wykonywania dotnet publish: $($_.Exception.Message)"
    exit 1
}

# Krok 2: Automatyczne wykrywanie katalogu z plikami (teraz zawsze publish)
$PublishPath = "Calibrator\bin\$Configuration\net9.0-windows\publish\"

if (Test-Path $PublishPath) {
    $SourcePath = $PublishPath
    Write-Output ""
    Write-Output "=== KROK 2: Kopiowanie plikow ==="
    Write-Output "Znaleziono katalog publish: $PublishPath"
}
else {
    Write-Output "Blad: Nie znaleziono katalogu publish po wykonaniu dotnet publish!"
    Write-Output "Sprawdzona sciezka: $PublishPath"
    exit 1
}

$ProjectPath = "Calibrator\"

# Lista lokalizacji docelowych
$NetworkLocations = @($NetworkLocation, $NetworkLocation2)

# Kopiowanie do wszystkich lokalizacji
foreach ($location in $NetworkLocations) {
    Write-Host "`n=== Kopiowanie do: $location ===" -ForegroundColor Cyan
    
    # Tworzenie katalogu docelowego jesli nie istnieje
    if (-not (Test-Path $location)) {
        Write-Host "Tworzenie katalogu docelowego: $location" -ForegroundColor Yellow
        try {
            New-Item -ItemType Directory -Path $location -Force | Out-Null
            Write-Host "Katalog utworzony pomyslnie." -ForegroundColor Green
        }
        catch {
            Write-Host "Blad podczas tworzenia katalogu: $($_.Exception.Message)" -ForegroundColor Red
            continue
        }
    }

    # Kopiowanie plikow wykonywalnych i zaleznosci
    Write-Host "Kopiowanie plikow aplikacji..." -ForegroundColor Yellow
    try {
        Copy-Item -Path "$SourcePath\*" -Destination $location -Recurse -Force
        Write-Host "Pliki aplikacji skopiowane pomyslnie." -ForegroundColor Green
    }
    catch {
        Write-Host "Blad podczas kopiowania plikow aplikacji: $($_.Exception.Message)" -ForegroundColor Red
        continue
    }

    # Kopiowanie plikow konfiguracyjnych
    Write-Host "Kopiowanie plikow konfiguracyjnych..." -ForegroundColor Yellow

    $ConfigFiles = @(
        "calibration_history.xml",
        "welder_settings.json"
    )

    foreach ($file in $ConfigFiles) {
        $sourceFile = Join-Path $ProjectPath $file
        if (Test-Path $sourceFile) {
            try {
                Copy-Item -Path $sourceFile -Destination $location -Force
                Write-Host "Skopiowano: $file" -ForegroundColor Green
            }
            catch {
                Write-Host "Blad podczas kopiowania $file : $($_.Exception.Message)" -ForegroundColor Red
            }
        }
        else {
            Write-Host "Plik nie istnieje: $file" -ForegroundColor Yellow
        }
    }
}

# Sprawdzenie wynikow
Write-Host "`n=== Podsumowanie ===" -ForegroundColor Green
$allSuccessful = $true
foreach ($location in $NetworkLocations) {
    if (Test-Path $location) {
        $copiedFiles = Get-ChildItem -Path $location -File | Measure-Object
        Write-Host "Lokalizacja: $location" -ForegroundColor Cyan
        Write-Host "Liczba skopiowanych plikow: $($copiedFiles.Count)" -ForegroundColor Cyan
    }
    else {
        Write-Host "Lokalizacja niedostepna: $location" -ForegroundColor Red
        $allSuccessful = $false
    }
}

if ($allSuccessful) {
    Write-Host "`nDeployment zakonczony sukcesem!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`nDeployment zakonczony z bledami!" -ForegroundColor Red
    exit 1
}