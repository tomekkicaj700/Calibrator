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
    /// <summary>
    /// Refactored WelderService that follows the new layered architecture
    /// Uses WelderCommunicationService for communication and ConfigService for settings
    /// </summary>
    public class WelderServiceRefactored
    {
        private readonly WelderCommunicationManager communicationManager;
        private readonly ConfigService configService;
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

        public WelderServiceRefactored()
        {
            communicationManager = new WelderCommunicationManager();
            configService = ConfigService.Instance;
            LoadHistoryFromFile();

            // Subscribe to config changes
            configService.SettingsChanged += OnSettingsChanged;
            configService.DetectedPortsChanged += OnDetectedPortsChanged;
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

        public WelderStatus WelderStatus => communicationManager.IsConnected ? WelderStatus.CONNECTED : WelderStatus.NO_CONNECTION;
        public string? ConnectedPort => GetConnectedPortInfo();
        public int? ConnectedBaudRate => GetConnectedBaudRate();
        public WelderInfo? WelderInfo => GetWelderInfo();

        public List<CalibrationRecord> CalibrationHistory => calibrationHistory;

        #endregion

        #region Welder Operations

        public async Task<bool> EnsureWelderConnectionAsync(string operationName = "operacji")
        {
            // Sprawdź czy mamy aktywne połączenie
            if (communicationManager.IsConnected)
            {
                Log($"Zgrzewarka już połączona. Kontynuuję {operationName}.");
                return true;
            }

            // Próbuj połączyć się z zapisanymi ustawieniami
            Log("Próbuję połączyć się z zapisanymi ustawieniami...");
            var connectSuccess = await communicationManager.ConnectWithSavedSettingsAsync();

            if (connectSuccess)
            {
                UpdateWelderInfo();
                Log($"✓ Połączenie udane. Kontynuuję {operationName}.");
                return true;
            }

            // Jeśli nie udało się połączyć, skanuj wszystkie urządzenia
            Log($"Automatycznie skanuję wszystkie urządzenia w poszukiwaniu zgrzewarki dla {operationName}...");
            var scanSuccess = await ScanAllDevicesAsync();

            if (scanSuccess)
            {
                // Próbuj ponownie połączyć się z nowymi ustawieniami
                connectSuccess = await communicationManager.ConnectWithSavedSettingsAsync();
                UpdateWelderInfo();

                if (connectSuccess)
                {
                    Log($"✓ Zgrzewarka została znaleziona i połączona! Kontynuuję {operationName}.");
                    return true;
                }
            }

            Log("✗ Zgrzewarka nie została znaleziona na żadnym urządzeniu.");
            Log("✗ Sprawdź połączenie ze zgrzewarką i spróbuj ponownie.");
            return false;
        }

        public async Task<bool> ScanComPortsAsync(string? preferredPort = null, int? preferredBaud = null)
        {
            if (isScanning) return false;

            try
            {
                isScanning = true;
                Log("=== ROZPOCZYNAM SKANOWANIE PORTÓW COM ===");

                var results = await communicationManager.ScanComPortsAsync(preferredPort, preferredBaud);

                // Update ConfigService with detected ports
                var detectedPorts = results.Select(r => new DetectedPort
                {
                    Name = r.PortName,
                    Type = CommunicationType.COM_PORT,
                    BaudRate = r.BaudRate,
                    IsConnected = r.Success,
                    LastDetected = DateTime.Now,
                    Response = r.Response
                }).ToList();

                await configService.AddDetectedPortsAsync(detectedPorts);

                var success = results.Any(r => r.Success);
                UpdateWelderInfo();

                if (success)
                {
                    Log("✓ Skanowanie portów COM zakończone pomyślnie!");
                    Log("✓ Ustawienia komunikacji zostały zapisane.");
                }
                else
                {
                    Log("✗ Skanowanie portów COM nie powiodło się.");
                    Log("✗ Nie znaleziono zgrzewarki na żadnym porcie COM.");
                }

                return success;
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
            if (isScanning) return false;

            try
            {
                isScanning = true;
                Log("=== ROZPOCZYNAM SKANOWANIE URZĄDZEŃ USR-N520 ===");
                Log("USR-N520 ma 2 fizyczne porty: RS-232 (9-pin D-sub) i RS-485 (2-wire A+, B-)");

                var results = await communicationManager.ScanUSRDevicesAsync();

                // Update ConfigService with detected ports
                var detectedPorts = results.Select(r => new DetectedPort
                {
                    Name = r.PortName,
                    Type = CommunicationType.USR_N520,
                    IpAddress = ExtractIpFromPortName(r.PortName),
                    Port = ExtractPortFromPortName(r.PortName),
                    IsConnected = r.Success,
                    LastDetected = DateTime.Now,
                    Response = r.Response
                }).ToList();

                await configService.AddDetectedPortsAsync(detectedPorts);

                var success = results.Any(r => r.Success);
                UpdateWelderInfo();

                if (success)
                {
                    Log("✓ Skanowanie USR-N520 zakończone pomyślnie!");
                    Log("✓ Ustawienia komunikacji zostały zapisane.");
                }
                else
                {
                    Log("✗ Skanowanie USR-N520 nie powiodło się.");
                    Log("✗ Nie znaleziono zgrzewarki na żadnym urządzeniu USR-N520.");
                }

                return success;
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

        public async Task<bool> ScanAllDevicesAsync()
        {
            if (isScanning) return false;

            try
            {
                isScanning = true;
                Log("=== ROZPOCZYNAM SKANOWANIE WSZYSTKICH URZĄDZEŃ ===");

                // Clear previous detected ports
                await configService.ClearDetectedPortsAsync();

                // Scan COM ports first
                var comResults = await communicationManager.ScanComPortsAsync();
                var comDetectedPorts = comResults.Select(r => new DetectedPort
                {
                    Name = r.PortName,
                    Type = CommunicationType.COM_PORT,
                    BaudRate = r.BaudRate,
                    IsConnected = r.Success,
                    LastDetected = DateTime.Now,
                    Response = r.Response
                }).ToList();

                await configService.AddDetectedPortsAsync(comDetectedPorts);

                // Scan TCP devices
                var tcpResults = await communicationManager.ScanUSRDevicesAsync();
                var tcpDetectedPorts = tcpResults.Select(r => new DetectedPort
                {
                    Name = r.PortName,
                    Type = CommunicationType.USR_N520,
                    IpAddress = ExtractIpFromPortName(r.PortName),
                    Port = ExtractPortFromPortName(r.PortName),
                    IsConnected = r.Success,
                    LastDetected = DateTime.Now,
                    Response = r.Response
                }).ToList();

                await configService.AddDetectedPortsAsync(tcpDetectedPorts);

                var success = comResults.Any(r => r.Success) || tcpResults.Any(r => r.Success);

                if (success)
                {
                    await communicationManager.ConnectWithSavedSettingsAsync();
                }

                UpdateWelderInfo();

                if (success)
                {
                    Log("✓ Skanowanie wszystkich urządzeń zakończone pomyślnie!");
                    Log("✓ Ustawienia komunikacji zostały zapisane.");
                }
                else
                {
                    Log("✗ Skanowanie wszystkich urządzeń nie powiodło się.");
                    Log("✗ Nie znaleziono zgrzewarki na żadnym urządzeniu.");
                }

                return success;
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

        public async Task<WeldParameters?> ReadWeldParametersAsync()
        {
            if (isReadingConfig) return null;

            try
            {
                isReadingConfig = true;
                Log("=== ODCZYTUJĘ PARAMETRY ZGRZEWANIA ===");

                if (!communicationManager.IsConnected)
                {
                    Log("✗ Brak połączenia ze zgrzewarką.");
                    return null;
                }

                byte[] cmd = WelderCommands.BuildReadWeldParametersCommand(true); // Bez szyfrowania
                string response = await communicationManager.SendCommandAndReceiveResponseAsync(cmd, 2000);

                if (string.IsNullOrEmpty(response))
                {
                    Log("✗ Brak odpowiedzi od zgrzewarki.");
                    return null;
                }

                Log($"Odpowiedź: {response}");

                // Parsuj odpowiedź
                var parameters = ParseWeldParametersResponse(response);
                if (parameters != null)
                {
                    UpdateWeldParameters(parameters);
                    WeldParametersUpdated?.Invoke(parameters);
                    Log("✓ Parametry zgrzewania zostały odczytane i wyświetlone.");
                }
                else
                {
                    Log("✗ Nie udało się sparsować odpowiedzi zgrzewarki.");
                }

                return parameters;
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas odczytu parametrów zgrzewania: {ex.Message}");
                return null;
            }
            finally
            {
                isReadingConfig = false;
            }
        }

        public async Task<SKonfiguracjaSystemu?> ReadConfigurationAsync()
        {
            if (isReadingConfig) return null;

            try
            {
                isReadingConfig = true;
                Log("=== ODCZYTUJĘ KONFIGURACJĘ SYSTEMU ===");

                if (!communicationManager.IsConnected)
                {
                    Log("✗ Brak połączenia ze zgrzewarką.");
                    return null;
                }

                byte[] cmd = WelderCommands.BuildReadConfigCommand(true); // Bez szyfrowania
                string response = await communicationManager.SendCommandAndReceiveResponseAsync(cmd, 2000);

                if (string.IsNullOrEmpty(response))
                {
                    Log("✗ Brak odpowiedzi od zgrzewarki.");
                    return null;
                }

                Log($"Odpowiedź: {response}");

                // Parsuj odpowiedź
                var config = ParseConfigurationResponse(response);
                if (config != null)
                {
                    ConfigurationUpdated?.Invoke(config);
                    Log("✓ Konfiguracja została odczytana i wyświetlona.");
                }
                else
                {
                    Log("✗ Nie udało się sparsować odpowiedzi zgrzewarki.");
                }

                return config;
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas odczytu konfiguracji: {ex.Message}");
                return null;
            }
            finally
            {
                isReadingConfig = false;
            }
        }

        #endregion

        #region Event Handlers

        private void OnSettingsChanged(WelderSettings settings)
        {
            Log("Settings changed in ConfigService");
            // Update UI or other components as needed
        }

        private void OnDetectedPortsChanged(List<DetectedPort> ports)
        {
            Log($"Detected ports updated: {ports.Count} ports");
            // Update UI or other components as needed
        }

        #endregion

        #region Helper Methods

        private void UpdateWelderInfo()
        {
            var status = WelderStatus;
            WelderStatusChanged?.Invoke(status);
        }

        private void UpdateWeldParameters(WeldParameters parameters)
        {
            // Update statistics
            if (parameters.NapiecieZgrzewania > 0)
            {
                napiecieMin = Math.Min(napiecieMin, parameters.NapiecieZgrzewania);
                napiecieMax = Math.Max(napiecieMax, parameters.NapiecieZgrzewania);
                napiecieSum += parameters.NapiecieZgrzewania;
                napiecieSamples++;
            }

            if (parameters.PradZgrzewania > 0)
            {
                pradMin = Math.Min(pradMin, parameters.PradZgrzewania);
                pradMax = Math.Max(pradMax, parameters.PradZgrzewania);
                pradSum += parameters.PradZgrzewania;
                pradSamples++;
            }
        }

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

            Log("Statystyki zostały zresetowane.");
        }

        private string? GetConnectedPortInfo()
        {
            var settings = configService.GetSettings();
            if (settings.CommType == "COM")
            {
                return settings.COM_Port;
            }
            else if (settings.CommType == "TCP")
            {
                return $"USR-N520 {settings.USR_IP}:{settings.USR_Port}";
            }
            return null;
        }

        private int? GetConnectedBaudRate()
        {
            var settings = configService.GetSettings();
            return settings.CommType == "COM" ? settings.COM_Baud : 115200; // Domyślny dla USR
        }

        private WelderInfo? GetWelderInfo()
        {
            // Implementacja pobierania informacji o zgrzewarce
            return new WelderInfo
            {
                IsNewUnit = false,
                Version = "Unknown",
                Type = "Unknown"
            };
        }

        private WeldParameters? ParseWeldParametersResponse(string response)
        {
            // Implementacja parsowania odpowiedzi z parametrami zgrzewania
            // To jest uproszczona implementacja - w rzeczywistości trzeba by sparsować odpowiedź
            try
            {
                // Przykład parsowania - dostosuj do rzeczywistej odpowiedzi
                return new WeldParameters
                {
                    NapiecieZgrzewania = 12.5,
                    PradZgrzewania = 2.3,
                    ADCNapZgrzew = 0x1234,
                    ADCPradZgrzew = 0x5678,
                    // ... inne parametry
                };
            }
            catch (Exception ex)
            {
                Log($"Błąd parsowania parametrów zgrzewania: {ex.Message}");
                return null;
            }
        }

        private SKonfiguracjaSystemu? ParseConfigurationResponse(string response)
        {
            // Implementacja parsowania odpowiedzi z konfiguracją
            // To jest uproszczona implementacja - w rzeczywistości trzeba by sparsować odpowiedź
            try
            {
                // Przykład parsowania - dostosuj do rzeczywistej odpowiedzi
                return new SKonfiguracjaSystemu
                {
                    // ... parametry konfiguracji
                };
            }
            catch (Exception ex)
            {
                Log($"Błąd parsowania konfiguracji: {ex.Message}");
                return null;
            }
        }

        private string? ExtractIpFromPortName(string portName)
        {
            // Extract IP from port name like "USR-N520 192.168.0.7:23"
            if (portName.Contains(" "))
            {
                var parts = portName.Split(' ');
                if (parts.Length > 1)
                {
                    var addressPart = parts[1];
                    if (addressPart.Contains(":"))
                    {
                        return addressPart.Split(':')[0];
                    }
                }
            }
            return null;
        }

        private int? ExtractPortFromPortName(string portName)
        {
            // Extract port from port name like "USR-N520 192.168.0.7:23"
            if (portName.Contains(":"))
            {
                var portPart = portName.Split(':').Last();
                if (int.TryParse(portPart, out int port))
                {
                    return port;
                }
            }
            return null;
        }

        #endregion

        #region History Management

        public void SaveCalibrationToHistory(SKonfiguracjaSystemu config, string deviceType, string serialNumber)
        {
            try
            {
                var record = new CalibrationRecord
                {
                    DateTime = DateTime.Now,
                    DeviceType = deviceType,
                    SerialNumber = serialNumber,
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
                    Typ = config.Typ,
                    KeypadSE = config.KeypadSE,
                    NrJezyka = config.nrJezyka,
                    NazwaZgrzewarki = config.NazwaZgrzewarki,
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

                Log($"✓ Kalibracja została zapisana do historii. Liczba rekordów: {calibrationHistory.Count}");
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas zapisywania kalibracji: {ex.Message}");
            }
        }

        public void ClearHistory()
        {
            calibrationHistory.Clear();
            SaveHistoryToFile();
            HistoryUpdated?.Invoke(calibrationHistory);
            Log("Historia pomiarów została wyczyszczona.");
        }

        public void RefreshHistory()
        {
            LoadHistoryFromFile();
            HistoryUpdated?.Invoke(calibrationHistory);
            Log("Historia pomiarów została odświeżona.");
        }

        public List<CalibrationRecord> GetFilteredHistory(string? deviceTypeFilter = null, string? serialNumberFilter = null)
        {
            var filtered = calibrationHistory.AsEnumerable();

            if (!string.IsNullOrEmpty(deviceTypeFilter))
            {
                filtered = filtered.Where(r => r.DeviceType.Contains(deviceTypeFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(serialNumberFilter))
            {
                filtered = filtered.Where(r => r.SerialNumber.Contains(serialNumberFilter, StringComparison.OrdinalIgnoreCase));
            }

            return filtered.ToList();
        }

        private void SaveHistoryToFile()
        {
            lock (historyFileLock)
            {
                try
                {
                    var wrapper = new WelderServiceWrapper
                    {
                        Records = calibrationHistory
                    };

                    var serializer = new XmlSerializer(typeof(WelderServiceWrapper));
                    using var writer = new StreamWriter(historyFilePath);
                    serializer.Serialize(writer, wrapper);
                }
                catch (Exception ex)
                {
                    Log($"Błąd podczas zapisywania historii do pliku: {ex.Message}");
                }
            }
        }

        private void LoadHistoryFromFile()
        {
            lock (historyFileLock)
            {
                try
                {
                    if (File.Exists(historyFilePath))
                    {
                        var serializer = new XmlSerializer(typeof(WelderServiceWrapper));
                        using var reader = new StreamReader(historyFilePath);
                        var wrapper = (WelderServiceWrapper?)serializer.Deserialize(reader);
                        calibrationHistory = wrapper?.Records ?? new List<CalibrationRecord>();
                        Log($"Historia pomiarów została wczytana z pliku. Liczba rekordów: {calibrationHistory.Count}");
                    }
                    else
                    {
                        calibrationHistory = new List<CalibrationRecord>();
                        Log("Plik historii nie istnieje. Utworzono nową listę.");
                    }
                }
                catch (Exception ex)
                {
                    Log($"Błąd podczas wczytywania historii z pliku: {ex.Message}");
                    calibrationHistory = new List<CalibrationRecord>();
                }
            }
        }

        private string FormatDate(byte[] date)
        {
            if (date.Length != 3) return "-";
            return $"{date[0]:D2}-{date[1]:D2}-{2000 + date[2]}";
        }

        #endregion

        #region XML Serialization Classes

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
            public string DeviceType { get; set; } = string.Empty;

            [XmlAttribute("SerialNumber")]
            public string SerialNumber { get; set; } = string.Empty;

            [XmlElement("DateTime")]
            public DateTime DateTime { get; set; }

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

            [XmlElement("Typ")]
            public int Typ { get; set; }

            [XmlElement("KeypadSE")]
            public int KeypadSE { get; set; }

            [XmlElement("NrJezyka")]
            public int NrJezyka { get; set; }

            [XmlElement("NazwaZgrzewarki")]
            public int NazwaZgrzewarki { get; set; }

            [XmlElement("DaneWlasciciela0")]
            public string DaneWlasciciela0 { get; set; } = string.Empty;

            [XmlElement("DaneWlasciciela1")]
            public string DaneWlasciciela1 { get; set; } = string.Empty;

            [XmlElement("DaneWlasciciela2")]
            public string DaneWlasciciela2 { get; set; } = string.Empty;

            [XmlElement("DataSprzedazy")]
            public string DataSprzedazy { get; set; } = string.Empty;

            [XmlElement("DataPierwszegoZgrzewu")]
            public string DataPierwszegoZgrzewu { get; set; } = string.Empty;

            [XmlElement("DataOstatniejKalibracji")]
            public string DataOstatniejKalibracji { get; set; } = string.Empty;

            [XmlElement("OffsetMCP3425")]
            public int OffsetMCP3425 { get; set; }

            [XmlElement("WolneMiejsce")]
            public string WolneMiejsce { get; set; } = string.Empty;

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

        #region Static Methods

        public static string GetWelderName(int index)
        {
            return index switch
            {
                0 => "Zgrzewarka standardowa",
                1 => "Zgrzewarka premium",
                2 => "Zgrzewarka industrial",
                _ => $"Zgrzewarka typu {index}"
            };
        }

        #endregion

        public void Dispose()
        {
            communicationManager.Disconnect();
            configService.SettingsChanged -= OnSettingsChanged;
            configService.DetectedPortsChanged -= OnDetectedPortsChanged;
        }
    }
}