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

namespace Calibrator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly Welder welder;
    private bool isScanning = false;
    private readonly System.Windows.Threading.DispatcherTimer configTimer;
    private bool isRunning = false;
    private bool isReadingConfig = false;
    private double napiecieMin = double.MaxValue;
    private double napiecieMax = double.MinValue;
    private double napiecieSum = 0;
    private int napiecieSamples = 0;

    private double pradMin = double.MaxValue;
    private double pradMax = double.MinValue;
    private double pradSum = 0;
    private int pradSamples = 0;

    private bool logPanelCollapsed = false;
    private double lastLogPanelHeight = 150;

    // Historia pomiarów
    private List<CalibrationRecord> calibrationHistory = new List<CalibrationRecord>();
    private readonly string historyFilePath = "calibration_history.xml";

    // Klasa do przechowywania danych kalibracji
    [XmlRoot("CalibrationHistory")]
    public class CalibrationHistory
    {
        [XmlElement("Record")]
        public List<CalibrationRecord> Records { get; set; } = new List<CalibrationRecord>();
    }

    [XmlRoot("CalibrationRecord")]
    public class CalibrationRecord
    {
        [XmlAttribute("DeviceType")]
        public string DeviceType { get; set; } = "";

        [XmlAttribute("SerialNumber")]
        public string SerialNumber { get; set; } = "";

        [XmlElement("DateTime")]
        public string DateTime { get; set; } = "";

        // Kanały zgrzewarki
        [XmlElement("MMWVH")]
        public int MMWVH { get; set; }

        [XmlElement("MMWVL")]
        public int MMWVL { get; set; }

        [XmlElement("IVHC_U")]
        public int IVHC_U { get; set; }

        [XmlElement("IVLC_U")]
        public int IVLC_U { get; set; }

        [XmlElement("ADCIVHC_U")]
        public int ADCIVHC_U { get; set; }

        [XmlElement("ADCIVLC_U")]
        public int ADCIVLC_U { get; set; }

        [XmlElement("MMWCL")]
        public int MMWCL { get; set; }

        [XmlElement("MMWCH")]
        public int MMWCH { get; set; }

        [XmlElement("IVHC_I")]
        public int IVHC_I { get; set; }

        [XmlElement("IVLC_I")]
        public int IVLC_I { get; set; }

        [XmlElement("ADCIVHC_I")]
        public int ADCIVHC_I { get; set; }

        [XmlElement("ADCIVLC_I")]
        public int ADCIVLC_I { get; set; }

        // Pozostałe parametry
        [XmlElement("Typ")]
        public int Typ { get; set; }

        [XmlElement("KeypadSE")]
        public int KeypadSE { get; set; }

        [XmlElement("NrJezyka")]
        public int NrJezyka { get; set; }

        [XmlElement("NazwaZgrzewarki")]
        public string NazwaZgrzewarki { get; set; } = "";

        [XmlElement("DaneWlasciciela0")]
        public string DaneWlasciciela0 { get; set; } = "";

        [XmlElement("DaneWlasciciela1")]
        public string DaneWlasciciela1 { get; set; } = "";

        [XmlElement("DaneWlasciciela2")]
        public string DaneWlasciciela2 { get; set; } = "";

        [XmlElement("DataSprzedazy")]
        public string DataSprzedazy { get; set; } = "";

        [XmlElement("DataPierwszegoZgrzewu")]
        public string DataPierwszegoZgrzewu { get; set; } = "";

        [XmlElement("DataOstatniejKalibracji")]
        public string DataOstatniejKalibracji { get; set; } = "";

        [XmlElement("OffsetMCP3425")]
        public int OffsetMCP3425 { get; set; }

        [XmlElement("WolneMiejsce")]
        public string WolneMiejsce { get; set; } = "";

        [XmlElement("LiczbaZgrzOstKalibr")]
        public int LiczbaZgrzOstKalibr { get; set; }

        [XmlElement("OkresKalibracji")]
        public int OkresKalibracji { get; set; }

        [XmlElement("RejestrKonfiguracji")]
        public int RejestrKonfiguracji { get; set; }

        [XmlElement("RejestrKonfiguracjiBankTwo")]
        public int RejestrKonfiguracjiBankTwo { get; set; }

        [XmlElement("TempOtRefVal")]
        public int TempOtRefVal { get; set; }

        [XmlElement("TempOtRefADC")]
        public int TempOtRefADC { get; set; }

        [XmlElement("KorekcjaTempWewn")]
        public int KorekcjaTempWewn { get; set; }

        [XmlElement("KorekcjaTempZewn")]
        public int KorekcjaTempZewn { get; set; }

        [XmlElement("KodBlokady")]
        public int KodBlokady { get; set; }

        [XmlElement("TypBlokady")]
        public int TypBlokady { get; set; }

        [XmlElement("GPSconfiguration")]
        public int GPSconfiguration { get; set; }
    }

    // Struktura pomocnicza do mapowania wartości kanałów zgrzewarki
    private class WartosciKanalowZgrzewarki
    {
        public int MMWVH, MMWVL, IVHC_U, IVLC_U, ADCIVHC_U, ADCIVLC_U;
        public int MMWCL, MMWCH, IVHC_I, IVLC_I, ADCIVHC_I, ADCIVLC_I;
    }

    public MainWindow()
    {
        InitializeComponent();

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
                        LogToConsole("Okno było poza ekranem - wycentrowano na ekranie");
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
                        LogToConsole($"Dostosowano pozycję okna do rozmiaru ekranu: {screenWidth}x{screenHeight}");
                        LogToConsole($"Zapisana pozycja: {settings.WindowLeft.Value},{settings.WindowTop.Value}");
                        LogToConsole($"Ustawiona pozycja: {windowLeft},{windowTop}");
                    });
                }
            }

            // Logowanie informacji o dostosowaniu rozmiaru
            if (windowWidth != settings.WindowWidth.Value || windowHeight != settings.WindowHeight.Value)
            {
                // Użyj Dispatcher.BeginInvoke aby logowanie nastąpiło po inicjalizacji komponentów
                Dispatcher.BeginInvoke(() =>
                {
                    LogToConsole($"Dostosowano rozmiar okna do rozmiaru ekranu: {screenWidth}x{screenHeight}");
                    LogToConsole($"Zapisany rozmiar: {settings.WindowWidth.Value}x{settings.WindowHeight.Value}");
                    LogToConsole($"Ustawiony rozmiar: {windowWidth}x{windowHeight}");
                });
            }
        }
        if (settings.WindowMaximized.HasValue)
        {
            this.WindowState = settings.WindowMaximized.Value ? WindowState.Maximized : WindowState.Normal;
        }

        welder = new Welder(LogToConsole);
        welder.BezSzyfrowania = true;
        UpdateWelderInfo();
        configTimer = new System.Windows.Threading.DispatcherTimer();
        configTimer.Tick += ConfigTimer_Tick;

        // Inicjalizacja historii pomiarów
        LoadHistoryFromFile();
        dataGridHistory.ItemsSource = calibrationHistory;

        // Inicjalizacja filtrowania
        ApplyFilter();
    }

    private void InitConfigTimer()
    {
        configTimer.Interval = TimeSpan.FromMilliseconds(GetSelectedInterval());
    }

    private async void btnRun_Click(object sender, RoutedEventArgs e)
    {
        if (!isRunning)
        {
            LogToConsole("=== ROZPOCZYNAM RUN ===");

            if (!await EnsureWelderConnectionAsync("funkcji RUN"))
            {
                return; // Połączenie się nie udało, przerwij
            }

            LogToConsole("✓ Połączenie udane! Uruchamiam timer odczytu parametrów zgrzewania...");

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
            LogToConsole("=== ZATRZYMUJĘ RUN ===");
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
        if (isReadingConfig) return;
        isReadingConfig = true;
        try
        {
            byte[] configData = new byte[256];
            if (await Task.Run(() => welder.ReadConfigurationRegister(out configData)))
            {
                var config = await Task.Run(() => CalibrationReport.ReadFromBuffer(configData));
                DisplayConfiguration(config);
                txtStatus.Text = $"Konfiguracja odczytana {DateTime.Now:HH:mm:ss}";
            }
            else
            {
                txtStatus.Text = "Nie udało się odczytać konfiguracji.";
            }
        }
        catch (Exception ex)
        {
            txtStatus.Text = $"Błąd: {ex.Message}";
        }
        finally
        {
            isReadingConfig = false;
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
        var status = welder.GetStatus();
        txtWelderStatus.Text = GetStatusDescription(status);
        txtPort.Text = welder.GetConnectedPort() ?? "Brak";
        txtBaudRate.Text = welder.GetConnectedBaudRate()?.ToString() ?? "Brak";

        var info = welder.GetWelderInfo();
        if (info != null)
        {
            txtType.Text = info.IsNewUnit ? "Nowa jednostka" : info.Type;
            txtVersion.Text = info.Version;
        }
        else
        {
            txtType.Text = "Brak danych";
            txtVersion.Text = "Brak danych";
        }
    }

    private string GetStatusDescription(WelderStatus status)
    {
        var field = status.GetType().GetField(status.ToString());
        if (field == null) return status.ToString();

        var attribute = (DescriptionAttribute?)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
        return attribute?.Description ?? status.ToString();
    }

    private void LogToConsole(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => LogToConsole(message));
            return;
        }
        txtLog.AppendText($"{DateTime.Now:HH:mm:ss} | {message}\n");
        txtLog.ScrollToEnd();
    }

    private async Task ReadWeldParametersAndUpdateUIAsync()
    {
        try
        {
            LogToConsole("Wysyłanie polecenia: Odczytaj parametry zgrzewania");
            string? errorDetails = null;
            var wp = await Task.Run(() => welder.ReadWeldParameters(out errorDetails));
            if (wp != null)
            {
                UpdateWeldParameters(wp);
                LogToConsole("Odczytano parametry zgrzewania poprawnie.");
            }
            else
            {
                LogToConsole($"Błąd: Nie udało się odczytać parametrów zgrzewania. {errorDetails}");
            }
        }
        catch (Exception ex)
        {
            LogToConsole($"Błąd: {ex.Message}");
        }
    }

    private void UpdateWeldParameters(WeldParameters parameters)
    {
        // Aktualizacja napięcia
        double napiecie = parameters.NapiecieZgrzewania;
        napiecieMin = Math.Min(napiecieMin, napiecie);
        napiecieMax = Math.Max(napiecieMax, napiecie);
        napiecieSum += napiecie;
        napiecieSamples++;

        // Aktualizacja prądu
        double prad = parameters.PradZgrzewania;
        pradMin = Math.Min(pradMin, prad);
        pradMax = Math.Max(pradMax, prad);
        pradSum += prad;
        pradSamples++;

        // Aktualizacja wyświetlania
        Dispatcher.Invoke(() =>
        {
            // Logowanie surowej odpowiedzi
            LogToConsole($"Odebrane wartości:");
            LogToConsole($"Napięcie zgrzewania: {parameters.NapiecieZgrzewania:F2}V");
            LogToConsole($"Prąd zgrzewania: {parameters.PradZgrzewania:F2}A");
            LogToConsole($"ADC Napięcia: 0x{parameters.ADCNapZgrzew:X4}");
            LogToConsole($"ADC Prądu: 0x{parameters.ADCPradZgrzew:X4}");
            LogToConsole($"IVHC-U: {parameters.IVHC_U}");
            LogToConsole($"IVLC-U: {parameters.IVLC_U}");
            LogToConsole($"ADCIVHC-U: {parameters.ADCIVHC_U}");
            LogToConsole($"ADCIVLC-U: {parameters.ADCIVLC_U}");
            LogToConsole($"IVHC-I: {parameters.IMHC_I}");
            LogToConsole($"IVLC-I: {parameters.IMLC_I}");
            LogToConsole($"ADCIVHC-I: {parameters.ADCIVHC_I}");
            LogToConsole($"ADCIVLC-I: {parameters.ADCIVLC_I}");
            LogToConsole($"MMWVL: {parameters.MMWVL}");
            LogToConsole($"MMWVH: {parameters.MMWVH}");
            LogToConsole($"MMWCL: {parameters.MMWCL}");
            LogToConsole($"MMWCH: {parameters.MMWCH}");
            LogToConsole("-------------------");

            // Napięcie
            txtNapiecieZgrzewania.Text = napiecie.ToString("0.0", CultureInfo.InvariantCulture);
            txtNapiecieMin.Text = (napiecieSamples > 0 ? napiecieMin : 0).ToString("F2");
            txtNapiecieMax.Text = (napiecieSamples > 0 ? napiecieMax : 0).ToString("F2");
            txtNapiecieAvr.Text = (napiecieSamples > 0 ? (napiecieSum / napiecieSamples) : 0).ToString("F2");
            txtADCNapZgrzew.Text = parameters.ADCNapZgrzew.ToString();
            gaugeNapiecie.Value = napiecie;

            // Prąd
            txtPradZgrzewania.Text = prad.ToString("0.0", CultureInfo.InvariantCulture);
            txtPradMin.Text = (pradSamples > 0 ? pradMin : 0).ToString("F2");
            txtPradMax.Text = (pradSamples > 0 ? pradMax : 0).ToString("F2");
            txtPradAvr.Text = (pradSamples > 0 ? (pradSum / pradSamples) : 0).ToString("F2");
            txtADCPradZgrzew.Text = parameters.ADCPradZgrzew.ToString();
            gaugePrad.Value = prad;

            // Współczynniki kalibracji
            wspZgrzewaniaVoltage.MMWVHValue = parameters.MMWVH.ToString();
            wspZgrzewaniaVoltage.MMWVLValue = parameters.MMWVL.ToString();
            wspZgrzewaniaCurrent.MMWCHValue = parameters.MMWCH.ToString();
            wspZgrzewaniaCurrent.MMWCLValue = parameters.MMWCL.ToString();
            wspZgrzewaniaVoltage.IVHC_U_Value = parameters.IVHC_U.ToString();
            wspZgrzewaniaVoltage.IVLC_U_Value = parameters.IVLC_U.ToString();
            wspZgrzewaniaCurrent.IVHC_I_Value = parameters.IMHC_I.ToString();
            wspZgrzewaniaCurrent.IVLC_I_Value = parameters.IMLC_I.ToString();
            wspZgrzewaniaVoltage.ADCIVHC_U_Value = parameters.ADCIVHC_U.ToString();
            wspZgrzewaniaVoltage.ADCIVLC_U_Value = parameters.ADCIVLC_U.ToString();
            wspZgrzewaniaCurrent.ADCIVHC_I_Value = parameters.ADCIVHC_I.ToString();
            wspZgrzewaniaCurrent.ADCIVLC_I_Value = parameters.ADCIVLC_I.ToString();

            // Współczynniki kalibracji - prawa kolumna w zakładce Kanały zgrzewarki
            kanalyZgrzewarkiVoltage.MMWVHValue = parameters.MMWVH.ToString();
            kanalyZgrzewarkiVoltage.MMWVLValue = parameters.MMWVL.ToString();
            kanalyZgrzewarkiCurrent.MMWCHValue = parameters.MMWCH.ToString();
            kanalyZgrzewarkiCurrent.MMWCLValue = parameters.MMWCL.ToString();
        });
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
            LogToConsole("Brak zapisanych ustawień komunikacji.");
            LogToConsole($"Automatycznie skanuję wszystkie urządzenia w poszukiwaniu zgrzewarki dla {operationName}...");

            // Automatycznie skanuj wszystkie urządzenia
            var scanSuccess = await Task.Run(() => welder.ScanAndSaveSettings());
            UpdateWelderInfo();

            if (!scanSuccess)
            {
                LogToConsole("✗ Zgrzewarka nie została znaleziona na żadnym urządzeniu.");
                LogToConsole("✗ Sprawdź połączenie ze zgrzewarką i spróbuj ponownie.");
                return false;
            }

            LogToConsole($"✓ Zgrzewarka została znaleziona! Kontynuuję {operationName}...");
        }
        else
        {
            LogToConsole("Znaleziono zapisane ustawienia komunikacji.");
        }

        // Sprawdź czy jest połączenie, jeśli nie - użyj zapisanych ustawień
        if (welder.GetStatus() != WelderStatus.CONNECTED && welder.GetStatus() != WelderStatus.NEW_WELDER)
        {
            LogToConsole("Brak połączenia ze zgrzewarką. Próbuję połączyć się z zapisanymi ustawieniami...");
            var runSuccess = await Task.Run(() => welder.RunWithSavedSettings());
            UpdateWelderInfo();
            if (!runSuccess)
            {
                LogToConsole("✗ Nie udało się połączyć z zapisanymi ustawieniami.");
                LogToConsole("✗ Spróbuj ponownie skanować urządzenia.");
                return false;
            }
            LogToConsole($"✓ Połączenie z zapisanymi ustawieniami udane. Kontynuuję {operationName}.");
        }
        else
        {
            LogToConsole($"Zgrzewarka już połączona. Kontynuuję {operationName}.");
        }

        return true;
    }

    private async void btnReadWeldParams_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            btnReadWeldParams.IsEnabled = false;

            if (!await EnsureWelderConnectionAsync("odczytu parametrów zgrzewania"))
            {
                return; // Połączenie się nie udało, przerwij
            }

            await ReadWeldParametersAndUpdateUIAsync();
            // Przełącz na zakładkę 'Parametry zgrzewania' (pierwsza zakładka, indeks 0)
            mainTabControl.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            LogToConsole($"✗ Błąd: {ex.Message}");
        }
        finally
        {
            btnReadWeldParams.IsEnabled = true;
        }
    }

    private async void btnScanPorts_Click(object sender, RoutedEventArgs e)
    {
        if (isScanning) return;
        try
        {
            isScanning = true;
            btnScanPorts.IsEnabled = false;

            // Sprawdź zapisane ustawienia komunikacji
            var settings = WelderSettings.Load();
            bool hasComSettings = !string.IsNullOrEmpty(settings.CommType) && settings.CommType == "COM" && !string.IsNullOrEmpty(settings.COM_Port);

            if (hasComSettings)
            {
                LogToConsole("=== ROZPOCZYNAM SKANOWANIE PORTÓW COM ===");
                LogToConsole($"Znaleziono zapisane ustawienia COM: {settings.COM_Port} ({settings.COM_Baud} baud)");
                LogToConsole("Najpierw sprawdzam ostatnio używany port COM...");

                // Najpierw spróbuj na zapisanym porcie COM
                var scanSuccess = await Task.Run(() => welder.ScanComPortsOnly(settings.COM_Port, settings.COM_Baud));
                UpdateWelderInfo();

                if (scanSuccess)
                {
                    btnReadConfig.IsEnabled = true;
                    LogToConsole("✓ Zgrzewarka znaleziona na ostatnio używanym porcie COM!");
                    LogToConsole("✓ Ustawienia komunikacji zostały zapisane.");
                    LogToConsole("✓ Możesz teraz użyć przycisku RUN z zapisanymi ustawieniami.");
                }
                else
                {
                    LogToConsole("Zgrzewarka nie została znaleziona na ostatnio używanym porcie COM.");
                    LogToConsole("Skanuję wszystkie dostępne porty COM...");

                    // Jeśli nie znaleziono na preferowanym porcie, skanuj wszystkie porty COM
                    scanSuccess = await Task.Run(() => welder.ScanComPortsOnly());
                    UpdateWelderInfo();

                    if (scanSuccess)
                    {
                        btnReadConfig.IsEnabled = true;
                        LogToConsole("✓ Zgrzewarka znaleziona na innym porcie COM!");
                        LogToConsole("✓ Ustawienia komunikacji zostały zaktualizowane.");
                        LogToConsole("✓ Możesz teraz użyć przycisku RUN z zapisanymi ustawieniami.");
                    }
                    else
                    {
                        btnReadConfig.IsEnabled = false;
                        LogToConsole("✗ Skanowanie portów COM nie powiodło się.");
                        LogToConsole("✗ Nie znaleziono zgrzewarki na żadnym porcie COM.");
                    }
                }
            }
            else
            {
                LogToConsole("=== ROZPOCZYNAM SKANOWANIE WSZYSTKICH PORTÓW COM ===");
                if (!string.IsNullOrEmpty(settings.CommType))
                {
                    LogToConsole($"Znaleziono zapisane ustawienia {settings.CommType}, ale nie dotyczą portów COM.");
                    LogToConsole("Skanuję wszystkie dostępne porty COM...");
                }
                else
                {
                    LogToConsole("Brak zapisanych ustawień komunikacji.");
                    LogToConsole("Skanuję wszystkie dostępne porty COM...");
                }

                // Skanuj tylko porty COM (nie USR)
                var scanSuccess = await Task.Run(() => welder.ScanComPortsOnly());
                UpdateWelderInfo();

                if (scanSuccess)
                {
                    btnReadConfig.IsEnabled = true;
                    LogToConsole("✓ Skanowanie portów COM zakończone pomyślnie!");
                    LogToConsole("✓ Ustawienia komunikacji zostały zapisane.");
                    LogToConsole("✓ Możesz teraz użyć przycisku RUN z zapisanymi ustawieniami.");

                    // Pokaż szczegóły połączenia
                    var currentSettings = WelderSettings.Load();
                    if (!string.IsNullOrEmpty(currentSettings.CommType) && currentSettings.CommType == "COM")
                    {
                        LogToConsole($"✓ Typ połączenia: {currentSettings.CommType}");
                        LogToConsole($"✓ Port: {currentSettings.COM_Port}, Baud: {currentSettings.COM_Baud}");
                    }
                }
                else
                {
                    btnReadConfig.IsEnabled = false;
                    LogToConsole("✗ Skanowanie portów COM nie powiodło się.");
                    LogToConsole("✗ Nie znaleziono zgrzewarki na żadnym porcie COM.");
                }
            }
        }
        catch (Exception ex)
        {
            LogToConsole($"Błąd podczas skanowania: {ex.Message}");
        }
        finally
        {
            isScanning = false;
            btnScanPorts.IsEnabled = true;
            LogToConsole("=== ZAKOŃCZONO SKANOWANIE ===");
        }
    }

    private void DisplayConfiguration(SKonfiguracjaSystemu config)
    {
        // Kanały zgrzewarki (Tab 1) - używamy nowego komponentu
        welderChannels.UInputVoltageHighCurrent = string.Join(", ", config.uInputVoltageHighCurrent);
        welderChannels.UInputVoltageLowCurrent = string.Join(", ", config.uInputVoltageLowCurrent);
        welderChannels.UADCValueHighCurrent = string.Join(", ", config.uADCValueHighCurrent);
        welderChannels.UADCValueLowCurrent = string.Join(", ", config.uADCValueLowCurrent);
        welderChannels.UMultimeterWeldVoltageLowCurrent = config.uMultimeterWeldVoltageLowCurrent.ToString();
        welderChannels.UMultimeterWeldVoltageHighCurrent = config.uMultimeterWeldVoltageHighCurrent.ToString();
        welderChannels.UMultimeterWeldCurrentLowCurrent = config.uMultimeterWeldCurrentLowCurrent.ToString();
        welderChannels.UMultimeterWeldCurrentHighCurrent = config.uMultimeterWeldCurrentHighCurrent.ToString();

        // Wartości kanałów zgrzewarki (Tab 1)
        var kanaly = CalibrationReport.MapujKanały(config);
        // Usuwam stare przypisania do txtNapiecieZasilaniaAll, txtNapiecieZgrzewaniaAll, txtPradZgrzewaniaAll
        // Usuwam błędne przypisania do txtMMWVH_kanaly itd. z config
        // Wartości do tych pól będą przypisywane w UpdateWeldParameters na podstawie WeldParameters

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

        // Wartości kanałów zgrzewarki (Tab 2 prawa kolumna)
        var wartosci = MapujWartosciKanalowZgrzewarki(config);
        kanalyZgrzewarkiVoltage.MMWVHValue = wartosci.MMWVH.ToString();
        kanalyZgrzewarkiVoltage.MMWVLValue = wartosci.MMWVL.ToString();
        kanalyZgrzewarkiVoltage.IVHC_U_Value = wartosci.IVHC_U.ToString();
        kanalyZgrzewarkiVoltage.IVLC_U_Value = wartosci.IVLC_U.ToString();
        kanalyZgrzewarkiVoltage.ADCIVHC_U_Value = wartosci.ADCIVHC_U.ToString();
        kanalyZgrzewarkiVoltage.ADCIVLC_U_Value = wartosci.ADCIVLC_U.ToString();
        kanalyZgrzewarkiCurrent.MMWCLValue = wartosci.MMWCL.ToString();
        kanalyZgrzewarkiCurrent.MMWCHValue = wartosci.MMWCH.ToString();
        kanalyZgrzewarkiCurrent.IVHC_I_Value = wartosci.IVHC_I.ToString();
        kanalyZgrzewarkiCurrent.IVLC_I_Value = wartosci.IVLC_I.ToString();
        kanalyZgrzewarkiCurrent.ADCIVHC_I_Value = wartosci.ADCIVHC_I.ToString();
        kanalyZgrzewarkiCurrent.ADCIVLC_I_Value = wartosci.ADCIVLC_I.ToString();
    }

    private string FormatDate(byte[] date)
    {
        if (date.Length != 3) return "-";
        return $"{date[0]:D2}-{date[1]:D2}-{2000 + date[2]}";
    }

    private async void btnReadConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            btnReadConfig.IsEnabled = false;

            if (!await EnsureWelderConnectionAsync("odczytu konfiguracji"))
            {
                return; // Połączenie się nie udało, przerwij
            }

            LogToConsole("Wysyłanie polecenia: Odczytaj kalibrację");
            byte[] configData = new byte[256];
            if (await Task.Run(() => welder.ReadConfigurationRegister(out configData)))
            {
                var config = await Task.Run(() => CalibrationReport.ReadFromBuffer(configData));
                DisplayConfiguration(config);
                LogToConsole("✓ Kalibracja odczytana pomyślnie.");
                // Przełącz na zakładkę 'Parametry kalibracji' (druga zakładka, indeks 1)
                mainTabControl.SelectedIndex = 1;
            }
            else
            {
                LogToConsole("✗ Błąd: Nie udało się odczytać kalibracji.");
            }
        }
        catch (Exception ex)
        {
            LogToConsole($"✗ Błąd: {ex.Message}");
        }
        finally
        {
            btnReadConfig.IsEnabled = true;
        }
    }

    private void btnResetStats_Click(object sender, RoutedEventArgs e)
    {
        napiecieMin = double.MaxValue;
        napiecieMax = double.MinValue;
        napiecieSum = 0;
        napiecieSamples = 0;
        pradMin = double.MaxValue;
        pradMax = double.MinValue;
        pradSum = 0;
        pradSamples = 0;
        // Odśwież wyświetlanie
        txtNapiecieMin.Text = "0.00";
        txtNapiecieMax.Text = "0.00";
        txtNapiecieAvr.Text = "0.00";
        txtPradMin.Text = "0.00";
        txtPradMax.Text = "0.00";
        txtPradAvr.Text = "0.00";
    }

    private void btnToggleLogPanel_Click(object sender, RoutedEventArgs e)
    {
        var mainGrid = (Grid)this.Content;
        if (!logPanelCollapsed)
        {
            lastLogPanelHeight = mainGrid.RowDefinitions[2].ActualHeight;
            mainGrid.RowDefinitions[2].Height = new GridLength(0);
            txtToggleLogIcon.Text = "▲";
            logPanelCollapsed = true;
            LogToConsole($"Zwinięto logi, zapamiętana wysokość: {lastLogPanelHeight:F0} px");
        }
        else
        {
            mainGrid.RowDefinitions[2].Height = new GridLength(lastLogPanelHeight, GridUnitType.Pixel);
            txtToggleLogIcon.Text = "▼";
            logPanelCollapsed = false;
            LogToConsole($"Rozwinięto logi, przywrócona wysokość: {lastLogPanelHeight:F0} px");
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
            LogToConsole($"Odczytano wysokość logów z ustawień: {settings.LogPanelHeight.Value:F0} px");
        }
        else
        {
            LogToConsole("Brak zapisanej wysokości logów w ustawieniach, używam domyślnej.");
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
            LogToConsole($"Zapisano wysokość logów: {newHeight:F0} px do ustawień.");
        }
    }

    private void LogPanelSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (!logPanelCollapsed)
        {
            var settings = WelderSettings.Load();
            var mainGrid = (Grid)this.Content;
            double newHeight = mainGrid.RowDefinitions[2].ActualHeight;
            settings.LogPanelHeight = newHeight;
            settings.Save();
            LogToConsole($"[Splitter] Zapisano wysokość logów: {newHeight:F0} px do ustawień.");
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

    private void btnSaveCalibration_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Sprawdź czy mamy dane do zapisania
            if (string.IsNullOrEmpty(kanalyZgrzewarkiVoltage.MMWVHValue) || kanalyZgrzewarkiVoltage.MMWVHValue == "—")
            {
                MessageBox.Show("Brak danych konfiguracyjnych do zapisania. Odczytaj konfigurację ze zgrzewarki.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var record = new CalibrationRecord
            {
                DateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                DeviceType = txtNazwaZgrzewarki.Text,
                SerialNumber = txtNumerSeryjny.Text,
                // Kanały zgrzewarki
                MMWVH = int.TryParse(kanalyZgrzewarkiVoltage.MMWVHValue, out int mmwvh) ? mmwvh : 0,
                MMWVL = int.TryParse(kanalyZgrzewarkiVoltage.MMWVLValue, out int mmwvl) ? mmwvl : 0,
                IVHC_U = int.TryParse(kanalyZgrzewarkiVoltage.IVHC_U_Value, out int ivhc_u) ? ivhc_u : 0,
                IVLC_U = int.TryParse(kanalyZgrzewarkiVoltage.IVLC_U_Value, out int ivlc_u) ? ivlc_u : 0,
                ADCIVHC_U = int.TryParse(kanalyZgrzewarkiVoltage.ADCIVHC_U_Value, out int adcivhc_u) ? adcivhc_u : 0,
                ADCIVLC_U = int.TryParse(kanalyZgrzewarkiVoltage.ADCIVLC_U_Value, out int adcivlc_u) ? adcivlc_u : 0,
                MMWCL = int.TryParse(kanalyZgrzewarkiCurrent.MMWCLValue, out int mmwcl) ? mmwcl : 0,
                MMWCH = int.TryParse(kanalyZgrzewarkiCurrent.MMWCHValue, out int mmwch) ? mmwch : 0,
                IVHC_I = int.TryParse(kanalyZgrzewarkiCurrent.IVHC_I_Value, out int ivhc_i) ? ivhc_i : 0,
                IVLC_I = int.TryParse(kanalyZgrzewarkiCurrent.IVLC_I_Value, out int ivlc_i) ? ivlc_i : 0,
                ADCIVHC_I = int.TryParse(kanalyZgrzewarkiCurrent.ADCIVHC_I_Value, out int adcivhc_i) ? adcivhc_i : 0,
                ADCIVLC_I = int.TryParse(kanalyZgrzewarkiCurrent.ADCIVLC_I_Value, out int adcivlc_i) ? adcivlc_i : 0,
                // Pozostałe parametry
                Typ = int.TryParse(txtTyp.Text, out int typ) ? typ : 0,
                KeypadSE = int.TryParse(txtKeypadSE.Text, out int keypadse) ? keypadse : 0,
                NrJezyka = int.TryParse(txtNrJezyka.Text, out int nrjezyka) ? nrjezyka : 0,
                NazwaZgrzewarki = txtNazwaZgrzewarki.Text,
                DaneWlasciciela0 = txtDaneWlasciciela0.Text,
                DaneWlasciciela1 = txtDaneWlasciciela1.Text,
                DaneWlasciciela2 = txtDaneWlasciciela2.Text,
                DataSprzedazy = txtDataSprzedazy.Text,
                DataPierwszegoZgrzewu = txtDataPierwszegoZgrzewu.Text,
                DataOstatniejKalibracji = txtDataOstatniejKalibracji.Text,
                OffsetMCP3425 = int.TryParse(txtOffsetMCP3425.Text, out int offset) ? offset : 0,
                WolneMiejsce = txtWolneMiejsce.Text,
                LiczbaZgrzOstKalibr = int.TryParse(txtLiczbaZgrzOstKalibr.Text, out int liczba) ? liczba : 0,
                OkresKalibracji = int.TryParse(txtOkresKalibracji.Text, out int okres) ? okres : 0,
                RejestrKonfiguracji = int.TryParse(txtRejestrKonfiguracji.Text, out int rejestr) ? rejestr : 0,
                RejestrKonfiguracjiBankTwo = int.TryParse(txtRejestrKonfiguracjiBankTwo.Text, out int rejestr2) ? rejestr2 : 0,
                TempOtRefVal = int.TryParse(txtTempOtRefVal.Text, out int tempval) ? tempval : 0,
                TempOtRefADC = int.TryParse(txtTempOtRefADC.Text, out int tempadc) ? tempadc : 0,
                KorekcjaTempWewn = int.TryParse(txtKorekcjaTempWewn.Text, out int korekcjaw) ? korekcjaw : 0,
                KorekcjaTempZewn = int.TryParse(txtKorekcjaTempZewn.Text, out int korekcjaz) ? korekcjaz : 0,
                KodBlokady = int.TryParse(txtKodBlokady.Text, out int kod) ? kod : 0,
                TypBlokady = int.TryParse(txtTypBlokady.Text, out int typblok) ? typblok : 0,
                GPSconfiguration = int.TryParse(txtGPSconfiguration.Text, out int gps) ? gps : 0
            };

            calibrationHistory.Add(record);
            SaveHistoryToFile();

            // Odśwież widok siatki
            dataGridHistory.ItemsSource = null;
            dataGridHistory.ItemsSource = calibrationHistory;

            MessageBox.Show("Zapisano bieżącą konfigurację do historii.", "Zapisano", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd podczas zapisywania do historii: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void btnRefreshHistory_Click(object sender, RoutedEventArgs e)
    {
        LoadHistoryFromFile();
        dataGridHistory.ItemsSource = calibrationHistory;
        ApplyFilter();
        LogToConsole("Odświeżono historię pomiarów.");
    }

    private void btnClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Czy na pewno chcesz wyczyścić całą historię pomiarów? Tej operacji nie można cofnąć.", "Potwierdź wyczyszczenie historii", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            calibrationHistory.Clear();
            dataGridHistory.ItemsSource = null;
            dataGridHistory.ItemsSource = calibrationHistory;
            if (File.Exists(historyFilePath))
            {
                try
                {
                    File.Delete(historyFilePath);
                    LogToConsole("Plik historii pomiarów został usunięty.");
                }
                catch (Exception ex)
                {
                    LogToConsole($"Nie udało się usunąć pliku historii: {ex.Message}");
                }
            }
            LogToConsole("Historia pomiarów została wyczyszczona.");
        }
    }

    private void btnOpenFileHistory_Click(object sender, RoutedEventArgs e)
    {
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
                LogToConsole($"Nie udało się otworzyć pliku historii: {ex.Message}");
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

    private void SaveHistoryToFile()
    {
        try
        {
            var serializer = new XmlSerializer(typeof(CalibrationHistory));
            var historyWrapper = new CalibrationHistory { Records = this.calibrationHistory };
            using (var writer = new StreamWriter(historyFilePath))
            {
                serializer.Serialize(writer, historyWrapper);
            }
        }
        catch (Exception ex)
        {
            LogToConsole($"Błąd podczas zapisywania do pliku: {ex.Message}");
        }
    }

    private void LoadHistoryFromFile()
    {
        if (!File.Exists(historyFilePath))
        {
            calibrationHistory = new List<CalibrationRecord>();
            return;
        }

        try
        {
            var serializer = new XmlSerializer(typeof(CalibrationHistory));
            using (var reader = new StreamReader(historyFilePath))
            {
                if (serializer.Deserialize(reader) is CalibrationHistory history)
                {
                    calibrationHistory = history.Records;
                }
                else
                {
                    calibrationHistory = new List<CalibrationRecord>();
                    LogToConsole("Błąd: Nie udało się odczytać historii kalibracji z pliku.");
                }
            }
        }
        catch (Exception ex)
        {
            LogToConsole($"Błąd podczas wczytywania z pliku: {ex.Message}");
            calibrationHistory = new List<CalibrationRecord>();
        }
    }

    private void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void btnClearFilter_Click(object sender, RoutedEventArgs e)
    {
        txtFilterDeviceType.Text = string.Empty;
        txtFilterSerialNumber.Text = string.Empty;
    }

    private void ApplyFilter()
    {
        var filterDeviceType = txtFilterDeviceType.Text.ToLower();
        var filterSerialNumber = txtFilterSerialNumber.Text.ToLower();

        if (string.IsNullOrEmpty(filterDeviceType) && string.IsNullOrEmpty(filterSerialNumber))
        {
            dataGridHistory.ItemsSource = calibrationHistory;
        }
        else
        {
            var filteredList = calibrationHistory.Where(r =>
                (string.IsNullOrEmpty(filterDeviceType) || (r.DeviceType?.ToLower().Contains(filterDeviceType) ?? false)) &&
                (string.IsNullOrEmpty(filterSerialNumber) || (r.SerialNumber?.ToLower().Contains(filterSerialNumber) ?? false))
            ).ToList();
            dataGridHistory.ItemsSource = filteredList;
        }
    }

    private void dataGridHistory_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            if (dataGridHistory.SelectedItem is CalibrationRecord selectedRecord)
            {
                // Kopiuj wartości do karty Konfiguracja (Wartości kanałów zgrzewarki)
                kanalyZgrzewarkiVoltage.MMWVHValue = selectedRecord.MMWVH.ToString();
                kanalyZgrzewarkiVoltage.MMWVLValue = selectedRecord.MMWVL.ToString();
                kanalyZgrzewarkiVoltage.IVHC_U_Value = selectedRecord.IVHC_U.ToString();
                kanalyZgrzewarkiVoltage.IVLC_U_Value = selectedRecord.IVLC_U.ToString();
                kanalyZgrzewarkiVoltage.ADCIVHC_U_Value = selectedRecord.ADCIVHC_U.ToString();
                kanalyZgrzewarkiVoltage.ADCIVLC_U_Value = selectedRecord.ADCIVLC_U.ToString();
                kanalyZgrzewarkiCurrent.MMWCHValue = selectedRecord.MMWCH.ToString();
                kanalyZgrzewarkiCurrent.MMWCLValue = selectedRecord.MMWCL.ToString();
                kanalyZgrzewarkiCurrent.IVHC_I_Value = selectedRecord.IVHC_I.ToString();
                kanalyZgrzewarkiCurrent.IVLC_I_Value = selectedRecord.IVLC_I.ToString();
                kanalyZgrzewarkiCurrent.ADCIVHC_I_Value = selectedRecord.ADCIVHC_I.ToString();
                kanalyZgrzewarkiCurrent.ADCIVLC_I_Value = selectedRecord.ADCIVLC_I.ToString();

                // Kopiuj pozostałe parametry
                txtTyp.Text = selectedRecord.Typ.ToString();
                txtKeypadSE.Text = selectedRecord.KeypadSE.ToString();
                txtNrJezyka.Text = selectedRecord.NrJezyka.ToString();
                txtNazwaZgrzewarki.Text = selectedRecord.NazwaZgrzewarki;
                txtDaneWlasciciela0.Text = selectedRecord.DaneWlasciciela0;
                txtDaneWlasciciela1.Text = selectedRecord.DaneWlasciciela1;
                txtDaneWlasciciela2.Text = selectedRecord.DaneWlasciciela2;
                txtDataSprzedazy.Text = selectedRecord.DataSprzedazy;
                txtDataPierwszegoZgrzewu.Text = selectedRecord.DataPierwszegoZgrzewu;
                txtDataOstatniejKalibracji.Text = selectedRecord.DataOstatniejKalibracji;
                txtOffsetMCP3425.Text = selectedRecord.OffsetMCP3425.ToString();
                txtWolneMiejsce.Text = selectedRecord.WolneMiejsce;
                txtLiczbaZgrzOstKalibr.Text = selectedRecord.LiczbaZgrzOstKalibr.ToString();
                txtOkresKalibracji.Text = selectedRecord.OkresKalibracji.ToString();
                txtRejestrKonfiguracji.Text = selectedRecord.RejestrKonfiguracji.ToString();
                txtRejestrKonfiguracjiBankTwo.Text = selectedRecord.RejestrKonfiguracjiBankTwo.ToString();
                txtTempOtRefVal.Text = selectedRecord.TempOtRefVal.ToString();
                txtTempOtRefADC.Text = selectedRecord.TempOtRefADC.ToString();
                txtKorekcjaTempWewn.Text = selectedRecord.KorekcjaTempWewn.ToString();
                txtKorekcjaTempZewn.Text = selectedRecord.KorekcjaTempZewn.ToString();
                txtKodBlokady.Text = selectedRecord.KodBlokady.ToString();
                txtTypBlokady.Text = selectedRecord.TypBlokady.ToString();
                txtGPSconfiguration.Text = selectedRecord.GPSconfiguration.ToString();

                // Pokaż panel szczegółów, jeśli jest ukryty
                if (HistoryDetailsPanel.Visibility == Visibility.Collapsed)
                {
                    HistoryDetailsPanel.Visibility = Visibility.Visible;
                }

                // Przełącz na zakładkę "Parametry kalibracji", aby zobaczyć załadowane dane
                mainTabControl.SelectedIndex = 1;
            }
        }
        catch (Exception ex)
        {
            LogToConsole($"Błąd podczas ładowania danych z historii: {ex.Message}");
        }
    }

    private void dataGridHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dataGridHistory.SelectedItem is CalibrationRecord selectedRecord)
        {
            historyVoltageCoefficients.MMWVHValue = selectedRecord.MMWVH.ToString();
            historyVoltageCoefficients.MMWVLValue = selectedRecord.MMWVL.ToString();
            historyVoltageCoefficients.IVHC_U_Value = selectedRecord.IVHC_U.ToString();
            historyVoltageCoefficients.IVLC_U_Value = selectedRecord.IVLC_U.ToString();
            historyVoltageCoefficients.ADCIVHC_U_Value = selectedRecord.ADCIVHC_U.ToString();
            historyVoltageCoefficients.ADCIVLC_U_Value = selectedRecord.ADCIVLC_U.ToString();

            historyCurrentCoefficients.MMWCHValue = selectedRecord.MMWCH.ToString();
            historyCurrentCoefficients.MMWCLValue = selectedRecord.MMWCL.ToString();
            historyCurrentCoefficients.IVHC_I_Value = selectedRecord.IVHC_I.ToString();
            historyCurrentCoefficients.IVLC_I_Value = selectedRecord.IVLC_I.ToString();
            historyCurrentCoefficients.ADCIVHC_I_Value = selectedRecord.ADCIVHC_I.ToString();
            historyCurrentCoefficients.ADCIVLC_I_Value = selectedRecord.ADCIVLC_I.ToString();
        }
        else
        {
            // Wyczyść szczegóły, jeśli nic nie jest zaznaczone
            historyVoltageCoefficients.MMWVHValue = "—";
            historyVoltageCoefficients.MMWVLValue = "—";
            historyVoltageCoefficients.IVHC_U_Value = "—";
            historyVoltageCoefficients.IVLC_U_Value = "—";
            historyVoltageCoefficients.ADCIVHC_U_Value = "—";
            historyVoltageCoefficients.ADCIVLC_U_Value = "—";

            historyCurrentCoefficients.MMWCHValue = "—";
            historyCurrentCoefficients.MMWCLValue = "—";
            historyCurrentCoefficients.IVHC_I_Value = "—";
            historyCurrentCoefficients.IVLC_I_Value = "—";
            historyCurrentCoefficients.ADCIVHC_I_Value = "—";
            historyCurrentCoefficients.ADCIVLC_I_Value = "—";
        }
    }

    private void welderChannels_Loaded(object sender, RoutedEventArgs e)
    {

    }

    private async void btnScanUSRDevices_Click(object sender, RoutedEventArgs e)
    {
        if (isScanning) return;
        try
        {
            isScanning = true;
            btnScanUSRDevices.IsEnabled = false;
            LogToConsole("=== ROZPOCZYNAM SKANOWANIE URZĄDZEŃ USR-N520 ===");
            LogToConsole("USR-N520 ma 2 fizyczne porty: RS-232 (9-pin D-sub) i RS-485 (2-wire A+, B-)");
            LogToConsole("Próbuję połączyć się z USR-N520 na 192.168.0.7:23...");

            var scanSuccess = await Task.Run(() => welder.ScanUSRDevicesOnly());
            if (scanSuccess)
            {
                // Wymuś poprawny stan komunikacji po skanowaniu
                await Task.Run(() => welder.RunWithSavedSettings());
            }
            UpdateWelderInfo();

            if (scanSuccess)
            {
                LogToConsole("✓ Skanowanie USR-N520 zakończone pomyślnie!");
                LogToConsole("✓ Ustawienia komunikacji zostały zapisane.");

                // Pokaż szczegóły połączenia
                var settings = WelderSettings.Load();
                if (!string.IsNullOrEmpty(settings.CommType))
                {
                    LogToConsole($"✓ Typ połączenia: {settings.CommType}");
                    if (settings.CommType == "TCP")
                    {
                        LogToConsole($"✓ IP: {settings.USR_IP}, Port: {settings.USR_Port}");
                    }
                    else if (settings.CommType == "COM")
                    {
                        LogToConsole($"✓ Port: {settings.COM_Port}, Baud: {settings.COM_Baud}");
                    }
                }
            }
            else
            {
                LogToConsole("✗ Skanowanie USR-N520 nie powiodło się.");
                LogToConsole("✗ Nie znaleziono zgrzewarki lub wystąpił błąd komunikacji.");
            }
        }
        catch (Exception ex)
        {
            LogToConsole($"Błąd podczas skanowania urządzeń USR-N520: {ex.Message}");
            LogToConsole("Szczegóły błędu:");
            LogToConsole($"  - Typ: {ex.GetType().Name}");
            LogToConsole($"  - Wiadomość: {ex.Message}");
            if (ex.InnerException != null)
            {
                LogToConsole($"  - Błąd wewnętrzny: {ex.InnerException.Message}");
            }
        }
        finally
        {
            isScanning = false;
            btnScanUSRDevices.IsEnabled = true;
            LogToConsole("=== ZAKOŃCZONO SKANOWANIE USR-N520 ===");
        }
    }

    private async void btnScanAllDevices_Click(object sender, RoutedEventArgs e)
    {
        if (isScanning) return;
        try
        {
            isScanning = true;
            btnScanAllDevices.IsEnabled = false;
            LogToConsole("=== ROZPOCZYNAM SKANOWANIE WSZYSTKICH URZĄDZEŃ ===");
            LogToConsole("Skanuję TCP/IP (USR-N520) i porty COM, zapisuję ustawienia komunikacji...");

            var scanSuccess = await Task.Run(() => welder.ScanAndSaveSettings());
            if (scanSuccess)
            {
                await Task.Run(() => welder.RunWithSavedSettings());
            }
            UpdateWelderInfo();

            if (scanSuccess)
            {
                btnReadConfig.IsEnabled = true;
                LogToConsole("✓ Skanowanie wszystkich urządzeń zakończone pomyślnie!");
                LogToConsole("✓ Ustawienia komunikacji zostały zapisane.");
                LogToConsole("✓ Możesz teraz użyć przycisku RUN z zapisanymi ustawieniami.");

                // Pokaż szczegóły połączenia
                var settings = WelderSettings.Load();
                if (!string.IsNullOrEmpty(settings.CommType))
                {
                    LogToConsole($"✓ Typ połączenia: {settings.CommType}");
                    if (settings.CommType == "TCP")
                    {
                        LogToConsole($"✓ IP: {settings.USR_IP}, Port: {settings.USR_Port}");
                    }
                    else if (settings.CommType == "COM")
                    {
                        LogToConsole($"✓ Port: {settings.COM_Port}, Baud: {settings.COM_Baud}");
                    }
                }
            }
            else
            {
                btnReadConfig.IsEnabled = false;
                LogToConsole("✗ Skanowanie wszystkich urządzeń nie powiodło się.");
                LogToConsole("✗ Nie znaleziono zgrzewarki na żadnym urządzeniu.");
            }
        }
        catch (Exception ex)
        {
            LogToConsole($"Błąd podczas skanowania wszystkich urządzeń: {ex.Message}");
        }
        finally
        {
            isScanning = false;
            btnScanAllDevices.IsEnabled = true;
            LogToConsole("=== ZAKOŃCZONO SKANOWANIE WSZYSTKICH URZĄDZEŃ ===");
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
                LogToConsole($"Otwarto plik konfiguracji: {configPath}");
            }
            else
            {
                LogToConsole($"Plik konfiguracji nie istnieje: {configPath}");
                MessageBox.Show($"Plik konfiguracji nie istnieje.\nŚcieżka: {configPath}", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            LogToConsole($"Błąd podczas otwierania pliku konfiguracji: {ex.Message}");
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
}