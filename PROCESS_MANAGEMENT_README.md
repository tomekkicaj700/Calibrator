# Mechanizm Zarządzania Procesami - Calibrator

## Opis

Zaimplementowano mechanizm bezpiecznego zamykania aplikacji przed deploymentem, który rozwiązuje problem z kopiowaniem plików gdy aplikacja jest uruchomiona w lokalizacji docelowej.

## Jak to działa

### 1. Pliki kontrolne

Aplikacja używa dwóch plików kontrolnych w katalogu aplikacji:

- **`running.txt`** - zawiera ID procesu aplikacji, oznacza że aplikacja jest uruchomiona
- **`request-to-close.txt`** - pusty plik, który gdy zostanie utworzony, sygnalizuje aplikacji żądanie zamknięcia

### 2. Mechanizm w aplikacji

#### Inicjalizacja
- Przy uruchomieniu aplikacji tworzony jest plik `running.txt` z ID procesu
- Uruchamiany jest timer sprawdzający co sekundę czy istnieje plik `request-to-close.txt`

#### Sprawdzanie żądań zamknięcia
- Timer co sekundę sprawdza czy plik `request-to-close.txt` istnieje
- Jeśli plik zostanie wykryty:
  1. Usuwa plik `request-to-close.txt`
  2. Loguje informację o żądaniu zamknięcia
  3. Wywołuje `Application.Current.Shutdown()`

#### Zamykanie aplikacji
- Przy zamykaniu aplikacji (normalnym lub wymuszonym):
  1. Zatrzymuje timer sprawdzania
  2. Usuwa plik `running.txt`
  3. Usuwa plik `request-to-close.txt` (jeśli istnieje)

### 3. Mechanizm w skrypcie deploymentu

#### Sprawdzanie uruchomionej aplikacji
- Przed kopiowaniem sprawdza czy istnieje plik `running.txt` w lokalizacji docelowej
- Jeśli plik istnieje, oznacza to że aplikacja jest uruchomiona

#### Wysyłanie żądania zamknięcia
- Tworzy plik `request-to-close.txt` w lokalizacji docelowej
- Czeka maksymalnie 10 sekund na zniknięcie pliku `running.txt`
- Jeśli timeout zostanie przekroczony, kontynuuje kopiowanie

#### Czyszczenie
- Usuwa plik `request-to-close.txt` po zakończeniu operacji

## Implementacja

### Klasa ProcessManager

```csharp
public class ProcessManager
{
    private const string RUNNING_FILE = "running.txt";
    private const string REQUEST_CLOSE_FILE = "request-to-close.txt";
    private const int CHECK_INTERVAL_MS = 1000; // Sprawdzaj co sekundę
    private const int CLOSE_TIMEOUT_MS = 10000; // 10 sekund timeout
    
    // Metody:
    // - Initialize() - inicjalizuje mechanizm
    // - Shutdown() - zatrzymuje mechanizm
    // - CheckForCloseRequest() - sprawdza żądania zamknięcia
    // - IsApplicationRunning() - sprawdza czy aplikacja jest uruchomiona
    // - RequestApplicationClose() - wysyła żądanie zamknięcia
}
```

### Integracja w MainWindow

```csharp
public partial class MainWindow : Window
{
    private ProcessManager? processManager;
    
    public MainWindow()
    {
        // Inicjalizacja ProcessManager
        processManager = new ProcessManager(Log);
        processManager.Initialize();
    }
    
    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // Zatrzymaj ProcessManager przed zamknięciem
        processManager?.Shutdown();
    }
}
```

### Skrypt PowerShell

```powershell
# Sprawdź czy aplikacja jest uruchomiona
$runningFile = Join-Path $location "running.txt"
if (Test-Path $runningFile) {
    # Wysyłam żądanie zamknięcia
    $requestCloseFile = Join-Path $location "request-to-close.txt"
    [System.IO.File]::WriteAllText($requestCloseFile, (Get-Date).ToString())
    
    # Czekaj na zamknięcie (maksymalnie 10 sekund)
    $timeout = 10
    $elapsed = 0
    while (Test-Path $runningFile -and $elapsed -lt $timeout) {
        Start-Sleep -Milliseconds 100
        $elapsed += 0.1
    }
}
```

## Korzyści

1. **Bezpieczne zamykanie** - aplikacja zamyka się w kontrolowany sposób
2. **Automatyzacja** - nie wymaga ręcznego zamykania aplikacji
3. **Timeout** - nie blokuje deploymentu na zawsze
4. **Logowanie** - wszystkie operacje są logowane
5. **Kompatybilność** - działa z istniejącymi skryptami

## Użycie

### Deployment przez PowerShell
```powershell
.\deploy-to-network.ps1
```

### Deployment przez Batch
```cmd
deploy-to-network.bat
```

### Ręczne sprawdzenie
```powershell
# Sprawdź czy aplikacja jest uruchomiona w danej lokalizacji
$runningFile = "\\DiskStation\Public\Kalibrator\running.txt"
if (Test-Path $runningFile) {
    Write-Host "Aplikacja jest uruchomiona"
} else {
    Write-Host "Aplikacja nie jest uruchomiona"
}
```

## Troubleshooting

### Aplikacja nie zamyka się automatycznie
- Sprawdź czy plik `request-to-close.txt` został utworzony
- Sprawdź logi aplikacji pod kątem błędów ProcessManager
- Sprawdź czy aplikacja ma uprawnienia do zapisu w katalogu

### Timeout podczas deploymentu
- Sprawdź czy aplikacja nie jest zablokowana przez inne procesy
- Zwiększ timeout w skrypcie deploymentu jeśli potrzeba
- Sprawdź czy nie ma błędów w logach aplikacji

### Pliki kontrolne nie są usuwane
- Sprawdź czy aplikacja ma uprawnienia do usuwania plików
- Sprawdź czy nie ma błędów w metodzie Shutdown()
- Ręcznie usuń pliki jeśli potrzeba

## Bezpieczeństwo

- Pliki kontrolne są tworzone tylko w katalogu aplikacji
- Mechanizm nie wpływa na inne aplikacje
- Timeout zapobiega blokowaniu deploymentu
- Wszystkie operacje są logowane 