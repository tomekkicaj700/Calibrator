﻿using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WelderRS232;
using CalibrationReportLib;
using System.Text;
using System.ComponentModel;
using System.Threading;
using System.Globalization;
using System.Windows.Media;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Serialization;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using Calibrator.Services;
using Calibrator.Controls;
using Logger;
using static Logger.LoggerService;
using System.Text.Json;
using System.Diagnostics;

namespace Calibrator;

/// <summary>
/// Klasa do zarządzania ustawieniami UI okna
/// </summary>
public class WindowSettings
{
    private const string WINDOW_SETTINGS_FILE = "window_settings.json";

    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool? WindowMaximized { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? LogPanelHeight { get; set; }
    public bool? LogEnabled { get; set; }
    public double? TablePanelProportion { get; set; }
    public bool? LogPanelVisible { get; set; }

    public static WindowSettings Load()
    {
        try
        {
            if (File.Exists(WINDOW_SETTINGS_FILE))
            {
                string jsonString = File.ReadAllText(WINDOW_SETTINGS_FILE);
                var settings = JsonSerializer.Deserialize<WindowSettings>(jsonString);
                return settings ?? new WindowSettings();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd podczas odczytu ustawień okna: {ex.Message}");
        }
        return new WindowSettings();
    }

    public void Save()
    {
        try
        {
            string jsonString = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(WINDOW_SETTINGS_FILE, jsonString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd podczas zapisu ustawień okna: {ex.Message}");
        }
    }

    public static string GetConfigFilePath()
    {
        return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.xml");
    }
}

/// <summary>
/// Klasa do zarządzania procesami aplikacji - mechanizm bezpiecznego zamykania
/// </summary>
public class ProcessManager
{
    private const string RUNNING_FILE = "running.txt";
    private const string REQUEST_CLOSE_FILE = "request-to-close.txt";
    private const int CHECK_INTERVAL_MS = 1000; // Sprawdzaj co sekundę
    private const int CLOSE_TIMEOUT_MS = 10000; // 10 sekund timeout na zamknięcie

    private readonly string _appDirectory;
    private readonly string _runningFilePath;
    private readonly string _requestCloseFilePath;
    private readonly DispatcherTimer _checkTimer;
    private readonly Action<string> _logCallback;
    private bool _isShuttingDown = false;

    public ProcessManager(Action<string> logCallback)
    {
        _appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _runningFilePath = System.IO.Path.Combine(_appDirectory, RUNNING_FILE);
        _requestCloseFilePath = System.IO.Path.Combine(_appDirectory, REQUEST_CLOSE_FILE);
        _logCallback = logCallback;

        _checkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(CHECK_INTERVAL_MS)
        };
        _checkTimer.Tick += CheckForCloseRequest;
    }

    /// <summary>
    /// Inicjalizuje mechanizm zarządzania procesami
    /// </summary>
    public void Initialize()
    {
        try
        {
            // Utwórz plik running.txt z ID procesu
            int processId = Process.GetCurrentProcess().Id;
            File.WriteAllText(_runningFilePath, processId.ToString());
            _logCallback?.Invoke($"[PROCESS] Utworzono plik running.txt z ID procesu: {processId}");

            // Uruchom timer sprawdzający żądania zamknięcia
            _checkTimer.Start();
            _logCallback?.Invoke("[PROCESS] Timer sprawdzania żądań zamknięcia uruchomiony");

            // Zarejestruj handler dla zamykania aplikacji
            Application.Current.Exit += OnApplicationExit;
        }
        catch (Exception ex)
        {
            _logCallback?.Invoke($"[PROCESS] Błąd podczas inicjalizacji: {ex.Message}");
        }
    }

    /// <summary>
    /// Zatrzymuje mechanizm zarządzania procesami
    /// </summary>
    public void Shutdown()
    {
        try
        {
            _isShuttingDown = true;
            _checkTimer.Stop();
            Application.Current.Exit -= OnApplicationExit;

            // Usuń plik running.txt
            if (File.Exists(_runningFilePath))
            {
                File.Delete(_runningFilePath);
                _logCallback?.Invoke("[PROCESS] Usunięto plik running.txt");
            }

            // Usuń plik request-to-close.txt jeśli istnieje
            if (File.Exists(_requestCloseFilePath))
            {
                File.Delete(_requestCloseFilePath);
                _logCallback?.Invoke("[PROCESS] Usunięto plik request-to-close.txt");
            }
        }
        catch (Exception ex)
        {
            _logCallback?.Invoke($"[PROCESS] Błąd podczas zamykania: {ex.Message}");
        }
    }

    /// <summary>
    /// Sprawdza czy istnieje żądanie zamknięcia aplikacji
    /// </summary>
    private void CheckForCloseRequest(object? sender, EventArgs e)
    {
        if (_isShuttingDown) return;

        try
        {
            if (File.Exists(_requestCloseFilePath))
            {
                _logCallback?.Invoke("[PROCESS] Wykryto żądanie zamknięcia aplikacji");

                // Usuń plik request-to-close.txt
                File.Delete(_requestCloseFilePath);
                _logCallback?.Invoke("[PROCESS] Usunięto plik request-to-close.txt");

                // Zamknij aplikację
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    _logCallback?.Invoke("[PROCESS] Zamykanie aplikacji...");
                    Application.Current.Shutdown();
                });
            }
        }
        catch (Exception ex)
        {
            _logCallback?.Invoke($"[PROCESS] Błąd podczas sprawdzania żądania zamknięcia: {ex.Message}");
        }
    }

    /// <summary>
    /// Handler dla zamykania aplikacji
    /// </summary>
    private void OnApplicationExit(object sender, ExitEventArgs e)
    {
        Shutdown();
    }

    /// <summary>
    /// Statyczna metoda do sprawdzenia czy aplikacja jest uruchomiona w danej lokalizacji
    /// </summary>
    public static bool IsApplicationRunning(string directory)
    {
        string runningFile = System.IO.Path.Combine(directory, RUNNING_FILE);
        return File.Exists(runningFile);
    }

