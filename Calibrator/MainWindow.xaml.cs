using System;
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
using Logger;
using static Logger.LoggerService;

namespace Calibrator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // Struktura pomocnicza do mapowania wartości kanałów zgrzewarki
    private class WartosciKanalowZgrzewarki
    {
        public int MMWVH, MMWVL, IVHC_U, IVLC_U, ADCIVHC_U, ADCIVLC_U;
        public int MMWCL, MMWCH, IVHC_I, IVLC_I, ADCIVHC_I, ADCIVLC_I;
    }

    private readonly WelderService welderService;
    private readonly System.Windows.Threading.DispatcherTimer configTimer;
    private bool isRunning = false;

    private bool logPanelCollapsed = false;
    private double lastLogPanelHeight = 150;

    // TCP Server variables
    private TcpListener? tcpServer;
    private CancellationTokenSource? tcpServerCts;
    private bool isTcpServerRunning = false;
    private readonly ConcurrentBag<TcpClient> tcpClients = new ConcurrentBag<TcpClient>();

    private SKonfiguracjaSystemu? lastConfig;

    public MainWindow()
    {
        InitializeComponent();

        // Pobierz serwis z kontenera DI
        welderService = ServiceContainer.WelderService;

        // Ustaw LabelFormatter dla gaugeNapiecie i gaugePrad
        gaugeNapiecie.LabelFormatter = value => value.ToString("F1");
        gaugePrad.LabelFormatter = value => value.ToString("F1");

        // Podłącz eventy serwisu do UI
        // welderService.LogMessage += Log; // Usunięte - teraz logowanie przez LoggerService
        welderService.WeldParametersUpdated += OnWeldParametersUpdated;
        welderService.ConfigurationUpdated += OnConfigurationUpdated;
        welderService.WelderStatusChanged += OnWelderStatusChanged;
        welderService.HistoryUpdated += OnHistoryUpdated;

        // Przywracanie rozmiaru i stanu okna przed wyświetleniem
        var settings = WelderSettings.Load();
        if (settings.WindowWidth.HasValue && settings.WindowHeight.HasValue &&
            settings.WindowWidth.Value > 0 && settings.WindowHeight.Value > 0)
        {
            // Sprawdź rozmiar ekranu przed ustawieniem rozmiaru okna
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            // Ustaw rozmiar okna, ale nie przekraczaj rozmiaru ekranu
            double windowWidth = Math.Min(settings.WindowWidth.Value, screenWidth);
            double windowHeight = Math.Min(settings.WindowHeight.Value, screenHeight);

            // Dodatkowo sprawdź czy okno nie jest za małe (minimum 800x600)
            windowWidth = Math.Max(windowWidth, 800);
            windowHeight = Math.Max(windowHeight, 600);

            this.Width = windowWidth;
            this.Height = windowHeight;

            // Sprawdź i ustaw pozycję okna
            if (settings.WindowLeft.HasValue && settings.WindowTop.HasValue)
            {
                double windowLeft = settings.WindowLeft.Value;
                double windowTop = settings.WindowTop.Value;

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
                if (windowLeft != settings.WindowLeft.Value || windowTop != settings.WindowTop.Value)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        Log($"Dostosowano pozycję okna do rozmiaru ekranu: {screenWidth}x{screenHeight}");
                        Log($"Zapisana pozycja: {settings.WindowLeft.Value},{settings.WindowTop.Value}");
                        Log($"Ustawiona pozycja: {windowLeft},{windowTop}");
                    });
                }
            }

            // Logowanie informacji o dostosowaniu rozmiaru
            if (windowWidth != settings.WindowWidth.Value || windowHeight != settings.WindowHeight.Value)
            {
                // Użyj Dispatcher.BeginInvoke aby logowanie nastąpiło po inicjalizacji komponentów
                Dispatcher.BeginInvoke(() =>
                {
                    Log($"Dostosowano rozmiar okna do rozmiaru ekranu: {screenWidth}x{screenHeight}");
                    Log($"Zapisany rozmiar: {settings.WindowWidth.Value}x{settings.WindowHeight.Value}");
                    Log($"Ustawiony rozmiar: {windowWidth}x{windowHeight}");
                });
            }
        }
        if (settings.WindowMaximized.HasValue)
        {
            this.WindowState = settings.WindowMaximized.Value ? WindowState.Maximized : WindowState.Normal;
        }

        configTimer = new System.Windows.Threading.DispatcherTimer();
        configTimer.Tick += ConfigTimer_Tick;

        // Inicjalizacja historii pomiarów
        dataGridHistory.ItemsSource = welderService.CalibrationHistory;

        // Inicjalizacja filtrowania
        ApplyFilter();

        // Wyświetl IP komputera w pasku tytułowym
        UpdateTitleWithLocalIP();

        // Subskrybuj logi z LoggerService
        LoggerService.Instance.LogMessageAppended += AppendLogToUI;
        LoggerService.Instance.LogHistoryLoaded += LoadLogHistoryToUI;
        LoggerService.Instance.LoadLogHistory();

        // 3. Numeracja wierszy historii pomiarów
        // Dodaj event do DataGrid:
        dataGridHistory.LoadingRow += dataGridHistory_LoadingRow;
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
        if (!isRunning)
        {
            Log("=== ROZPOCZYNAM RUN ===");

            if (!await EnsureWelderConnectionAsync("funkcji RUN"))
            {
                return; // Połączenie się nie udało, przerwij
            }

            Log("✓ Połączenie udane! Uruchamiam timer odczytu parametrów zgrzewania...");

            configTimer.Interval = TimeSpan.FromMilliseconds(GetSelectedInterval());
            configTimer.Start();
            iconRun.Text = "⏹";
            txtRun.Text = "STOP";
            btnRun.Background = new SolidColorBrush(Colors.Red);
            btnRun.Foreground = Brushes.White;
            isRunning = true;
        }
        else
        {
            Log("=== ZATRZYMUJĘ RUN ===");
            configTimer.Stop();
            iconRun.Text = "▶";
            txtRun.Text = "RUN";
            btnRun.Background = new SolidColorBrush(Colors.Green);
            btnRun.Foreground = Brushes.White;
            isRunning = false;
        }
    }

    private void ConfigTimer_Tick(object? sender, EventArgs e)
    {
        _ = ReadWeldParametersAndUpdateUIAsync();
    }

    private int GetSelectedInterval()
    {
        if (comboInterval.SelectedItem is System.Windows.Controls.ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int ms))
            return ms;
        return 1000; // default 1s
    }

    private async Task ReadConfigAndUpdateUIAsync()
    {
        if (welderService.IsReadingConfig) return;

        try
        {
            Log("=== ODCZYTUJĘ KONFIGURACJĘ SYSTEMU ===");

            var config = await welderService.ReadConfigurationAsync();
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
        var status = welderService.WelderStatus;
        var statusDescription = GetStatusDescription(status);
        txtStatus.Text = statusDescription;

        // Aktualizacja koloru statusu
        switch (status)
        {
            case WelderStatus.CONNECTED:
            case WelderStatus.NEW_WELDER:
                txtStatus.Foreground = Brushes.Green;
                break;
            case WelderStatus.NO_CONNECTION:
                txtStatus.Foreground = Brushes.Red;
                break;
            default:
                txtStatus.Foreground = Brushes.Orange;
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
        txtLog.AppendText(message + "\n");
        txtLog.ScrollToEnd();
    }

    private void LoadLogHistoryToUI(IReadOnlyList<string> history)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => LoadLogHistoryToUI(history));
            return;
        }
        txtLog.Clear();
        foreach (var line in history)
        {
            txtLog.AppendText(line + "\n");
        }
        txtLog.ScrollToEnd();
    }

    private async Task ReadWeldParametersAndUpdateUIAsync()
    {
        try
        {
            Log("=== ODCZYTUJĘ PARAMETRY ZGRZEWANIA ===");

            var parameters = await welderService.ReadWeldParametersAsync();
            if (parameters != null)
            {
                UpdateWeldParametersUI(parameters);
                Log("✓ Parametry zgrzewania zostały odczytane i wyświetlone.");
            }
            else
            {
                Log("✗ Nie udało się odczytać parametrów zgrzewania.");
            }
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas odczytu parametrów zgrzewania: {ex.Message}");
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
            // Przełącz na zakładkę 'Parametry zgrzewania' (pierwsza zakładka, indeks 0) od razu
            mainTabControl.SelectedIndex = 0;

            if (!await welderService.EnsureWelderConnectionAsync("odczytu parametrów zgrzewania"))
            {
                return; // Połączenie się nie udało, przerwij
            }

            // Wywołaj bezpośrednio aktualizację UI jak w poprzednim commicie
            await ReadWeldParametersAndUpdateUIAsync();
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
        try
        {
            welderChannels.SetConfiguration(config);
            kanalyZgrzewarkiVoltage.SetConfiguration(config);
            kanalyZgrzewarkiCurrent.SetConfiguration(config);

            // Pozostałe parametry (Tab 2)
            txtTyp.Text = config.Typ.ToString();
            txtKeypadSE.Text = config.KeypadSE.ToString();
            txtNrJezyka.Text = config.nrJezyka.ToString();
            txtNazwaZgrzewarki.Text = WelderRS232.Welder.GetWelderName(config.NazwaZgrzewarki);
            txtNumerSeryjny.Text = Encoding.ASCII.GetString(config.NumerSeryjny).TrimEnd('\0');
            txtDaneWlasciciela0.Text = Encoding.ASCII.GetString(config.DaneWlasciciela0).TrimEnd('\0');
            txtDaneWlasciciela1.Text = Encoding.ASCII.GetString(config.DaneWlasciciela1).TrimEnd('\0');
            txtDaneWlasciciela2.Text = Encoding.ASCII.GetString(config.DaneWlasciciela2).TrimEnd('\0');
            txtDataSprzedazy.Text = FormatDate(config.DataSprzedazy);
            txtDataPierwszegoZgrzewu.Text = FormatDate(config.DataPierwszegoZgrzewu);
            txtDataOstatniejKalibracji.Text = FormatDate(config.DataOstatniejKalibracji);
            txtOffsetMCP3425.Text = config.Offset_MCP3425.ToString();
            txtWolneMiejsce.Text = BitConverter.ToString(config.WolneMiejsce);
            txtLiczbaZgrzOstKalibr.Text = config.LiczbaZgrzOstKalibr.ToString();
            txtOkresKalibracji.Text = config.OkresKalibracji.ToString();
            txtRejestrKonfiguracji.Text = config.RejestrKonfiguracji.ToString();
            txtRejestrKonfiguracjiBankTwo.Text = config.RejestrKonfiguracjiBankTwo.ToString();
            txtTempOtRefVal.Text = config.TempOtRefVal.ToString();
            txtTempOtRefADC.Text = config.TempOtRefADC.ToString();
            txtKorekcjaTempWewn.Text = config.KorekcjaTempWewn.ToString();
            txtKorekcjaTempZewn.Text = config.KorekcjaTempZewn.ToString();
            txtKodBlokady.Text = config.KodBlokady.ToString();
            txtTypBlokady.Text = config.TypBlokady.ToString();
            txtGPSconfiguration.Text = config.GPSconfiguration.ToString();

            Log("Konfiguracja została odczytana i wyświetlona w UI.");
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas wyświetlania konfiguracji: {ex.Message}");
        }
    }

    private async void btnReadConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Przełącz na zakładkę 'Parametry kalibracji' (druga zakładka, indeks 1) od razu
            mainTabControl.SelectedIndex = 1;

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
        welderService.ResetStatistics();
        UpdateStatisticsUI();
        Log("Statystyki zostały zresetowane.");
    }

    private void btnToggleLogPanel_Click(object sender, RoutedEventArgs e)
    {
        if (logPanelCollapsed)
        {
            // Rozwiń panel logów
            LogPanel.Height = lastLogPanelHeight;
            txtToggleLogIcon.Text = "▼";
            logPanelCollapsed = false;
        }
        else
        {
            // Zwiń panel logów
            lastLogPanelHeight = LogPanel.Height;
            LogPanel.Height = 0;
            txtToggleLogIcon.Text = "▲";
            logPanelCollapsed = true;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Odczytaj wysokość logów z ustawień
        var settings = WelderSettings.Load();
        var mainGrid = (Grid)this.Content;
        if (settings.LogPanelHeight.HasValue && settings.LogPanelHeight.Value > 0)
        {
            mainGrid.RowDefinitions[2].Height = new GridLength(settings.LogPanelHeight.Value);
            lastLogPanelHeight = settings.LogPanelHeight.Value;
            Log($"Odczytano wysokość logów z ustawień: {settings.LogPanelHeight.Value:F0} px");
        }
        else
        {
            Log("Brak zapisanej wysokości logów w ustawieniach, używam domyślnej.");
        }

        // Inicjalizacja WebView2 z treścią HTML
        InitializeInfoWebView();
    }

    private async void InitializeInfoWebView()
    {
        try
        {
            await InfoWebView.EnsureCoreWebView2Async();

            // Wczytaj HTML z pliku
            string htmlFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "InfoContent.html");
            if (File.Exists(htmlFilePath))
            {
                string htmlContent = await File.ReadAllTextAsync(htmlFilePath);
                InfoWebView.NavigateToString(htmlContent);
            }
            else
            {
                Log($"Plik HTML nie został znaleziony: {htmlFilePath}");
            }
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas inicjalizacji WebView2: {ex.Message}");
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Zapisz rozmiar okna tylko jeśli nie jest zmaksymalizowane
        if (this.WindowState == WindowState.Normal)
        {
            var settings = WelderSettings.Load();
            settings.WindowWidth = this.Width;
            settings.WindowHeight = this.Height;
            settings.WindowLeft = this.Left;
            settings.WindowTop = this.Top;
            settings.Save();
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        var settings = WelderSettings.Load();
        settings.WindowMaximized = (this.WindowState == WindowState.Maximized);
        settings.Save();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Zapisz aktualny rozmiar i stan okna przed zamknięciem
        var settings = WelderSettings.Load();

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
        settings.Save();
    }

    private void LogPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!logPanelCollapsed && e.HeightChanged)
        {
            var settings = WelderSettings.Load();
            double newHeight = LogPanel.RowDefinitions[1].ActualHeight;
            settings.LogPanelHeight = newHeight;
            settings.Save();
            Log($"Zapisano wysokość logów: {newHeight:F0} px do ustawień.");
        }
    }

    private void LogPanelSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        var newHeight = LogPanel.ActualHeight;
        lastLogPanelHeight = newHeight;
        var settings = WelderSettings.Load();
        settings.LogPanelHeight = newHeight;
        settings.Save();
        Log($"[Splitter] Zapisano wysokość logów: {newHeight:F0} px do ustawień.");
    }

    private void btnSaveCalibration_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = GetCurrentConfigurationFromUI();
            if (config == null)
            {
                MessageBox.Show("Brak danych konfiguracyjnych do zapisania. Najpierw odczytaj konfigurację ze zgrzewarki.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Walidacja kluczowych pól (przykład, możesz dodać więcej)
            if (config.uInputVoltageHighCurrent == null || config.uInputVoltageHighCurrent.Length < 7)
            {
                MessageBox.Show("Konfiguracja jest niekompletna. Najpierw odczytaj konfigurację ze zgrzewarki.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string deviceType = txtNazwaZgrzewarki.Text;
            string serialNumber = txtNumerSeryjny.Text;

            welderService.SaveCalibrationToHistory(config, deviceType, serialNumber);
            Log("✓ Kalibracja została zapisana do historii.");
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas zapisywania kalibracji: {ex.Message}");
        }
    }

    private void btnRefreshHistory_Click(object sender, RoutedEventArgs e)
    {
        welderService.RefreshHistory();
    }

    private void btnClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Czy na pewno chcesz wyczyścić całą historię pomiarów?",
            "Potwierdzenie",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            welderService.ClearHistory();
            dataGridHistory.ItemsSource = null;
            dataGridHistory.ItemsSource = welderService.CalibrationHistory;
        }
    }

    private SKonfiguracjaSystemu? GetCurrentConfigurationFromUI()
    {
        return lastConfig;
    }

    private void btnOpenFileHistory_Click(object sender, RoutedEventArgs e)
    {
        var historyFilePath = "calibration_history.xml";
        if (File.Exists(historyFilePath))
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo(historyFilePath)
                    {
                        UseShellExecute = true
                    }
                };
                process.Start();
            }
            catch (Exception ex)
            {
                Log($"Nie udało się otworzyć pliku historii: {ex.Message}");
                MessageBox.Show($"Nie udało się otworzyć pliku. Możesz go znaleźć tutaj: {System.IO.Path.GetFullPath(historyFilePath)}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            MessageBox.Show("Plik historii jeszcze nie istnieje.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void btnToggleDetails_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryDetailsPanel.Visibility == Visibility.Collapsed)
        {
            HistoryDetailsPanel.Visibility = Visibility.Visible;
        }
        else
        {
            HistoryDetailsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void btnClearFilter_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Wyczyść filtry
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        // TODO: Zastosuj filtry do historii
        // var filteredHistory = welderService.GetFilteredHistory(deviceTypeFilter, serialNumberFilter);
        string deviceTypeFilter = txtFilterDeviceType.Text?.Trim() ?? "";
        string serialNumberFilter = txtFilterSerialNumber.Text?.Trim() ?? "";

        var filteredHistory = welderService.GetFilteredHistory(deviceTypeFilter, serialNumberFilter);
        dataGridHistory.ItemsSource = filteredHistory;
    }

    private void dataGridHistory_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            if (dataGridHistory.SelectedItem is WelderService.CalibrationRecord selectedRecord)
            {
                // TODO: Implementuj kopiowanie wartości do UI
                Log($"Wybrano rekord z historii: {selectedRecord.DateTime}");
            }
        }
        catch (Exception ex)
        {
            Log($"Błąd podczas ładowania danych z historii: {ex.Message}");
        }
    }

    private void dataGridHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dataGridHistory.SelectedItem is WelderService.CalibrationRecord selectedRecord)
        {
            // TODO: Implementuj aktualizację UI z wybranym rekordem
            Log($"Wybrano rekord: {selectedRecord.DateTime}");
        }
        else
        {
            // TODO: Wyczyść szczegóły, jeśli nic nie jest zaznaczone
        }
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
            var configPath = WelderSettings.GetConfigFilePath();
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
            txtLog.Clear();
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
            if (!string.IsNullOrEmpty(txtLog.Text))
            {
                Clipboard.SetText(txtLog.Text);
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
            if (!string.IsNullOrEmpty(txtLog.SelectedText))
            {
                Clipboard.SetText(txtLog.SelectedText);
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
        if (!isTcpServerRunning)
        {
            // Start serwera
            string ip = txtTcpServerIp.Text.Trim();
            int port = int.TryParse(txtTcpServerPort.Text.Trim(), out int p) ? p : 20108;
            try
            {
                tcpServerCts = new CancellationTokenSource();
                tcpServer = new TcpListener(IPAddress.Parse(ip), port);
                tcpServer.Start();
                isTcpServerRunning = true;
                btnTcpServer.Content = "Zatrzymaj serwer TCP";
                Log($"[TCP SERVER] Nasłuchuję na {ip}:{port}");
                _ = Task.Run(() => AcceptTcpClientsAsync(tcpServer, tcpServerCts.Token));
            }
            catch (Exception ex)
            {
                Log($"[TCP SERVER] Błąd uruchamiania: {ex.Message}");
                isTcpServerRunning = false;
                btnTcpServer.Content = "Uruchom serwer TCP";
            }
        }
        else
        {
            // Stop serwera
            try
            {
                tcpServerCts?.Cancel();
                tcpServer?.Stop();
                isTcpServerRunning = false;
                btnTcpServer.Content = "Uruchom serwer TCP";
                Log("[TCP SERVER] Serwer zatrzymany.");
            }
            catch (Exception ex)
            {
                Log($"[TCP SERVER] Błąd zatrzymywania: {ex.Message}");
            }
        }
    }

    private async Task AcceptTcpClientsAsync(TcpListener listener, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync();
                tcpClients.Add(client);
                _ = Task.Run(() => HandleTcpClientAsync(client, token));
            }
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            Log($"[TCP SERVER] Błąd AcceptTcpClients: {ex.Message}");
        }
    }

    private async Task HandleTcpClientAsync(TcpClient client, CancellationToken token)
    {
        var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "?";
        Log($"[TCP SERVER] Połączono z {endpoint}");
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                byte[] buffer = new byte[4096];
                while (!token.IsCancellationRequested)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead == 0) break; // rozłączono
                    string data = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Log($"[TCP SERVER] Otrzymano od {endpoint}: {data}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[TCP SERVER] Błąd klienta {endpoint}: {ex.Message}");
        }
        Log($"[TCP SERVER] Rozłączono {endpoint}");
    }

    private async void btnSendSampleData_Click(object sender, RoutedEventArgs e)
    {
        if (!isTcpServerRunning)
        {
            Log("[TCP SERVER] Serwer nie jest uruchomiony.");
            return;
        }
        if (tcpClients.IsEmpty)
        {
            Log("[TCP SERVER] Brak podłączonych klientów do wysłania danych.");
            return;
        }
        string sample = $"Przykładowe dane {DateTime.Now:HH:mm:ss}";
        byte[] data = System.Text.Encoding.UTF8.GetBytes(sample);
        int sent = 0;
        foreach (var client in tcpClients)
        {
            try
            {
                if (client.Connected)
                {
                    await client.GetStream().WriteAsync(data, 0, data.Length);
                    sent++;
                }
            }
            catch (Exception ex)
            {
                Log($"[TCP SERVER] Błąd wysyłania do klienta: {ex.Message}");
            }
        }
        Log($"[TCP SERVER] Wysłano przykładowe dane do {sent} klient(ów).");
    }

    private void OnWeldParametersUpdated(WeldParameters parameters)
    {
        Dispatcher.Invoke(() =>
        {
            // Aktualizacja UI z parametrami zgrzewania
            UpdateWeldParametersUI(parameters);
        });
    }

    private void OnConfigurationUpdated(SKonfiguracjaSystemu config)
    {
        Dispatcher.Invoke(() =>
        {
            // Aktualizacja UI z konfiguracją
            DisplayConfiguration(config);
        });
    }

    private void OnWelderStatusChanged(WelderStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            // Aktualizacja UI ze statusem zgrzewarki
            UpdateWelderStatusUI(status);
        });
    }

    private void OnHistoryUpdated(List<WelderService.CalibrationRecord> history)
    {
        Dispatcher.Invoke(() =>
        {
            // Aktualizacja UI z historią
            dataGridHistory.ItemsSource = history;
        });
    }

    private void UpdateStatisticsUI()
    {
        txtNapiecieMin.Text = welderService.NapiecieMin.ToString("F2");
        txtNapiecieMax.Text = welderService.NapiecieMax.ToString("F2");
        txtNapiecieAvr.Text = welderService.NapiecieAverage.ToString("F2");
        txtPradMin.Text = welderService.PradMin.ToString("F2");
        txtPradMax.Text = welderService.PradMax.ToString("F2");
        txtPradAvr.Text = welderService.PradAverage.ToString("F2");
    }

    private void UpdateWeldParametersUI(WeldParameters parameters)
    {
        // Aktualizacja UI z parametrami zgrzewania
        txtNapiecieZgrzewania.Text = parameters.NapiecieZgrzewania.ToString("F2");
        txtPradZgrzewania.Text = parameters.PradZgrzewania.ToString("F2");
        txtADCNapZgrzew.Text = $"0x{parameters.ADCNapZgrzew:X4}";
        txtADCPradZgrzew.Text = $"0x{parameters.ADCPradZgrzew:X4}";

        // Aktualizacja Gauge
        gaugeNapiecie.Value = parameters.NapiecieZgrzewania;
        gaugePrad.Value = parameters.PradZgrzewania;

        // Aktualizacja współczynników kalibracji - tylko na karcie 'Parametry zgrzewania'
        wspZgrzewaniaVoltage.MMWVHValue = parameters.MMWVH.ToString();
        wspZgrzewaniaVoltage.MMWVLValue = parameters.MMWVL.ToString();
        wspZgrzewaniaVoltage.IVHC_U_Value = parameters.IVHC_U.ToString();
        wspZgrzewaniaVoltage.IVLC_U_Value = parameters.IVLC_U.ToString();
        wspZgrzewaniaVoltage.ADCIVHC_U_Value = parameters.ADCIVHC_U.ToString();
        wspZgrzewaniaVoltage.ADCIVLC_U_Value = parameters.ADCIVLC_U.ToString();

        wspZgrzewaniaCurrent.MMWCHValue = parameters.MMWCH.ToString();
        wspZgrzewaniaCurrent.MMWCLValue = parameters.MMWCL.ToString();
        wspZgrzewaniaCurrent.IVHC_I_Value = parameters.IMHC_I.ToString();
        wspZgrzewaniaCurrent.IVLC_I_Value = parameters.IMLC_I.ToString();
        wspZgrzewaniaCurrent.ADCIVHC_I_Value = parameters.ADCIVHC_I.ToString();
        wspZgrzewaniaCurrent.ADCIVLC_I_Value = parameters.ADCIVLC_I.ToString();

        // NIE aktualizuję kanalyZgrzewarkiVoltage ani kanalyZgrzewarkiCurrent!

        // Aktualizacja statystyk
        UpdateStatisticsUI();
    }

    private void UpdateWelderStatusUI(WelderStatus status)
    {
        // Aktualizacja UI ze statusem zgrzewarki
        var statusDescription = GetStatusDescription(status);
        txtStatus.Text = statusDescription;

        // Aktualizacja koloru statusu
        switch (status)
        {
            case WelderStatus.CONNECTED:
            case WelderStatus.NEW_WELDER:
                txtStatus.Foreground = Brushes.Green;
                break;
            case WelderStatus.NO_CONNECTION:
                txtStatus.Foreground = Brushes.Red;
                break;
            default:
                txtStatus.Foreground = Brushes.Orange;
                break;
        }

        // Aktualizacja informacji o połączeniu
        var connectedPort = welderService.ConnectedPort;
        var connectedBaudRate = welderService.ConnectedBaudRate;

        if (!string.IsNullOrEmpty(connectedPort))
        {
            // Aktualizuj odpowiednie kontrolki w UI
            Log($"Połączono: {connectedPort} {(connectedBaudRate.HasValue ? $"({connectedBaudRate} baud)" : "")}");
        }
        else
        {
            Log("Brak połączenia");
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
        if (date.Length != 3) return "-";
        return $"{date[0]:D2}-{date[1]:D2}-{2000 + date[2]}";
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
                txtLog.Clear();
                LoggerService.Instance.LoadLogHistory();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd podczas usuwania pliku logu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 3. Numeracja wierszy historii pomiarów
    // Dodaj event do DataGrid:
    private void dataGridHistory_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        e.Row.Header = (e.Row.GetIndex() + 1).ToString();
    }
}