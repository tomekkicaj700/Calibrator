using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using WelderRS232;
using CalibrationReportLib;
using System.Text;
using static Logger.LoggerService;

namespace Calibrator.Services
{
    public class WelderService
    {
        private readonly Welder welder;
        private bool isScanning = false;
        private bool isRunning = false;
        private bool isReadingConfig = false;

        // Statystyki pomiarów
        private double napiecieMin = double.MaxValue;
        private double napiecieMax = double.MinValue;
        private double napiecieSum = 0;
        private int napiecieSamples = 0;

        private double pradMin = double.MaxValue;
        private double pradMax = double.MinValue;
        private double pradSum = 0;
        private int pradSamples = 0;

        // Historia pomiarów
        private List<CalibrationRecord> calibrationHistory = new List<CalibrationRecord>();
        private readonly string historyFilePath = "calibration_history.xml";
        private readonly object historyFileLock = new object();

        // Eventy do komunikacji z UI
        public event Action<string>? LogMessage;
        public event Action<WeldParameters>? WeldParametersUpdated;
        public event Action<SKonfiguracjaSystemu>? ConfigurationUpdated;
        public event Action<WelderStatus>? WelderStatusChanged;
        public event Action<List<CalibrationRecord>>? HistoryUpdated;

        public WelderService()
        {
            welder = new Welder(Log);
            welder.BezSzyfrowania = true;
            LoadHistoryFromFile();
        }

        #region Properties

        public bool IsScanning => isScanning;
        public bool IsRunning => isRunning;
        public bool IsReadingConfig => isReadingConfig;

        public double NapiecieMin => napiecieMin;
        public double NapiecieMax => napiecieMax;
        public double NapiecieAverage => napiecieSamples > 0 ? napiecieSum / napiecieSamples : 0;
        public int NapiecieSamples => napiecieSamples;

        public double PradMin => pradMin;
        public double PradMax => pradMax;
        public double PradAverage => pradSamples > 0 ? pradSum / pradSamples : 0;
        public int PradSamples => pradSamples;

        public WelderStatus WelderStatus => welder.GetStatus();
        public string? ConnectedPort => welder.GetConnectedPort();
        public int? ConnectedBaudRate => welder.GetConnectedBaudRate();
        public WelderInfo? WelderInfo => welder.GetWelderInfo();

        public List<CalibrationRecord> CalibrationHistory => calibrationHistory;

        #endregion

        #region Welder Operations