    /// <summary>
    /// Statyczna metoda do wysłania żądania zamknięcia aplikacji
    /// </summary>
    public static bool RequestApplicationClose(string directory, Action<string>? logCallback = null)
    {
        try
        {
            string requestFile = System.IO.Path.Combine(directory, REQUEST_CLOSE_FILE);
            string runningFile = System.IO.Path.Combine(directory, RUNNING_FILE);

            // Sprawdź czy aplikacja jest uruchomiona
            if (!File.Exists(runningFile))
            {
                logCallback?.Invoke($"[DEPLOY] Aplikacja nie jest uruchomiona w {directory}");
                return true; // Aplikacja już nie jest uruchomiona
            }

            // Utwórz plik żądania zamknięcia
            File.WriteAllText(requestFile, DateTime.Now.ToString());
            logCallback?.Invoke($"[DEPLOY] Wysłano żądanie zamknięcia do aplikacji w {directory}");

            // Czekaj na zamknięcie aplikacji
            DateTime startTime = DateTime.Now;
            while (File.Exists(runningFile))
            {
                if ((DateTime.Now - startTime).TotalMilliseconds > CLOSE_TIMEOUT_MS)
                {
                    logCallback?.Invoke($"[DEPLOY] Timeout podczas oczekiwania na zamknięcie aplikacji w {directory}");
                    return false;
                }
                Thread.Sleep(100); // Czekaj 100ms przed kolejnym sprawdzeniem
            }

            // Usuń plik żądania zamknięcia
            if (File.Exists(requestFile))
            {
                File.Delete(requestFile);
            }

            logCallback?.Invoke($"[DEPLOY] Aplikacja została zamknięta w {directory}");
            return true;
        }
        catch (Exception ex)
        {
            logCallback?.Invoke($"[DEPLOY] Błąd podczas wysyłania żądania zamknięcia: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // Unikalne identyfikatory zakładek (niezależne od języka)
    private const string TAB_ID_WELD_PARAMETERS = "weld_parameters";
    private const string TAB_ID_CALIBRATION_PARAMETERS = "calibration_parameters";
    private const string TAB_ID_CONFIGURATION = "configuration";
    private const string TAB_ID_OTHER_PARAMETERS = "other_parameters";
    private const string TAB_ID_MEASUREMENT_HISTORY = "measurement_history";
    private const string TAB_ID_MEASUREMENT_HISTORY_NEW = "measurement_history_new";
    private const string TAB_ID_INFO = "info";
    private const string TAB_ID_COMMUNICATION = "communication";

    // Stałe dla nazw zakładek
    private const string TAB_PARAMETRY_ZGRZEWANIA = "Parametry zgrzewania";
    private const string TAB_PARAMETRY_KALIBRACJI = "Parametry kalibracji";
    private const string TAB_KONFIGURACJA = "Konfiguracja";
    private const string TAB_POZOSTALE_PARAMETRY = "Pozostałe parametry";
    private const string TAB_HISTORIA_POMIAROW = "Historia pomiarów";
    private const string TAB_INFO = "INFO";
    private const string TAB_KOMUNIKACJA = "Komunikacja";

    // Słownik mapujący identyfikatory na nazwy zakładek (może być rozszerzony o inne języki)
    private readonly Dictionary<string, string> tabNames = new Dictionary<string, string>
            {
            { TAB_ID_WELD_PARAMETERS, "Parametry zgrzewania" },
            { TAB_ID_CALIBRATION_PARAMETERS, "Parametry kalibracji" },
            { TAB_ID_CONFIGURATION, "Konfiguracja" },
            { TAB_ID_OTHER_PARAMETERS, "Pozostałe parametry" },
            { TAB_ID_MEASUREMENT_HISTORY, "Historia pomiarów" },
            { TAB_ID_INFO, "INFO" },
            { TAB_ID_COMMUNICATION, "Komunikacja" }
            };

    // Metoda do pobierania nazwy zakładki na podstawie identyfikatora
    private string GetTabName(string tabId)
    {
        return tabNames.TryGetValue(tabId, out string? name) ? name : tabId;
    }

    // Struktura pomocnicza do mapowania wartości kanałów zgrzewarki
    private class WartosciKanalowZgrzewarki
    {
        public int MMWVH, MMWVL, IVHC_U, IVLC_U, ADCIVHC_U, ADCIVLC_U;
        public int MMWCL, MMWCH, IVHC_I, IVLC_I, ADCIVHC_I, ADCIVLC_I;
    }

    private WelderService? welderService;
    private readonly System.Windows.Threading.DispatcherTimer configTimer;
    private bool isRunning = false;

    // Licznik komend na sekundę
    private int commandsSentThisSecond = 0;
    private DateTime lastCommandTime = DateTime.Now;
    private readonly System.Windows.Threading.DispatcherTimer commandCounterTimer;

    // Wydajne zarządzanie logiem UI
    private readonly System.Text.StringBuilder logBuffer = new System.Text.StringBuilder();
    private int currentLogLines = 0;
    private const int MAX_LOG_LINES = 500; // Zmniejszone z 1000 na 500
    private bool logNeedsUpdate = false;
    private readonly System.Windows.Threading.DispatcherTimer logUpdateTimer;
    private DateTime lastCommunicationTime = DateTime.Now;

    // TCP Server service
    private LocalTcpServerService tcpServerService = new LocalTcpServerService();

    private SKonfiguracjaSystemu? lastConfig;

    // ProcessManager do zarządzania procesami aplikacji
    private ProcessManager? processManager;

    // Właściwości pomocnicze do dostępu do kontrolek w UserControl
    private WeldParametersTab WeldParametersTab => weldParametersTab;
    private CalibrationParametersTab CalibrationParametersTab => calibrationParametersTab;
    private MeasurementHistoryTab MeasurementHistoryTab => measurementHistoryTab;


    private InfoTab InfoTab => infoTab;
    private CommunicationTab CommunicationTab => communicationTab;

    private double? lastLogPanelHeight = null;

    public MainWindow()
    {
        InitializeComponent();

        // Inicjalizacja ProcessManager do zarządzania procesami
        processManager = new ProcessManager(Log);
        processManager.Initialize();

        // Initialize services according to the new architecture
        InitializeServicesAsync();

        // Ustaw LabelFormatter dla gaugeNapiecie i gaugePrad
        // Przeniesione do WeldParametersTab

        // Subscribe to ConfigService events
        var configService = ServiceContainer.ConfigService;
        configService.SettingsChanged += OnConfigSettingsChanged;
        configService.DetectedPortsChanged += OnConfigDetectedPortsChanged;

        // Przywracanie rozmiaru i stanu okna przed wyświetleniem
        var windowSettings = WindowSettings.Load();
        if (windowSettings.WindowWidth.HasValue && windowSettings.WindowHeight.HasValue &&
windowSettings.WindowWidth.Value > 0 && windowSettings.WindowHeight.Value > 0)
        {
            // Sprawdź rozmiar ekranu przed ustawieniem rozmiaru okna
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            // Ustaw rozmiar okna, ale nie przekraczaj rozmiaru ekranu
            double windowWidth = Math.Min(windowSettings.WindowWidth.Value, screenWidth);
            double windowHeight = Math.Min(windowSettings.WindowHeight.Value, screenHeight);

            // Dodatkowo sprawdź czy okno nie jest za małe (minimum 800x600)
            windowWidth = Math.Max(windowWidth, 800);
            windowHeight = Math.Max(windowHeight, 600);

            this.Width = windowWidth;
            this.Height = windowHeight;

            // Sprawdź i ustaw pozycję okna
            if (windowSettings.WindowLeft.HasValue && windowSettings.WindowTop.HasValue)
            {
                double windowLeft = windowSettings.WindowLeft.Value;
                double windowTop = windowSettings.WindowTop.Value;

                // Sprawdź czy okno nie wychodzi poza granice ekranu
                if (windowLeft + windowWidth > screenWidth)
                {
                    windowLeft = Math.Max(0, screenWidth - windowWidth);
                }
                if (windowTop + windowHeight > screenHeight)
                {
                    windowTop = Math.Max(0, screenHeight - windowHeight);
                }

                // Sprawdź czy okno nie jest całkowicie poza ekranem (np. na odłączonym monitorze)
                if (windowLeft < -windowWidth + 100 || windowTop < -windowHeight + 100)
                {
                    // Wycentruj okno na ekranie
                    windowLeft = (screenWidth - windowWidth) / 2;
                    windowTop = (screenHeight - windowHeight) / 2;

                    Dispatcher.BeginInvoke(() =>
                      {
                          Log("Okno było poza ekranem - wycentrowano na ekranie");
                      });
                }

                // Ustaw pozycję okna
                this.Left = windowLeft;
                this.Top = windowTop;

                // Logowanie informacji o dostosowaniu pozycji
                if (windowLeft != windowSettings.WindowLeft.Value || windowTop != windowSettings.WindowTop.Value)
                {
                    Dispatcher.BeginInvoke(() =>
                          {
                              Log($"Dostosowano pozycję okna do rozmiaru ekranu: {screenWidth}x{screenHeight}");
                              Log($"Zapisana pozycja: {windowSettings.WindowLeft.Value},{windowSettings.WindowTop.Value}");
                              Log($"Ustawiona pozycja: {windowLeft},{windowTop}");
                          });
                }
            }

            // Logowanie informacji o dostosowaniu rozmiaru
            if (windowWidth != windowSettings.WindowWidth.Value || windowHeight != windowSettings.WindowHeight.Value)
            {
                // Użyj Dispatcher.BeginInvoke aby logowanie nastąpiło po inicjalizacji komponentów
                Dispatcher.BeginInvoke(() =>
                          {
                              Log($"Dostosowano rozmiar okna do rozmiaru ekranu: {screenWidth}x{screenHeight}");
                              Log($"Zapisany rozmiar: {windowSettings.WindowWidth.Value}x{windowSettings.WindowHeight.Value}");
                              Log($"Ustawiony rozmiar: {windowWidth}x{windowHeight}");
                          });
            }
        }
        if (windowSettings.WindowMaximized.HasValue)
        {
            this.WindowState = windowSettings.WindowMaximized.Value ? WindowState.Maximized : WindowState.Normal;
        }

        configTimer = new System.Windows.Threading.DispatcherTimer();
        configTimer.Tick += ConfigTimer_Tick;
        InitConfigTimer(); // Inicjalizuj timer z poprawnym interwałem

        // Inicjalizacja timera licznika komend
        commandCounterTimer = new System.Windows.Threading.DispatcherTimer();
        commandCounterTimer.Interval = TimeSpan.FromSeconds(1);
        commandCounterTimer.Tick += CommandCounterTimer_Tick;
        commandCounterTimer.Start();

        // Inicjalizacja timera aktualizacji logu
        logUpdateTimer = new System.Windows.Threading.DispatcherTimer();
        logUpdateTimer.Interval = TimeSpan.FromMilliseconds(100); // Aktualizuj co 100ms
        logUpdateTimer.Tick += LogUpdateTimer_Tick;
        logUpdateTimer.Start();

        // Inicjalizacja filtrowania
        ApplyFilter();

        // Wyświetl IP komputera w pasku tytułowym
        UpdateTitleWithLocalIP();

        // Subskrybuj logi z LoggerService
        LoggerService.Instance.LogMessageAppended += AppendLogToUI;
        LoggerService.Instance.LogHistoryLoaded += LoadLogHistoryToUI;
        LoggerService.Instance.LoadLogHistory();

        // Subskrybuj eventy serwisu TCP
        // Możesz dodać obsługę DataReceived/ClientConnected/ClientDisconnected jeśli chcesz
        tcpServerService.ClientConnected += OnTcpClientConnected;
        tcpServerService.ClientDisconnected += OnTcpClientDisconnected;

        // Przywracanie widoczności logPanel
        if (windowSettings.LogPanelVisible.HasValue)
        {
            if (!windowSettings.LogPanelVisible.Value)
            {
                logPanel.Visibility = Visibility.Collapsed;
                logSplitter.Visibility = Visibility.Collapsed;
                var mainGrid = (Grid)this.Content;
                mainGrid.RowDefinitions[2].Height = new GridLength(0);
            }
            else
            {
                logPanel.Visibility = Visibility.Visible;
                logSplitter.Visibility = Visibility.Visible;
            }
        }
    }

    private async void InitializeServicesAsync()
    {
        try
        {
            Log("Initializing services...");
            await ServiceContainer.InitializeAsync();

            // Get services after initialization
            welderService = ServiceContainer.WelderService;

            // Podłącz eventy serwisu do UI
            if (welderService != null)
            {
                welderService.WeldParametersUpdated += OnWeldParametersUpdated;
                welderService.ConfigurationUpdated += OnConfigurationUpdated;
                welderService.WelderStatusChanged += OnWelderStatusChanged;
                welderService.HistoryUpdated += OnHistoryUpdated;

                // Inicjalizacja historii pomiarów
                // Przeniesione do MeasurementHistoryTab
            }

            Log("Services initialized successfully");
        }
        catch (Exception ex)
        {
            Log($"Error initializing services: {ex.Message}");
            // Fallback to direct service access
            welderService = ServiceContainer.WelderService;
        }
    }

    private string GetBuildDateTime()
    {
        try
        {
            // Próbuj odczytać z assembly
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var buildTime = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                .OfType<System.Reflection.AssemblyMetadataAttribute>()
                .FirstOrDefault(x => x.Key == "BuildDateTime")?.Value;

            if (!string.IsNullOrEmpty(buildTime))
            {
                return buildTime;
            }

            // Fallback - użyj czasu kompilacji z assembly
            var fileInfo = new FileInfo(assembly.Location);
            return fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch
        {
            return "Nieznany";
        }
    }

    private void UpdateTitleWithLocalIP()
    {
        try
        {
            string localIP = GetLocalIPAddress();
            if (!string.IsNullOrEmpty(localIP))
            {
                this.Title = $"Calibrator - IP: {localIP}";
                Log($"Adres IP komputera: {localIP}");
            }
            else
            {
                this.Title = "Calibrator";
                Log("Nie udało się określić adresu IP komputera");
            }
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas pobierania adresu IP: {ex.Message}");
            this.Title = "Calibrator";
        }
    }

    private string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas pobierania adresu IP: {ex.Message}");
        }
        return string.Empty;
    }

    private void InitConfigTimer()
    {
        configTimer.Interval = TimeSpan.FromMilliseconds(GetSelectedInterval());
    }

    private async void btnRun_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!isRunning)
            {
                // Sprawdź połączenie przed uruchomieniem
                if (!await EnsureWelderConnectionAsync("uruchomienia pomiarów"))
                    return;

                // Upewnij się, że timer ma poprawny interwał
                InitConfigTimer();

                measurementHistoryNewTab.ClearData();

                isRunning = true;
                btnRun.IsEnabled = true;
                btnRun.Background = Brushes.Red; // Czerwony kolor dla STOP
                btnRun.Foreground = Brushes.White; // Biały tekst

                configTimer.Start();
                commandCounterTimer.Start(); // Uruchom timer licznika komend
                Log("▶ Pomiar parametrów uruchomiony");
            }
            else
            {
                isRunning = false;
                btnRun.IsEnabled = true;
                btnRun.Background = Brushes.Green; // Zielony kolor dla RUN
                btnRun.Foreground = Brushes.White; // Biały tekst

                configTimer.Stop();
                commandCounterTimer.Stop(); // Zatrzymaj timer licznika komend
                commandsSentThisSecond = 0; // Resetuj licznik

                measurementHistoryNewTab.SaveDataToFile();

                Log("⏸ Pomiar parametrów zatrzymany");
            }
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas przełączania trybu pomiaru: {ex.Message}");
        }
    }

    private void ConfigTimer_Tick(object? sender, EventArgs e)
    {
        if (isRunning)
        {
            _ = ReadWeldParametersAndUpdateUIAsync();
        }
    }

    private void CommandCounterTimer_Tick(object? sender, EventArgs e)
    {
        UpdateStatusBar();
        commandsSentThisSecond = 0; // Resetuj licznik
    }

    private void LogUpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (logNeedsUpdate)
        {
            logPanel.ClearLog();
            logPanel.AppendLog(logBuffer.ToString());
            logNeedsUpdate = false;
        }
    }

    private int GetSelectedInterval()
    {
        if (comboInterval.SelectedItem is System.Windows.Controls.ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int ms))
            return ms;
        return 1000; // default 1s
    }

    private async Task ReadConfigAndUpdateUIAsync()
    {
        if (welderService?.IsReadingConfig == true) return;

        try
        {
            lastCommunicationTime = DateTime.Now; // Aktualizuj czas ostatniej komunikacji
            Log("=== ODCZYTUJĘ KONFIGURACJĘ SYSTEMU ===");

            var config = await welderService?.ReadConfigurationAsync();
            if (config != null)
            {
                DisplayConfiguration(config);
                Log("✓ Konfiguracja została odczytana i wyświetlona.");
            }
            else
            {
                Log("✗ Nie udało się odczytać konfiguracji systemu.");
            }
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas odczytu konfiguracji: {ex.Message}");
        }
    }

    private void comboInterval_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (configTimer != null)
        {
            configTimer.Interval = TimeSpan.FromMilliseconds(GetSelectedInterval());
        }
    }

    private void UpdateWelderInfo()
    {
        if (welderService == null) return;

        var status = welderService.WelderStatus;
        var statusDescription = GetStatusDescription(status);
        txtStatus.Text = statusDescription;

        // Aktualizacja paska statusu
        txtStatusSection0.Text = $"Status: {statusDescription}";
        txtStatusSection2.Text = $"Połączenie: {welderService.ConnectedPort ?? "Brak"}";
        txtStatusSection3.Text = $"Czas: {DateTime.Now:HH:mm:ss}";

        // Aktualizacja koloru statusu
        switch (status)
        {
            case WelderStatus.CONNECTED:
            case WelderStatus.NEW_WELDER:
                txtStatus.Foreground = Brushes.Green;
                txtStatusSection0.Foreground = Brushes.Green;
                break;
            case WelderStatus.NO_CONNECTION:
                txtStatus.Foreground = Brushes.Red;
                txtStatusSection0.Foreground = Brushes.Red;
                break;
            default:
                txtStatus.Foreground = Brushes.Orange;
                txtStatusSection0.Foreground = Brushes.Orange;
                break;
        }
    }

    private string GetStatusDescription(WelderStatus status)
    {
        var field = status.GetType().GetField(status.ToString());
        if (field == null) return status.ToString();

        var attribute = (DescriptionAttribute?)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
        return attribute?.Description ?? status.ToString();
    }

    private void AppendLogToUI(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => AppendLogToUI(message));
            return;
        }

        // Dodaj nową linię do bufora
        logBuffer.AppendLine(message);
        currentLogLines++;

        // Jeśli przekroczono limit linii, usuń najstarsze
        if (currentLogLines > MAX_LOG_LINES)
        {
            // Znajdź pierwszy znak nowej linii i usuń wszystko przed nim
            var text = logBuffer.ToString();
            var lines = text.Split('\n');
            if (lines.Length > MAX_LOG_LINES)
            {
                // Zachowaj tylko ostatnie MAX_LOG_LINES linii
                var newLines = lines.Skip(lines.Length - MAX_LOG_LINES - 1).ToArray();
                logBuffer.Clear();
                logBuffer.AppendLine(string.Join("\n", newLines));
                currentLogLines = MAX_LOG_LINES;
            }
        }

        // Oznacz że log wymaga aktualizacji
        logNeedsUpdate = true;
    }

    private void LoadLogHistoryToUI(IReadOnlyList<string> history)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => LoadLogHistoryToUI(history));
            return;
        }

        // Wyczyść bufor i załaduj historię
        logBuffer.Clear();
        currentLogLines = 0;

        // Załaduj tylko ostatnie MAX_LOG_LINES linii z historii
        var linesToLoad = history.Skip(Math.Max(0, history.Count - MAX_LOG_LINES)).ToList();

        foreach (var line in linesToLoad)
        {
            logBuffer.AppendLine(line);
            currentLogLines++;
        }

        // Oznacz że log wymaga aktualizacji
        logNeedsUpdate = true;
    }

    private void IncrementCommandCounter()
    {
        commandsSentThisSecond++;
        lastCommandTime = DateTime.Now;
    }

    private void UpdateStatusBar()
    {
        if (welderService != null)
        {
            var status = welderService.WelderStatus;
            var statusDescription = GetStatusDescription(status);
            txtStatusSection0.Text = $"Status: {statusDescription}";
            txtStatusSection2.Text = $"Połączenie: {welderService.ConnectedPort ?? "Brak"}";
        }
        else
        {
            txtStatusSection0.Text = "Status: Nie zainicjalizowany";
            txtStatusSection2.Text = "Połączenie: Brak";
        }

        txtStatusSection1.Text = $"Komendy/s: {commandsSentThisSecond}";
        txtStatusSection3.Text = $"Czas: {DateTime.Now:HH:mm:ss} | Log: {currentLogLines} linii";

        // Aktualizuj informację o czasie buildu
        txtBuildDateTime.Text = $"Build: {GetBuildDateTime()}";
    }

    private async Task ReadWeldParametersAndUpdateUIAsync(bool force = false)
    {
        if (!force && !isRunning)
        {
            Log("⏸ Pomijam odczyt - pomiar zatrzymany");
            return;
        }
        if (welderService?.IsReadingConfig == true)
        {
            Log("⏸ Pomijam odczyt - trwa inna operacja komunikacji");
            return;
        }
        try
        {
            lastCommunicationTime = DateTime.Now; // Aktualizuj czas ostatniej komunikacji
            Log("=== ODCZYTUJĘ PARAMETRY ZGRZEWANIA ===");
            IncrementCommandCounter();
            var parameters = await welderService?.ReadWeldParametersAsync();
            if (parameters != null && (isRunning || force))
            {
                UpdateWeldParametersUI(parameters);
                if (isRunning) measurementHistoryNewTab.AddMeasurement(parameters);
                Log("✓ Parametry zgrzewania zostały odczytane i wyświetlone.");
            }
            else if (!isRunning && !force)
            {
                Log("⏸ Pomiar został zatrzymany podczas odczytu parametrów.");
            }
            else
            {
                Log("✗ Nie udało się odczytać parametrów zgrzewania.");
            }
        }
        catch (Exception ex)
        {
            if (isRunning || force)
            {
                Log($"Błąd podczas odczytu parametrów zgrzewania: {ex.Message}");
            }
        }
    }

    private void UpdateWeldParameters(WeldParameters parameters)
    {
        // TODO: Aktualizacja współczynników kalibracji - kontrolki są w user controls
        // Aktualizacja statystyk
        UpdateStatisticsUI();
    }

    private string FormatNoLeadingZero(double value)
    {
        string s = value.ToString("0.00", CultureInfo.InvariantCulture);
        if (s.Length > 0 && s[0] == '0')
            s = " " + s.Substring(1);
        return s;
    }

    // Wspólna metoda do sprawdzania połączenia ze zgrzewarką i automatycznego skanowania
    private async Task<bool> EnsureWelderConnectionAsync(string operationName = "operacji")
    {
        if (welderService == null) return false;

        // Sprawdź czy mamy zapisane ustawienia komunikacji
        var settings = WelderSettings.Load();
        if (string.IsNullOrEmpty(settings.CommType))
        {
            Log("Brak zapisanych ustawień komunikacji.");
            Log($"Automatycznie skanuję wszystkie urządzenia w poszukiwaniu zgrzewarki dla {operationName}...");

            // Automatycznie skanuj wszystkie urządzenia
            var scanSuccess = await welderService.ScanAllDevicesAsync();
            UpdateWelderInfo();

            if (!scanSuccess)
            {
                Log("✗ Zgrzewarka nie została znaleziona na żadnym urządzeniu.");
                Log("✗ Sprawdź połączenie ze zgrzewarką i spróbuj ponownie.");
                return false;
            }

            Log($"✓ Zgrzewarka została znaleziona! Kontynuuję {operationName}...");
        }
        else
        {
            Log("Znaleziono zapisane ustawienia komunikacji.");
        }

        // Sprawdź czy jest połączenie, jeśli nie - użyj zapisanych ustawień
        if (welderService.WelderStatus != WelderStatus.CONNECTED && welderService.WelderStatus != WelderStatus.NEW_WELDER)
        {
            Log("Brak połączenia ze zgrzewarką. Próbuję połączyć się z zapisanymi ustawieniami...");
            var runSuccess = await welderService.EnsureWelderConnectionAsync(operationName);
            UpdateWelderInfo();
            if (!runSuccess)
            {
                Log("✗ Nie udało się połączyć z zapisanymi ustawieniami.");
                Log("✗ Spróbuj ponownie skanować urządzenia.");
                return false;
            }
            Log($"✓ Połączenie z zapisanymi ustawieniami udane. Kontynuuję {operationName}.");
        }
        else
        {
            Log($"Zgrzewarka już połączona. Kontynuuję {operationName}.");
        }

        return true;
    }

    private async void btnReadWeldParams_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Przełącz na zakładkę 'Parametry zgrzewania' od razu
            SwitchToTabById(TAB_ID_WELD_PARAMETERS);
            if (welderService == null) return;
            if (!await welderService.EnsureWelderConnectionAsync("odczytu parametrów zgrzewania"))
            {
                return; // Połączenie się nie udało, przerwij
            }
            await ReadWeldParametersAndUpdateUIAsync(force: true);
        }
        catch (Exception ex)
        {
            Log($"✗ Błąd: {ex.Message}");
        }
    }

    private async void btnScanPorts_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (welderService == null) return;

            // Sprawdź czy mamy preferowane ustawienia
            var settings = WelderSettings.Load();
            string? preferredPort = null;
            int? preferredBaud = null;

            if (settings.CommType == "COM" && !string.IsNullOrEmpty(settings.COM_Port))
            {
                preferredPort = settings.COM_Port;
                preferredBaud = settings.COM_Baud;
                Log($"Używam preferowanych ustawień: {preferredPort} ({preferredBaud} baud)");
            }

            var success = await welderService.ScanComPortsAsync(preferredPort, preferredBaud);
            if (success)
            {
                Log("✓ Skanowanie portów COM zakończone pomyślnie!");
            }
            else
            {
                Log("✗ Skanowanie portów COM nie powiodło się.");
            }
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas skanowania: {ex.Message}");
        }
    }

    private void DisplayConfiguration(SKonfiguracjaSystemu config)
    {
        lastConfig = config;
        CalibrationParametersTab.SetConfiguration(config);
    }

    private async void btnReadConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Przełącz na zakładkę 'Parametry kalibracji' od razu
            SwitchToTabById(TAB_ID_CALIBRATION_PARAMETERS);

            if (welderService == null) return;
            if (!await welderService.EnsureWelderConnectionAsync("odczytu konfiguracji"))
            {
                return; // Połączenie się nie udało, przerwij
            }

            var config = await welderService.ReadConfigurationAsync();
            // Usuwam bezpośrednie wywołanie DisplayConfiguration(config)
            // Aktualizacja UI nastąpi przez event ConfigurationUpdated
        }
        catch (Exception ex)
        {
            Log($"✗ Błąd: {ex.Message}");
        }
    }

    private void btnResetStats_Click(object sender, RoutedEventArgs e)
    {
        if (welderService == null) return;
        welderService.ResetStatistics();
        UpdateStatisticsUI();
        Log("Statystyki zostały zresetowane.");
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Odczytaj wysokość logów z ustawień
        var settings = WindowSettings.Load();
        var mainGrid = (Grid)this.Content;
        if (settings.LogPanelHeight.HasValue && settings.LogPanelHeight.Value > 0)
        {
            mainGrid.RowDefinitions[2].Height = new GridLength(settings.LogPanelHeight.Value);
            Log($"Odczytano wysokość logów z ustawień: {settings.LogPanelHeight.Value:F0} px");
        }
        else
        {
            Log("Brak zapisanej wysokości logów w ustawieniach, używam domyślnej.");
        }

        // Inicjalizuj timer z poprawnym interwałem
        InitConfigTimer();

        // Inicjalizuj pasek statusu
        UpdateStatusBar();

        // Wyświetl informację o czasie buildu
        var buildTime = GetBuildDateTime();
        Log($"📦 Wersja aplikacji zbudowana: {buildTime}");

        // Wyświetl informację o skrótach klawiaturowych
        Log("🚀 Calibrator uruchomiony! Używaj klawiszy F1-F6 aby szybko przełączać zakładki.");
        Log("💡 Pomoc → Skróty klawiaturowe, aby zobaczyć wszystkie dostępne skróty.");
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Zapisz rozmiar okna tylko jeśli nie jest zmaksymalizowane
        if (this.WindowState == WindowState.Normal)
        {
            var settings = WindowSettings.Load();
            settings.WindowWidth = this.Width;
            settings.WindowHeight = this.Height;
            settings.WindowLeft = this.Left;
            settings.WindowTop = this.Top;
            settings.Save();
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        var settings = WindowSettings.Load();
        settings.WindowMaximized = (this.WindowState == WindowState.Maximized);
        settings.Save();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Zatrzymaj ProcessManager przed zamknięciem
        processManager?.Shutdown();

        // Zapisz aktualny rozmiar i stan okna przed zamknięciem
        var settings = WindowSettings.Load();

        if (this.WindowState == WindowState.Normal)
        {
            settings.WindowWidth = this.Width;
            settings.WindowHeight = this.Height;
            settings.WindowLeft = this.Left;
            settings.WindowTop = this.Top;
        }
        else
        {
            // Jeśli okno jest zmaksymalizowane, zapisz ostatni znany rozmiar normalny
            // lub użyj domyślnych wartości
            if (!settings.WindowWidth.HasValue || !settings.WindowHeight.HasValue)
            {
                settings.WindowWidth = 1200;
                settings.WindowHeight = 800;
            }
        }

        settings.WindowMaximized = (this.WindowState == WindowState.Maximized);
        settings.LogPanelVisible = logPanel.Visibility == Visibility.Visible;
        settings.Save();
    }

    private void ApplyFilter()
    {
        // Przeniesione do MeasurementHistoryTab
    }

    private void dataGridHistory_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Przeniesione do MeasurementHistoryTab
    }

    private void dataGridHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Przeniesione do MeasurementHistoryTab
    }

    private void welderChannels_Loaded(object sender, RoutedEventArgs e)
    {

    }

    private async void btnScanUSRDevices_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var success = await welderService.ScanUSRDevicesAsync();
            if (success)
            {
                Log("✓ Skanowanie urządzeń USR-N520 zakończone pomyślnie!");
            }
            else
            {
                Log("✗ Skanowanie urządzeń USR-N520 nie powiodło się.");
            }
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas skanowania: {ex.Message}");
        }
    }

    private async void btnScanAllDevices_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var success = await welderService.ScanAllDevicesAsync();
            if (success)
            {
                Log("✓ Skanowanie wszystkich urządzeń zakończone pomyślnie!");
            }
            else
            {
                Log("✗ Skanowanie wszystkich urządzeń nie powiodło się.");
            }
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas skanowania: {ex.Message}");
        }
    }

    private void btnOpenConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var configPath = WindowSettings.GetConfigFilePath();
            if (File.Exists(configPath))
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo(configPath)
                    {
                        UseShellExecute = true
                    }
                };
                process.Start();
                Log($"Otwarto plik konfiguracji: {configPath}");
            }
            else
            {
                Log($"Plik konfiguracji nie istnieje: {configPath}");
                MessageBox.Show($"Plik konfiguracji nie istnieje.\nŚcieżka: {configPath}", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas otwierania pliku konfiguracji: {ex.Message}");
            MessageBox.Show($"Nie udało się otworzyć pliku konfiguracji: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Menu kontekstowe logu
    private void menuClearLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            logPanel.ClearLog();
            logBuffer.Clear();
            currentLogLines = 0;
            // Nie logujemy informacji o wyczyszczeniu logu
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd podczas czyszczenia logu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void menuCopyLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(logPanel.GetLogText()))
            {
                Clipboard.SetText(logPanel.GetLogText() ?? "");
                // Nie logujemy informacji o skopiowaniu
            }
            // Nie logujemy gdy log jest pusty
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd podczas kopiowania logu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void menuCopySelected_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(logPanel.GetSelectedText()))
            {
                Clipboard.SetText(logPanel.GetSelectedText() ?? "");
                // Nie logujemy informacji o skopiowaniu zaznaczonego tekstu
            }
            // Nie logujemy gdy nic nie jest zaznaczone
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd podczas kopiowania zaznaczonego tekstu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void btnTcpServer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!tcpServerService.IsRunning)
            {
                string ip = CommunicationTab.ServerIp;
                int port = CommunicationTab.ServerPort;

                var success = await tcpServerService.StartAsync(ip, port);
                if (success)
                {
                    CommunicationTab.UpdateStatus($"Serwer aktywny - {tcpServerService.ConnectedClientsCount} klientów", true);
                    Log($"[TCP SERVER] Serwer uruchomiony na {ip}:{port}");
                }
                else
                {
                    CommunicationTab.UpdateStatus("Błąd uruchomienia serwera", false);
                    Log("[TCP SERVER] Błąd uruchomienia serwera");
                }
            }
            else
            {
                tcpServerService.Stop();
                CommunicationTab.UpdateStatus("Serwer nieaktywny", false);
                Log("[TCP SERVER] Serwer zatrzymany");
            }
        }
        catch (Exception ex)
        {
            Log($"[TCP SERVER] Błąd: {ex.Message}");
        }
    }

    private void OnWeldParametersUpdated(WeldParameters parameters)
    {
        try
        {
            Dispatcher.BeginInvoke(() =>
                                                          {
                                                              try
                                                              {
                                                                  // Aktualizacja UI z parametrami zgrzewania
                                                                  UpdateWeldParametersUI(parameters);
                                                                  // Aktualizacja statystyk (min/max/średnia)
                                                                  UpdateStatisticsUI();

                                                                  if (isRunning) measurementHistoryNewTab.AddMeasurement(parameters);

                                                              }
                                                              catch (Exception ex)
                                                              {
                                                                  Log($"Błąd podczas aktualizacji UI parametrów zgrzewania: {ex.Message}");
                                                              }
                                                          });
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas obsługi zdarzenia WeldParametersUpdated: {ex.Message}");
        }
    }

    private void OnConfigurationUpdated(SKonfiguracjaSystemu config)
    {
        try
        {
            Dispatcher.BeginInvoke(() =>
                                                              {
                                                                  try
                                                                  {
                                                                      // Aktualizacja UI z konfiguracją
                                                                      DisplayConfiguration(config);
                                                                  }
                                                                  catch (Exception ex)
                                                                  {
                                                                      Log($"Błąd podczas aktualizacji UI konfiguracji: {ex.Message}");
                                                                  }
                                                              });
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas obsługi zdarzenia ConfigurationUpdated: {ex.Message}");
        }
    }

    private void OnWelderStatusChanged(WelderStatus status)
    {
        try
        {
            Dispatcher.BeginInvoke(() =>
                                                                  {
                                                                      try
                                                                      {
                                                                          // Aktualizacja UI ze statusem zgrzewarki
                                                                          UpdateWelderStatusUI(status);
                                                                      }
                                                                      catch (Exception ex)
                                                                      {
                                                                          Log($"Błąd podczas aktualizacji UI statusu zgrzewarki: {ex.Message}");
                                                                      }
                                                                  });
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas obsługi zdarzenia WelderStatusChanged: {ex.Message}");
        }
    }

    private void OnHistoryUpdated(List<WelderService.CalibrationRecord> history)
    {
        Dispatcher.BeginInvoke(() =>
                                                                      {
                                                                          MeasurementHistoryTab.SetHistory(history);
                                                                      });
    }

    private void UpdateStatisticsUI()
    {
        if (welderService != null)
        {
            WeldParametersTab.UpdateStatistics(welderService);
        }
    }

    private void UpdateWeldParametersUI(WeldParameters parameters)
    {
        WeldParametersTab.UpdateWeldParameters(parameters);
    }

    private void UpdateWelderStatusUI(WelderStatus status)
    {
        // Aktualizacja statusu zgrzewarki
        txtStatus.Text = GetStatusDescription(status);

        // Aktualizacja przycisku RUN
        if (status == WelderStatus.CONNECTED)
        {
            btnRun.IsEnabled = true;
        }
        else
        {
            btnRun.IsEnabled = false;
        }
    }

    private WartosciKanalowZgrzewarki MapujWartosciKanalowZgrzewarki(CalibrationReportLib.SKonfiguracjaSystemu konf)
    {
        return new WartosciKanalowZgrzewarki
        {
            MMWVH = konf.uMultimeterWeldVoltageHighCurrent,
            MMWVL = konf.uMultimeterWeldVoltageLowCurrent,
            IVHC_U = konf.uInputVoltageHighCurrent.Length > 5 ? konf.uInputVoltageHighCurrent[5] : 0,
            IVLC_U = konf.uInputVoltageLowCurrent.Length > 5 ? konf.uInputVoltageLowCurrent[5] : 0,
            ADCIVHC_U = konf.uADCValueHighCurrent.Length > 5 ? konf.uADCValueHighCurrent[5] : 0,
            ADCIVLC_U = konf.uADCValueLowCurrent.Length > 5 ? konf.uADCValueLowCurrent[5] : 0,
            MMWCL = konf.uMultimeterWeldCurrentLowCurrent,
            MMWCH = konf.uMultimeterWeldCurrentHighCurrent,
            IVHC_I = konf.uInputVoltageHighCurrent.Length > 6 ? konf.uInputVoltageHighCurrent[6] : 0,
            IVLC_I = konf.uInputVoltageLowCurrent.Length > 6 ? konf.uInputVoltageLowCurrent[6] : 0,
            ADCIVHC_I = konf.uADCValueHighCurrent.Length > 6 ? konf.uADCValueHighCurrent[6] : 0,
            ADCIVLC_I = konf.uADCValueLowCurrent.Length > 6 ? konf.uADCValueLowCurrent[6] : 0
        };
    }

    private string FormatDate(byte[] date)
    {
        // Przeniesione do CalibrationParametersTab
        return "";
    }

    // 1. Handler do czyszczenia pliku logu
    private void menuClearLogFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = MessageBox.Show(
            "Czy na pewno chcesz trwale usunąć cały plik logu (log.txt)?\nTej operacji nie można cofnąć.",
            "Potwierdzenie",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var logPath = "log.txt";
                if (File.Exists(logPath))
                {
                    File.Delete(logPath);
                }
                logPanel.ClearLog();
                LoggerService.Instance.LoadLogHistory();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd podczas usuwania pliku logu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ConfigService event handlers
    private void OnConfigSettingsChanged(WelderSettings settings)
    {
        Dispatcher.Invoke(() =>
                                                                                                          {
                                                                                                              Log($"Configuration settings changed: {settings.CommType}");
                                                                                                              // Update UI elements that depend on configuration
                                                                                                              UpdateWelderInfo();
                                                                                                          });
    }

    private void OnConfigDetectedPortsChanged(List<DetectedPort> ports)
    {
        Dispatcher.Invoke(() =>
                                                                                                                  {
                                                                                                                      Log($"Detected ports updated: {ports.Count} ports");
                                                                                                                      // Update UI elements that show detected ports
                                                                                                                      // This could update a list of available ports in the UI
                                                                                                                  });
    }

    private void Log(string message)
    {
        LoggerService.Log(message);
    }

    private void OnTcpClientConnected(TcpClient client)
    {
        Dispatcher.BeginInvoke(() =>
                                                                                                                      {
                                                                                                                          CommunicationTab.UpdateStatus($"Serwer aktywny - {tcpServerService.ConnectedClientsCount} klientów", true);
                                                                                                                      });
    }

    private void OnTcpClientDisconnected(TcpClient client)
    {
        Dispatcher.BeginInvoke(() =>
                                                                                                                          {
                                                                                                                              if (tcpServerService.ConnectedClientsCount > 0)
                                                                                                                              {
                                                                                                                                  CommunicationTab.UpdateStatus($"Serwer aktywny - {tcpServerService.ConnectedClientsCount} klientów", true);
                                                                                                                              }
                                                                                                                              else
                                                                                                                              {
                                                                                                                                  CommunicationTab.UpdateStatus("Serwer aktywny - brak klientów", true);
                                                                                                                              }
                                                                                                                          });
    }





    /// <summary>
    /// Przełącza na zakładkę INFO
    /// </summary>
    private void SwitchToInfoTab()
    {
        SwitchToTabById(TAB_ID_INFO);
    }

    /// <summary>
    /// Przełącza na zakładkę na podstawie identyfikatora (niezależne od języka)
    /// </summary>
    private void SwitchToTabById(string tabId)
    {
        try
        {
            // Find the tab with the specified tag (more reliable than header text)
            foreach (TabItem tab in mainTabControl.Items)
            {
                if (tab.Tag?.ToString() == tabId)
                {
                    mainTabControl.SelectedItem = tab;

                    // Get the F-key number for display
                    string fKeyNumber = GetFKeyForTag(tabId);
                    string displayName = GetDisplayNameForTag(tabId);
                    Log($"🔄 Przełączono na zakładkę: {displayName} ({fKeyNumber})");
                    return;
                }
            }

            // Jeśli nie znaleziono zakładki, zaloguj błąd
            Log($"⚠ Nie znaleziono zakładki o ID: {tabId}");
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas przełączania na zakładkę {tabId}: {ex.Message}");
        }
    }

    private void measurementHistoryNewTab_Loaded(object sender, RoutedEventArgs e)
    {
        // Loaded event for measurement history tab
    }

    /// <summary>
    /// Event handler for setting measurement interval from menu
    /// </summary>
    private void SetInterval_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.Tag != null)
            {
                string tagValue = menuItem.Tag.ToString();

                // Find the corresponding combo box item and select it
                foreach (ComboBoxItem item in comboInterval.Items)
                {
                    if (item.Tag?.ToString() == tagValue)
                    {
                        comboInterval.SelectedItem = item;
                        Log($"Interwał próbkowania zmieniony na: {item.Content}");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas zmiany interwału: {ex.Message}");
        }
    }

    /// <summary>
    /// Event handler for About dialog
    /// </summary>
    private void ShowAbout_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
            string message = $"Calibrator\n\n" +
            $"Wersja: {version}\n" +
            $"Framework: .NET 9.0\n\n" +
            $"Aplikacja do kalibracji zgrzewarek\n" +
            $"© 2024";

            MessageBox.Show(message, "O programie", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas wyświetlania okna 'O programie': {ex.Message}");
        }
    }

    /// <summary>
    /// Event handler for Exit menu item
    /// </summary>
    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            this.Close();
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas zamykania aplikacji: {ex.Message}");
        }
    }

    /// <summary>
    /// Event handler for F-key shortcuts to switch between tabs
    /// </summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            // Handle F1-F6 keys for tab switching using tag IDs
            switch (e.Key)
            {
                case Key.F1:
                    SwitchToTabById(TAB_ID_WELD_PARAMETERS);
                    e.Handled = true;
                    break;
                case Key.F2:
                    SwitchToTabById(TAB_ID_CALIBRATION_PARAMETERS);
                    e.Handled = true;
                    break;
                case Key.F3:
                    SwitchToTabById(TAB_ID_MEASUREMENT_HISTORY);
                    e.Handled = true;
                    break;
                case Key.F4:
                    SwitchToTabById(TAB_ID_MEASUREMENT_HISTORY_NEW);
                    e.Handled = true;
                    break;
                case Key.F5:
                    SwitchToTabById(TAB_ID_INFO);
                    e.Handled = true;
                    break;
                case Key.F6:
                    SwitchToTabById(TAB_ID_COMMUNICATION);
                    e.Handled = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas przełączania zakładki klawiszem skrótu: {ex.Message}");
        }
    }

    /// <summary>
    /// Get F-key name for a given tag
    /// </summary>
    private string GetFKeyForTag(string tagName)
    {
        return tagName switch
        {
            TAB_ID_WELD_PARAMETERS => "F1",
            TAB_ID_CALIBRATION_PARAMETERS => "F2",
            TAB_ID_MEASUREMENT_HISTORY => "F3",
            TAB_ID_MEASUREMENT_HISTORY_NEW => "F4",
            TAB_ID_INFO => "F5",
            TAB_ID_COMMUNICATION => "F6",
            _ => "F?"
        };
    }

    /// <summary>
    /// Get display name for a given tag
    /// </summary>
    private string GetDisplayNameForTag(string tagName)
    {
        return tagName switch
        {
            TAB_ID_WELD_PARAMETERS => "Parametry zgrzewania",
            TAB_ID_CALIBRATION_PARAMETERS => "Parametry kalibracji",
            TAB_ID_MEASUREMENT_HISTORY => "Historia kalibracji",
            TAB_ID_MEASUREMENT_HISTORY_NEW => "Historia pomiarów",
            TAB_ID_INFO => "INFO",
            TAB_ID_COMMUNICATION => "Komunikacja",
            _ => tagName
        };
    }

    /// <summary>
    /// Event handler for Keyboard Shortcuts dialog
    /// </summary>
    private void ShowKeyboardShortcuts_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string shortcuts = "Skróty klawiaturowe Calibrator\n\n" +
            "ZAKŁADKI:\n" +
            "F1 - Parametry zgrzewania\n" +
            "F2 - Parametry kalibracji\n" +
            "F3 - Historia kalibracji\n" +
            "F4 - Historia pomiarów\n" +
            "F5 - INFO\n" +
            "F6 - Komunikacja\n\n" +
            "MENU:\n" +
            "Alt + P - Menu Plik\n" +
            "Alt + O - Menu Połączenie\n" +
            "Alt + K - Menu Kalibracja\n" +
            "Alt + T - Menu Kontrola\n" +
            "Alt + W - Menu Widok\n" +
            "Alt + M - Menu Pomoc\n\n" +
            "Naciśnij odpowiedni klawisz F, aby szybko przełączyć się między zakładkami!";

            MessageBox.Show(shortcuts, "Skróty klawiaturowe", MessageBoxButton.OK, MessageBoxImage.Information);
            Log("ℹ Wyświetlono okno ze skrótami klawiaturowymi");
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas wyświetlania skrótów klawiaturowych: {ex.Message}");
        }
    }

    private void btnToggleLogPanel_Click(object sender, RoutedEventArgs e)
    {
        var mainGrid = (Grid)this.Content;
        var settings = WindowSettings.Load();
        if (logPanel.Visibility == Visibility.Visible)
        {
            lastLogPanelHeight = mainGrid.RowDefinitions[2].Height.Value;
            logPanel.Visibility = Visibility.Collapsed;
            logSplitter.Visibility = Visibility.Collapsed;
            mainGrid.RowDefinitions[2].Height = new GridLength(0);
            settings.LogPanelVisible = false;
        }
        else
        {
            logPanel.Visibility = Visibility.Visible;
            logSplitter.Visibility = Visibility.Visible;
            double height = lastLogPanelHeight.HasValue && lastLogPanelHeight.Value > 0 ? lastLogPanelHeight.Value : 180;
            mainGrid.RowDefinitions[2].Height = new GridLength(height);
            settings.LogPanelVisible = true;
        }
        settings.Save();
    }

    private void weldParametersTab_Loaded(object sender, RoutedEventArgs e)
    {

    }
}