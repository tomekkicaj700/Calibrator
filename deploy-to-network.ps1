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
    Write-Output ""
    Write-Output "=== Kopiowanie do: $location ==="
    $runningFile = Join-Path $location "running.txt"
    $skipThisLocation = $false
    if (Test-Path $runningFile) {
        Write-Output "Wykryto uruchomiona aplikacje w lokalizacji docelowej: $location"
        Write-Output "Wysylam zadanie zamkniecia aplikacji..."
        
        # Utwórz plik żądania zamknięcia
        $requestCloseFile = Join-Path $location "request-to-close.txt"
        try {
            [System.IO.File]::WriteAllText($requestCloseFile, (Get-Date).ToString())
            Write-Output "Wyslano zadanie zamkniecia aplikacji."
            
            # Czekaj na zamknięcie aplikacji (maksymalnie 15 sekund, co 1 sekunda)
            $timeout = 15
            $elapsed = 0
            Write-Output "Oczekiwanie na zamkniecie aplikacji (maksymalnie $timeout sekund)..."
            while ((Test-Path $runningFile) -and ($elapsed -lt $timeout)) {
                Start-Sleep -Seconds 1
                $elapsed++
                Write-Output "  Czekam... ($elapsed s / $timeout s)"
            }
            
            if (Test-Path $runningFile) {
                Write-Output "Aplikacja nie zostala zamknieta w ciagu $timeout sekund. Pomijam kopiowanie do tej lokalizacji."
                $skipThisLocation = $true
            } else {
                Write-Output "Aplikacja zostala zamknieta pomyslnie."
            }
            
            # Usuń plik żądania zamknięcia
            if (Test-Path $requestCloseFile) {
                Remove-Item $requestCloseFile -Force
                Write-Output "Usunieto plik zadania zamkniecia."
            }
        }
        catch {
            Write-Output "Blad podczas wysylania zadania zamkniecia: $($_.Exception.Message)"
            Write-Output "Pomijam kopiowanie do tej lokalizacji."
            $skipThisLocation = $true
        }
    }
    if ($skipThisLocation) {
        continue
    }
    
    # Tworzenie katalogu docelowego jesli nie istnieje
    if (-not (Test-Path $location)) {
        Write-Output "Tworzenie katalogu docelowego: $location"
        try {
            New-Item -ItemType Directory -Path $location -Force | Out-Null
            Write-Output "Katalog utworzony pomyslnie."
        }
        catch {
            Write-Output "Blad podczas tworzenia katalogu: $($_.Exception.Message)"
            continue
        }
    }

    # Kopiowanie plikow wykonywalnych i zaleznosci
    Write-Output "Kopiowanie plikow aplikacji..."
    try {
        # Próbuj skopiować wszystkie pliki
        Copy-Item -Path "$SourcePath\*" -Destination $location -Recurse -Force -ErrorAction Stop
        Write-Output "Pliki aplikacji skopiowane pomyslnie."
    }
    catch {
        Write-Output "Błąd podczas kopiowania plików aplikacji: $($_.Exception.Message)"
        Write-Output "Próbuję skopiować pliki pojedynczo..."
        
        # Próbuj skopiować pliki pojedynczo, pomijając te które są zablokowane
        $sourceFiles = Get-ChildItem -Path $SourcePath -Recurse
        $successCount = 0
        $errorCount = 0
        
        foreach ($file in $sourceFiles) {
            $relativePath = $file.FullName.Substring($SourcePath.Length)
            $destinationPath = Join-Path $location $relativePath
            
            try {
                # Utwórz katalog docelowy jeśli nie istnieje
                $destinationDir = Split-Path $destinationPath -Parent
                if (!(Test-Path $destinationDir)) {
                    New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
                }
                
                Copy-Item -Path $file.FullName -Destination $destinationPath -Force -ErrorAction Stop
                $successCount++
            }
            catch {
                Write-Output "  Nie udało się skopiować: $relativePath - $($_.Exception.Message)"
                $errorCount++
            }
        }
        
        Write-Output "Kopiowanie zakonczoone: $successCount plikow skopiowanych, $errorCount bledow."
        
        if ($errorCount -gt 0) {
            Write-Output "UWAGA: Niektore pliki nie zostaly skopiowane z powodu bledow."
        }
    }

    # Kopiowanie plikow konfiguracyjnych
    Write-Output "Kopiowanie plikow konfiguracyjnych..."

    $ConfigFiles = @(
        "calibration_history.xml",
        "welder_settings.json"
    )

    foreach ($file in $ConfigFiles) {
        $sourceFile = Join-Path $ProjectPath $file
        if (Test-Path $sourceFile) {
            try {
                Copy-Item -Path $sourceFile -Destination $location -Force
                Write-Output "Skopiowano: $file"
            }
            catch {
                Write-Output "Blad podczas kopiowania $file : $($_.Exception.Message)"
            }
        }
        else {
            Write-Output "Plik nie istnieje: $file"
        }
    }
}

# Sprawdzenie wynikow
Write-Output ""
Write-Output "=== Podsumowanie ==="
$allSuccessful = $true
foreach ($location in $NetworkLocations) {
    if (Test-Path $location) {
        $copiedFiles = Get-ChildItem -Path $location -File | Measure-Object
        Write-Output "Lokalizacja: $location"
        Write-Output "Liczba skopiowanych plikow: $($copiedFiles.Count)"
    }
    else {
        Write-Output "Lokalizacja niedostepna: $location"
        $allSuccessful = $false
    }
}

if ($allSuccessful) {
    Write-Output ""
    Write-Output "Deployment zakonczony sukcesem!"
    exit 0
} else {
    Write-Output ""
    Write-Output "Deployment zakonczony z bledami!"
    exit 1
}