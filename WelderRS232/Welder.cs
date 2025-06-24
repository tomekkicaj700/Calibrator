using System;
using System.IO.Ports;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;

namespace WelderRS232
{
    // Delegat dla funkcji logowania
    public delegate void LogCallback(string message);

    public enum WelderStatus
    {
        [Description("Brak połączenia ze zgrzewarką")]
        NO_CONNECTION,

        [Description("Zgrzewarka podłączona")]
        CONNECTED,

        [Description("Nie znaleziono zgrzewarki na żadnym porcie")]
        DEVICE_NOT_FOUND,

        [Description("Nowa zgrzewarka podłączona")]
        NEW_WELDER
    }

    public class PortScanResult
    {
        public string PortName { get; set; } = string.Empty;
        public int BaudRate { get; set; }
        public bool Success { get; set; }
        public string Response { get; set; } = string.Empty;
    }

    public class WelderInfo
    {
        public bool IsNewUnit { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    // Struktura przechowująca parametry zgrzewania pobrane ze zgrzewarki.
    public class WeldParameters
    {
        /// <summary>U: Napięcie zgrzewania (V, np. 23.4)</summary>
        public double NapiecieZgrzewania { get; set; }
        /// <summary>I: Prąd zgrzewania (A, np. 12.7)</summary>
        public double PradZgrzewania { get; set; }
        /// <summary>ADCU: Wartość ADC napięcia zgrzewania (hex)</summary>
        public int ADCNapZgrzew { get; set; }
        /// <summary>ADCI: Wartość ADC prądu zgrzewania (hex)</summary>
        public int ADCPradZgrzew { get; set; }
        /// <summary>IVHC-U: Wartość uInputVoltageHighCurrent dla napięcia zgrzewania</summary>
        public int IVHC_U { get; set; }
        /// <summary>IVLC-U: Wartość uInputVoltageLowCurrent dla napięcia zgrzewania</summary>
        public int IVLC_U { get; set; }
        /// <summary>ADCIVHC-U: Wartość uADCValueHighCurrent dla napięcia zgrzewania</summary>
        public int ADCIVHC_U { get; set; }
        /// <summary>ADCIVLC-U: Wartość uADCValueLowCurrent dla napięcia zgrzewania</summary>
        public int ADCIVLC_U { get; set; }
        /// <summary>IVHC-I: Wartość uInputVoltageHighCurrent dla prądu zgrzewania</summary>
        public int IMHC_I { get; set; }
        /// <summary>IVLC-I: Wartość uInputVoltageLowCurrent dla prądu zgrzewania</summary>
        public int IMLC_I { get; set; }
        /// <summary>ADCIVHC-I: Wartość uADCValueHighCurrent dla prądu zgrzewania</summary>
        public int ADCIVHC_I { get; set; }
        /// <summary>ADCIVLC-I: Wartość uADCValueLowCurrent dla prądu zgrzewania</summary>
        public int ADCIVLC_I { get; set; }
        /// <summary>MMWVL: uMultimeterWeldVoltageLowCurrent</summary>
        public int MMWVL { get; set; }
        /// <summary>MMWVH: uMultimeterWeldVoltageHighCurrent</summary>
        public int MMWVH { get; set; }
        /// <summary>MMWCL: uMultimeterWeldCurrentLowCurrent</summary>
        public int MMWCL { get; set; }
        /// <summary>MMWCH: uMultimeterWeldCurrentHighCurrent</summary>
        public int MMWCH { get; set; }
    }


    public class Welder
    {
        private WelderStatus status = WelderStatus.NO_CONNECTION;
        private string? connectedPort = null;
        private int? connectedBaudRate = null;
        private const string RC4_KEY = "adA$2#34&1ASdq123";
        private const int COMMAND_SIZE = 30;  // Match C++ COMMAND_SIZE
        private WelderInfo? welderInfo = null;
        private readonly WelderSettings settings;
        private readonly LogCallback logCallback;

        public bool BezSzyfrowania { get; set; } = false;

        // Konstruktor domyślny - używa Console.WriteLine
        public Welder() : this(Console.WriteLine)
        {
        }

        // Konstruktor z callbackiem
        public Welder(LogCallback? callback)
        {
            settings = WelderSettings.Load();
            logCallback = callback ?? Console.WriteLine;
        }

        // Prywatna metoda do logowania
        private void Log(string message)
        {
            logCallback?.Invoke(message);
        }

        public WelderStatus GetStatus() => status;
        public string? GetConnectedPort() => connectedPort;
        public int? GetConnectedBaudRate() => connectedBaudRate;
        public WelderInfo? GetWelderInfo() => welderInfo;

        public List<PortScanResult> ScanAllPorts()
        {
            var results = new List<PortScanResult>();
            status = WelderStatus.NO_CONNECTION;
            connectedPort = null;
            connectedBaudRate = null;

            // Najpierw spróbuj na ostatnio używanym porcie, jeśli jest zapisany
            if (settings.LastPort != null && settings.LastBaudRate != null)
            {
                Log($"Próba połączenia na ostatnio używanym porcie {settings.LastPort} z prędkością {settings.LastBaudRate} baud...");
                var result = TryConnectToPort(settings.LastPort, settings.LastBaudRate.Value);
                results.Add(result);
                if (result.Success)
                {
                    return results;
                }
            }

            // Jeśli nie udało się połączyć na ostatnim porcie, skanuj wszystkie
            int[] baudRates = { 19200, 115200 };
            foreach (var portName in SerialPort.GetPortNames())
            {
                // Jeśli to ostatnio używany port, pomiń go bo już próbowaliśmy
                if (portName == settings.LastPort)
                    continue;

                for (int i = 0; i < baudRates.Length; i++)
                {
                    int baud = baudRates[i];
                    for (int attempt = 1; attempt <= 2; attempt++)
                    {
                        var result = TryConnectToPort(portName, baud);
                        results.Add(result);
                        if (result.Success)
                        {
                            // Zapisz udane ustawienia
                            settings.LastPort = portName;
                            settings.LastBaudRate = baud;
                            settings.Save();
                            return results;
                        }
                    }
                }
            }

            // Jeśli nie znaleziono zgrzewarki, ustaw odpowiedni status
            if (status == WelderStatus.NO_CONNECTION)
            {
                status = WelderStatus.DEVICE_NOT_FOUND;
            }
            return results;
        }

        private PortScanResult TryConnectToPort(string portName, int baudRate)
        {
            try
            {
                using (var port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One))
                {
                    port.ReadTimeout = 1000;
                    port.WriteTimeout = 500;
                    port.Open();
                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();
                    byte[] cmd = WelderCommands.BuildIdentifyCommand(BezSzyfrowania);
                    Log($"Port: {portName}, Baud: {baudRate}");
                    PrintFrameTable(cmd);
                    port.Write(cmd, 0, cmd.Length);

                    // Odbiór do CRLF lub timeout 1s bez znaku
                    string response = ReadResponseToCRLF(port);
                    if (string.IsNullOrEmpty(response))
                    {
                        Log($"Brak odpowiedzi od urządzenia na porcie {portName} ({baudRate} baud)");
                        return new PortScanResult
                        {
                            PortName = portName,
                            BaudRate = baudRate,
                            Success = false,
                            Response = "Brak odpowiedzi"
                        };
                    }

                    bool found = response.Contains("ZGRZ") || response.Contains("AGRE");
                    if (!found)
                    {
                        Log($"Odpowiedź o nieznanym formacie na porcie {portName} ({baudRate} baud): {response}");
                        return new PortScanResult
                        {
                            PortName = portName,
                            BaudRate = baudRate,
                            Success = false,
                            Response = $"Nieznana odpowiedź: {response}"
                        };
                    }

                    if (found)
                    {
                        if (response.Contains("AGRE"))
                        {
                            status = WelderStatus.NEW_WELDER;
                            welderInfo = new WelderInfo
                            {
                                IsNewUnit = true,
                                Version = response.Length >= 6 && response[4] == 'V' && response[5] == 'C' ? "C" : "EE"
                            };
                        }
                        else
                        {
                            status = WelderStatus.CONNECTED;
                        }
                        connectedPort = portName;
                        connectedBaudRate = baudRate;
                        Log($"Poprawna odpowiedź od zgrzewarki na porcie {portName} ({baudRate} baud): {response}");
                    }

                    return new PortScanResult
                    {
                        PortName = portName,
                        BaudRate = baudRate,
                        Success = found,
                        Response = response
                    };
                }
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas próby połączenia na porcie {portName} ({baudRate} baud): {ex.Message}");
                return new PortScanResult
                {
                    PortName = portName,
                    BaudRate = baudRate,
                    Success = false,
                    Response = $"Błąd: {ex.Message}"
                };
            }
        }

        private void PrintFrameTable(byte[] frame)
        {
            // Wiersz z numerami bajtów
            Log("Bajt:   " + string.Join(" ", Enumerable.Range(0, frame.Length).Select(i => i.ToString("D2")).ToArray()));
            // Wiersz z wartościami HEX
            Log("HEX:    " + string.Join(" ", frame.Select(b => b.ToString("X2")).ToArray()));
            // Wiersz z wartościami ASCII (drukowalne znaki, reszta kropka)
            Log("ASCII:  " + string.Join(" ", frame.Select(b => b >= 32 && b <= 126 ? ((char)b).ToString() : ".").ToArray()));
            Log("");
        }

        public void Connect()
        {
            status = WelderStatus.NO_CONNECTION;
            connectedPort = null;
            connectedBaudRate = null;

            // Najpierw spróbuj na ostatnio używanym porcie
            if (settings.LastPort != null && settings.LastBaudRate != null)
            {
                var result = TryConnectToPort(settings.LastPort, settings.LastBaudRate.Value);
                if (result.Success)
                    return;
            }

            // Jeśli nie udało się połączyć na ostatnim porcie, skanuj wszystkie
            int[] baudRates = { 19200, 115200 };
            foreach (var portName in SerialPort.GetPortNames())
            {
                if (portName == settings.LastPort)
                    continue;

                foreach (var baud in baudRates)
                {
                    var result = TryConnectToPort(portName, baud);
                    if (result.Success)
                    {
                        settings.LastPort = portName;
                        settings.LastBaudRate = baud;
                        settings.Save();
                        return;
                    }
                }
            }

            // Jeśli nie znaleziono zgrzewarki, ustaw odpowiedni status
            if (status == WelderStatus.NO_CONNECTION)
            {
                status = WelderStatus.DEVICE_NOT_FOUND;
            }
        }

        public bool QueryWelderType()
        {
            if (status != WelderStatus.CONNECTED) return false;

            try
            {
                using (var port = new SerialPort(connectedPort!, 9600, Parity.None, 8, StopBits.One))
                {
                    port.ReadTimeout = 500;
                    port.WriteTimeout = 500;
                    port.Open();
                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();

                    byte[] cmd = WelderCommands.BuildTypeQueryCommand(BezSzyfrowania);
                    port.Write(cmd, 0, cmd.Length);

                    // Odbiór do CRLF lub timeout 1s bez znaku
                    string response = ReadResponseToCRLF(port);
                    if (!string.IsNullOrEmpty(response))
                    {
                        welderInfo = new WelderInfo();
                        if (response.StartsWith("AGRE"))
                        {
                            welderInfo.IsNewUnit = true;
                            if (response.Length >= 6 && response[4] == 'V' && response[5] == 'C')
                            {
                                welderInfo.Version = "C";
                            }
                            else
                            {
                                welderInfo.Version = "EE";
                            }
                            status = WelderStatus.NEW_WELDER;
                            return true;
                        }
                        else
                        {
                            welderInfo.Type = response.TrimEnd('\r', '\n');
                            return true;
                        }
                    }
                }
            }
            catch { /* ignoruj błędy portów */ }
            return false;
        }

        // Nowa metoda do wyświetlania statusu połączenia
        public void DisplayConnectionStatus()
        {
            string statusDescription = GetStatusDescription(status);
            if ((status == WelderStatus.CONNECTED || status == WelderStatus.NEW_WELDER) && connectedPort != null && connectedBaudRate != null)
            {
                Log($"{statusDescription} na porcie {connectedPort} z prędkością {connectedBaudRate} baud");
            }
            else
            {
                Log(statusDescription);
            }
        }

        private string GetStatusDescription(WelderStatus status)
        {
            var field = status.GetType().GetField(status.ToString());
            if (field == null) return status.ToString();

            var attribute = (DescriptionAttribute?)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
            return attribute?.Description ?? status.ToString();
        }

        public bool ReadConfigurationRegister(out byte[] configData)
        {
            configData = new byte[256];  // Inicjalizacja bufora na dane konfiguracji
            if (status != WelderStatus.CONNECTED && status != WelderStatus.NEW_WELDER) return false;

            try
            {
                using (var port = new SerialPort(connectedPort!, connectedBaudRate!.Value, Parity.None, 8, StopBits.One))
                {
                    port.ReadTimeout = 2000;  // Dłuższy timeout ze względu na większą ilość danych
                    port.WriteTimeout = 500;
                    port.Open();
                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();

                    byte[] cmd = WelderCommands.BuildReadConfigCommand(BezSzyfrowania);
                    Log("Wysyłanie komendy odczytu rejestru konfiguracji:");
                    PrintFrameTable(cmd);

                    port.Write(cmd, 0, cmd.Length);

                    // Odbiór do CRLF lub timeout 1s bez znaku
                    string response = ReadResponseToCRLF(port);
                    if (string.IsNullOrEmpty(response))
                    {
                        Log("Błąd: Nie otrzymano odpowiedzi");
                        return false;
                    }

                    if (response.Length > 256)
                    {
                        Log("Błąd: Odpowiedź zbyt długa");
                        return false;
                    }

                    // Kopiuj dane konfiguracji do bufora wyjściowego (bez CRLF)
                    Array.Copy(System.Text.Encoding.ASCII.GetBytes(response), configData, response.Length);

                    Log($"\nOdebrano {response.Length} bajtów:");
                    Log("Offset    00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F    ASCII");
                    Log("--------  -----------------------------------------------    ----------------");

                    for (int i = 0; i < response.Length; i += 16)
                    {
                        // Wyświetl offset
                        var line = $"{i:X8}  ";

                        // Wyświetl bajty w hex
                        for (int j = 0; j < 16; j++)
                        {
                            if (i + j < response.Length)
                                line += $"{response[i + j]:X2} ";
                            else
                                line += "   ";
                        }

                        // Wyświetl ASCII
                        line += "   ";
                        for (int j = 0; j < 16 && i + j < response.Length; j++)
                        {
                            char c = response[i + j];
                            if (c >= 32 && c <= 126)  // Drukowalne znaki ASCII
                                line += c;
                            else
                                line += ".";
                        }
                        Log(line);
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas odczytu rejestru konfiguracji: {ex.Message}");
            }
            return false;
        }

        public int? ReadWeldCount()
        {
            if (status != WelderStatus.CONNECTED && status != WelderStatus.NEW_WELDER) return null;

            try
            {
                using (var port = new SerialPort(connectedPort!, connectedBaudRate!.Value, Parity.None, 8, StopBits.One))
                {
                    port.ReadTimeout = 1000;
                    port.WriteTimeout = 500;
                    port.Open();
                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();

                    byte[] cmd = WelderCommands.BuildReadWeldCountCommand(BezSzyfrowania);
                    Log("Wysyłanie komendy odczytu liczby zgrzewów:");
                    PrintFrameTable(cmd);

                    port.Write(cmd, 0, cmd.Length);

                    // Odbiór do CRLF lub timeout 1s bez znaku
                    string response = ReadResponseToCRLF(port);
                    if (!string.IsNullOrEmpty(response))
                    {
                        // Konwertuj string na liczbę
                        if (int.TryParse(response, out int result))
                        {
                            Log($"Liczba zgrzewów w pamięci: {result}");
                            return result;
                        }
                        else
                        {
                            Log("Błąd: Nie można przekonwertować odpowiedzi na liczbę");
                        }
                    }
                    else
                    {
                        Log("Błąd: Nie otrzymano odpowiedzi");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas odczytu liczby zgrzewów: {ex.Message}");
            }
            return null;
        }

        public static WeldParameters ParseWeldParameters(string response)
        {
            var result = new WeldParameters();
            var parts = response.Split(';');
            foreach (var part in parts)
            {
                var kv = part.Split(':');
                if (kv.Length != 2)
                {
                    continue;
                }
                switch (kv[0])
                {
                    case "U":
                        result.NapiecieZgrzewania = double.Parse(kv[1].Replace('.', ',')); break;
                    case "I":
                        result.PradZgrzewania = double.Parse(kv[1].Replace('.', ',')); break;
                    case "ADCU":
                        result.ADCNapZgrzew = Convert.ToInt32(kv[1], 16); break;
                    case "ADCI":
                        result.ADCPradZgrzew = Convert.ToInt32(kv[1], 16); break;
                    case "IVHC-U":
                        result.IVHC_U = int.Parse(kv[1]); break;
                    case "IVLC-U":
                        result.IVLC_U = int.Parse(kv[1]); break;
                    case "ADCIVHC-U":
                        result.ADCIVHC_U = int.Parse(kv[1]); break;
                    case "ADCIVLC-U":
                        result.ADCIVLC_U = int.Parse(kv[1]); break;
                    case "IVHC-I":
                        result.IMHC_I = int.Parse(kv[1]); break;
                    case "IVLC-I":
                        result.IMLC_I = int.Parse(kv[1]); break;
                    case "ADCIVHC-I":
                        result.ADCIVHC_I = int.Parse(kv[1]); break;
                    case "ADCIVLC-I":
                        result.ADCIVLC_I = int.Parse(kv[1]); break;
                    case "MMWVL":
                        result.MMWVL = int.Parse(kv[1]); break;
                    case "MMWVH":
                        result.MMWVH = int.Parse(kv[1]); break;
                    case "MMWCL":
                        result.MMWCL = int.Parse(kv[1]); break;
                    case "MMWCH":
                        result.MMWCH = int.Parse(kv[1]); break;
                    default:
                        break;
                }
            }
            return result;
        }


        public WeldParameters? ReadWeldParameters(out string? errorDetails)
        {
            errorDetails = null;
            if (status != WelderStatus.CONNECTED && status != WelderStatus.NEW_WELDER)
            {
                errorDetails = "Brak połączenia ze zgrzewarką.";
                return null;
            }
            try
            {
                using (var port = new SerialPort(connectedPort!, connectedBaudRate!.Value, Parity.None, 8, StopBits.One))
                {
                    port.ReadTimeout = 1000;
                    port.WriteTimeout = 500;
                    port.Open();
                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();

                    byte[] cmd = WelderCommands.BuildReadWeldParametersCommand(BezSzyfrowania);
                    Log("Wysyłanie komendy odczytu parametrów zgrzewania:");
                    PrintFrameTable(cmd);

                    port.Write(cmd, 0, cmd.Length);

                    // Odbiór do CRLF lub timeout 1s bez znaku
                    string response = ReadResponseToCRLF(port);
                    if (string.IsNullOrEmpty(response))
                    {
                        errorDetails = "Brak odpowiedzi od zgrzewarki.";
                        return null;
                    }

                    try
                    {
                        var result = ParseWeldParameters(response);
                        if (result == null)
                        {
                            errorDetails = "Nieprawidłowy format odpowiedzi.";
                            return null;
                        }
                        // Logowanie wszystkich parametrów zgrzewania
                        Log($"\nParametry zgrzewania:");
                        Log($"Napięcie zgrzewania: {result.NapiecieZgrzewania:F2} V");
                        Log($"Prąd zgrzewania: {result.PradZgrzewania:F2} A");
                        Log($"\nWartości ADC:");
                        Log($"ADC napięcia zgrzewania: {result.ADCNapZgrzew:X4}");
                        Log($"ADC prądu zgrzewania: {result.ADCPradZgrzew:X4}");
                        Log($"\nParametry kalibracji napięcia:");
                        Log($"IVHC-U: {result.IVHC_U}");
                        Log($"IVLC-U: {result.IVLC_U}");
                        Log($"ADCIVHC-U: {result.ADCIVHC_U}");
                        Log($"ADCIVLC-U: {result.ADCIVLC_U}");
                        Log($"\nParametry kalibracji prądu:");
                        Log($"IVHC-I: {result.IMHC_I}");
                        Log($"IVLC-I: {result.IMLC_I}");
                        Log($"ADCIVHC-I: {result.ADCIVHC_I}");
                        Log($"ADCIVLC-I: {result.ADCIVLC_I}");
                        Log($"\nWartości multimetru:");
                        Log($"MMWVL: {result.MMWVL}");
                        Log($"MMWVH: {result.MMWVH}");
                        Log($"MMWCL: {result.MMWCL}");
                        Log($"MMWCH: {result.MMWCH}");
                        return result;
                    }
                    catch (Exception ex)
                    {
                        errorDetails = $"Błąd parsowania odpowiedzi: {ex.Message}";
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                errorDetails = $"Wyjątek: {ex.Message}";
            }
            return null;
        }

        // Pomocnicza metoda do odbioru odpowiedzi do CRLF lub timeout
        private string ReadResponseToCRLF(SerialPort port, int timeoutMs = 1000)
        {
            StringBuilder responseBuilder = new StringBuilder();
            DateTime lastByteTime = DateTime.Now;
            bool crlfFound = false;
            while ((DateTime.Now - lastByteTime).TotalMilliseconds < timeoutMs)
            {
                if (port.BytesToRead > 0)
                {
                    int b = port.ReadByte();
                    responseBuilder.Append((char)b);
                    lastByteTime = DateTime.Now;
                    if (responseBuilder.Length >= 2 &&
                        responseBuilder[^2] == '\r' && responseBuilder[^1] == '\n')
                    {
                        crlfFound = true;
                        break;
                    }
                }
                else
                {
                    System.Threading.Thread.Sleep(5);
                }
            }
            if (!crlfFound)
                return responseBuilder.ToString();
            return responseBuilder.ToString().TrimEnd('\r', '\n');
        }

        public static string GetWelderName(int index)
        {
            string[] txt_nazwy_zgrzewarek = {
                "n/d                 ", // 0
                "ZK90ECO             ", // 1
                "ZK160ECO            ", // 2
                "ZK250PRO            ", // 3
                "ZK400PRO            ", // 4
                "ZK401SE800          ", // 5
                "ZK401SE1200         ", // 6
                "ZK401SE2000         ", // 7
                "ZK401SE3000         ", // 8
                "EUROTECH800S        ", // 9
                "EUROTECH1200S       ", // 10
                "EUROTECH2000S       ", // 11
                "EUROTECH3000S       ", // 12
                "ZK90PRO             ", // 13
                "ZK160PRO            ", // 14
                "ZK315PRO            "  // 15
            };
            if (index >= 0 && index < txt_nazwy_zgrzewarek.Length)
                return txt_nazwy_zgrzewarek[index].Trim();
            return "n/d";
        }
    }
}