        public async Task<bool> EnsureWelderConnectionAsync(string operationName = "operacji")
        {
            // Sprawdź czy mamy zapisane ustawienia komunikacji
            var settings = WelderSettings.Load();
            if (string.IsNullOrEmpty(settings.CommType))
            {
                Log("Brak zapisanych ustawień komunikacji.");
                Log($"Automatycznie skanuję wszystkie urządzenia w poszukiwaniu zgrzewarki dla {operationName}...");

                // Automatycznie skanuj wszystkie urządzenia
                var scanSuccess = await Task.Run(() => welder.ScanAndSaveSettings());
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
            if (welder.GetStatus() != WelderStatus.CONNECTED && welder.GetStatus() != WelderStatus.NEW_WELDER)
            {
                Log("Brak połączenia ze zgrzewarką. Próbuję połączyć się z zapisanymi ustawieniami...");
                var runSuccess = await Task.Run(() => welder.RunWithSavedSettings());
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

        public async Task<bool> ScanComPortsAsync(string? preferredPort = null, int? preferredBaud = null)
        {
            if (isScanning) return false;

            try
            {
                isScanning = true;
                Log("=== ROZPOCZYNAM SKANOWANIE PORTÓW COM ===");

                bool scanSuccess;
                if (!string.IsNullOrEmpty(preferredPort) && preferredBaud.HasValue)
                {
                    Log($"Skanuję preferowany port: {preferredPort} ({preferredBaud} baud)");
                    scanSuccess = await Task.Run(() => welder.ScanComPortsOnly(preferredPort, preferredBaud.Value));

                    if (!scanSuccess)
                    {
                        Log("Nie znaleziono na preferowanym porcie, skanuję wszystkie...");
                        scanSuccess = await Task.Run(() => welder.ScanComPortsOnly());
                    }
                }
                else
                {
                    scanSuccess = await Task.Run(() => welder.ScanComPortsOnly());
                }

                UpdateWelderInfo();

                if (scanSuccess)
                {
                    Log("✓ Skanowanie portów COM zakończone pomyślnie!");
                    Log("✓ Ustawienia komunikacji zostały zapisane.");
                }
                else
                {
                    Log("✗ Skanowanie portów COM nie powiodło się.");
                    Log("✗ Nie znaleziono zgrzewarki na żadnym porcie COM.");
                }

                return scanSuccess;
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas skanowania: {ex.Message}");
                return false;
            }
            finally
            {
                isScanning = false;
                Log("=== ZAKOŃCZONO SKANOWANIE ===");
            }
        }

        public async Task<bool> ScanUSRDevicesAsync()
        {

            try
            {
                isScanning = true;
                Log("=== ROZPOCZYNAM SKANOWANIE URZĄDZEŃ USR-N520 ===");
                Log("USR-N520 ma 2 fizyczne porty: RS-232 (9-pin D-sub) i RS-485 (2-wire A+, B-)");
                Log("Próbuję połączyć się z USR-N520 na 192.168.0.7:23...");

                var scanSuccess = await Task.Run(() => welder.ScanUSRDevicesOnly());
                if (scanSuccess)
                {
                    await Task.Run(() => welder.RunWithSavedSettings());
                }
                UpdateWelderInfo();

                if (scanSuccess)
                {
                    Log("✓ Skanowanie USR-N520 zakończone pomyślnie!");
                    Log("✓ Ustawienia komunikacji zostały zapisane.");
                }
                else
                {
                    Log("✗ Skanowanie USR-N520 nie powiodło się.");
                    Log("✗ Nie znaleziono zgrzewarki lub wystąpił błąd komunikacji.");
                }

                return scanSuccess;
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas skanowania urządzeń USR-N520: {ex.Message}");
                return false;
            }
            finally
            {
                isScanning = false;
                Log("=== ZAKOŃCZONO SKANOWANIE USR-N520 ===");
            }
        }

        public async Task<bool> ScanAllDevicesAsync()
        {
            if (isScanning) return false;

            try
            {
                isScanning = true;
                Log("=== ROZPOCZYNAM SKANOWANIE WSZYSTKICH URZĄDZEŃ ===");
                Log("Skanuję TCP/IP (USR-N520) i porty COM, zapisuję ustawienia komunikacji...");

                var scanSuccess = await Task.Run(() => welder.ScanAndSaveSettings());
                if (scanSuccess)
                {
                    await Task.Run(() => welder.RunWithSavedSettings());
                }
                UpdateWelderInfo();

                if (scanSuccess)
                {
                    Log("✓ Skanowanie wszystkich urządzeń zakończone pomyślnie!");
                    Log("✓ Ustawienia komunikacji zostały zapisane.");
                }
                else
                {
                    Log("✗ Skanowanie wszystkich urządzeń nie powiodło się.");
                    Log("✗ Nie znaleziono zgrzewarki na żadnym urządzeniu.");
                }

                return scanSuccess;
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas skanowania wszystkich urządzeń: {ex.Message}");
                return false;
            }
            finally
            {
                isScanning = false;
                Log("=== ZAKOŃCZONO SKANOWANIE WSZYSTKICH URZĄDZEŃ ===");
            }
        }

        public async Task<WeldParameters?> ReadWeldParametersAsync()
        {
            try
            {
                Log("Wysyłanie polecenia: Odczytaj parametry zgrzewania");
                string? errorDetails = null;
                var wp = await Task.Run(() => welder.ReadWeldParameters(out errorDetails));
                if (wp != null)
                {
                    UpdateWeldParameters(wp);
                    Log("Odczytano parametry zgrzewania poprawnie.");
                    return wp;
                }
                else
                {
                    Log($"Błąd: Nie udało się odczytać parametrów zgrzewania. {errorDetails}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log($"Błąd: {ex.Message}");
                return null;
            }
        }

        public async Task<SKonfiguracjaSystemu?> ReadConfigurationAsync()
        {
            try
            {
                Log("Wysyłanie polecenia: Odczytaj kalibrację");
                byte[] configData = new byte[256];
                if (await Task.Run(() => welder.ReadConfigurationRegister(out configData)))
                {
                    var config = await Task.Run(() => CalibrationReport.ReadFromBuffer(configData));
                    Log("✓ Kalibracja odczytana pomyślnie.");
                    ConfigurationUpdated?.Invoke(config);
                    return config;
                }
                else
                {
                    Log("✗ Błąd: Nie udało się odczytać kalibracji.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log($"✗ Błąd: {ex.Message}");
                return null;
            }
        }

        private void UpdateWelderInfo()
        {
            WelderStatusChanged?.Invoke(welder.GetStatus());
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

            // Logowanie surowej odpowiedzi
            Log("Odebrane wartości:");
            Log($"Napięcie zgrzewania: {parameters.NapiecieZgrzewania:F2}V");
            Log($"Prąd zgrzewania: {parameters.PradZgrzewania:F2}A");
            Log($"ADC Napięcia: 0x{parameters.ADCNapZgrzew:X4}");
            Log($"ADC Prądu: 0x{parameters.ADCPradZgrzew:X4}");
            Log($"IVHC-U: {parameters.IVHC_U}");
            Log($"IVLC-U: {parameters.IVLC_U}");
            Log($"ADCIVHC-U: {parameters.ADCIVHC_U}");
            Log($"ADCIVLC-U: {parameters.ADCIVLC_U}");
            Log($"IVHC-I: {parameters.IMHC_I}");
            Log($"IVLC-I: {parameters.IMLC_I}");
            Log($"ADCIVHC-I: {parameters.ADCIVHC_I}");
            Log($"ADCIVLC-I: {parameters.ADCIVLC_I}");
            Log($"MMWVL: {parameters.MMWVL}");
            Log($"MMWVH: {parameters.MMWVH}");
            Log($"MMWCL: {parameters.MMWCL}");
            Log($"MMWCH: {parameters.MMWCH}");
            Log("-------------------");

            WeldParametersUpdated?.Invoke(parameters);
        }

        #endregion

        #region Statistics Management

        public void ResetStatistics()
        {
            napiecieMin = double.MaxValue;
            napiecieMax = double.MinValue;
            napiecieSum = 0;
            napiecieSamples = 0;
            pradMin = double.MaxValue;
            pradMax = double.MinValue;
            pradSum = 0;
            pradSamples = 0;
        }

        #endregion

        #region History Management

        public void SaveCalibrationToHistory(SKonfiguracjaSystemu config, string deviceType, string serialNumber)
        {
            if (config == null || config.uInputVoltageHighCurrent == null || config.uInputVoltageHighCurrent.Length < 7)
            {
                Log("Próba zapisu niekompletnej konfiguracji. Zapis przerwany.");
                return;
            }
            try
            {
                var record = new CalibrationRecord
                {
                    DateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    DeviceType = deviceType,
                    SerialNumber = serialNumber,
                    // Kanały zgrzewarki
                    MMWVH = config.uMultimeterWeldVoltageHighCurrent,
                    MMWVL = config.uMultimeterWeldVoltageLowCurrent,
                    IVHC_U = config.uInputVoltageHighCurrent.Length > 5 ? config.uInputVoltageHighCurrent[5] : 0,
                    IVLC_U = config.uInputVoltageLowCurrent.Length > 5 ? config.uInputVoltageLowCurrent[5] : 0,
                    ADCIVHC_U = config.uADCValueHighCurrent.Length > 5 ? config.uADCValueHighCurrent[5] : 0,
                    ADCIVLC_U = config.uADCValueLowCurrent.Length > 5 ? config.uADCValueLowCurrent[5] : 0,
                    MMWCL = config.uMultimeterWeldCurrentLowCurrent,
                    MMWCH = config.uMultimeterWeldCurrentHighCurrent,
                    IVHC_I = config.uInputVoltageHighCurrent.Length > 6 ? config.uInputVoltageHighCurrent[6] : 0,
                    IVLC_I = config.uInputVoltageLowCurrent.Length > 6 ? config.uInputVoltageLowCurrent[6] : 0,
                    ADCIVHC_I = config.uADCValueHighCurrent.Length > 6 ? config.uADCValueHighCurrent[6] : 0,
                    ADCIVLC_I = config.uADCValueLowCurrent.Length > 6 ? config.uADCValueLowCurrent[6] : 0,
                    // Pozostałe parametry
                    Typ = config.Typ,
                    KeypadSE = config.KeypadSE,
                    NrJezyka = config.nrJezyka,
                    NazwaZgrzewarki = WelderRS232.Welder.GetWelderName(config.NazwaZgrzewarki),
                    DaneWlasciciela0 = Encoding.ASCII.GetString(config.DaneWlasciciela0).TrimEnd('\0'),
                    DaneWlasciciela1 = Encoding.ASCII.GetString(config.DaneWlasciciela1).TrimEnd('\0'),
                    DaneWlasciciela2 = Encoding.ASCII.GetString(config.DaneWlasciciela2).TrimEnd('\0'),
                    DataSprzedazy = FormatDate(config.DataSprzedazy),
                    DataPierwszegoZgrzewu = FormatDate(config.DataPierwszegoZgrzewu),
                    DataOstatniejKalibracji = FormatDate(config.DataOstatniejKalibracji),
                    OffsetMCP3425 = config.Offset_MCP3425,
                    WolneMiejsce = BitConverter.ToString(config.WolneMiejsce),
                    LiczbaZgrzOstKalibr = config.LiczbaZgrzOstKalibr,
                    OkresKalibracji = config.OkresKalibracji,
                    RejestrKonfiguracji = config.RejestrKonfiguracji,
                    RejestrKonfiguracjiBankTwo = config.RejestrKonfiguracjiBankTwo,
                    TempOtRefVal = config.TempOtRefVal,
                    TempOtRefADC = config.TempOtRefADC,
                    KorekcjaTempWewn = config.KorekcjaTempWewn,
                    KorekcjaTempZewn = config.KorekcjaTempZewn,
                    KodBlokady = config.KodBlokady,
                    TypBlokady = config.TypBlokady,
                    GPSconfiguration = config.GPSconfiguration
                };

                calibrationHistory.Add(record);
                SaveHistoryToFile();
                HistoryUpdated?.Invoke(calibrationHistory);
                Log("Zapisano bieżącą konfigurację do historii.");
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas zapisywania do historii: {ex.Message}");
            }
        }

        public void ClearHistory()
        {
            calibrationHistory.Clear();
            if (File.Exists(historyFilePath))
            {
                try
                {
                    File.Delete(historyFilePath);
                    Log("Plik historii pomiarów został usunięty.");
                }
                catch (Exception ex)
                {
                    Log($"Nie udało się usunąć pliku historii: {ex.Message}");
                }
            }
            HistoryUpdated?.Invoke(calibrationHistory);
            Log("Historia pomiarów została wyczyszczona.");
        }

        public void RefreshHistory()
        {
            LoadHistoryFromFile();
            HistoryUpdated?.Invoke(calibrationHistory);
            Log("Odświeżono historię pomiarów.");
        }

        public List<CalibrationRecord> GetFilteredHistory(string? deviceTypeFilter = null, string? serialNumberFilter = null)
        {
            if (string.IsNullOrEmpty(deviceTypeFilter) && string.IsNullOrEmpty(serialNumberFilter))
            {
                return calibrationHistory;
            }

            return calibrationHistory.Where(r =>
                (string.IsNullOrEmpty(deviceTypeFilter) || (r.DeviceType?.ToLower().Contains(deviceTypeFilter.ToLower()) ?? false)) &&
                (string.IsNullOrEmpty(serialNumberFilter) || (r.SerialNumber?.ToLower().Contains(serialNumberFilter.ToLower()) ?? false))
            ).ToList();
        }

        private void SaveHistoryToFile()
        {
            try
            {
                lock (historyFileLock)
                {
                    try
                    {
                        var serializer = new XmlSerializer(typeof(WelderServiceWrapper));
                        var historyWrapper = new WelderServiceWrapper { Records = this.calibrationHistory };
                        using (var writer = new StreamWriter(historyFilePath))
                        {
                            serializer.Serialize(writer, historyWrapper);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Błąd podczas zapisywania do pliku (wewnątrz lock): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas zapisywania do pliku (poza lock): {ex.Message}");
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
                var serializer = new XmlSerializer(typeof(WelderServiceWrapper));
                using (var reader = new StreamReader(historyFilePath))
                {
                    if (serializer.Deserialize(reader) is WelderServiceWrapper history)
                    {
                        calibrationHistory = history.Records;
                    }
                    else
                    {
                        calibrationHistory = new List<CalibrationRecord>();
                        Log("Błąd: Nie udało się odczytać historii kalibracji z pliku.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas wczytywania z pliku: {ex.Message}");
                calibrationHistory = new List<CalibrationRecord>();
            }
        }

        private string FormatDate(byte[] date)
        {
            if (date.Length != 3) return "-";
            return $"{date[0]:D2}-{date[1]:D2}-{2000 + date[2]}";
        }

        #endregion

        #region Data Classes

        [XmlRoot("CalibrationHistory")]
        public class WelderServiceWrapper
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

        #endregion
    }
}
