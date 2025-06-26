using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Management;
using System.Threading;

namespace WelderRS232
{
    public class USRDeviceManager
    {
        private TcpClient tcpClient;
        private NetworkStream stream;
        private string deviceIP;
        private int devicePort;
        private readonly Action<string> logFn;

        private void Log(string msg) => logFn?.Invoke(msg);

        // Konstruktor domyślny - używa Console.WriteLine
        public USRDeviceManager(string ip, int port = 23) : this(ip, port, Console.WriteLine)
        {
        }

        // Konstruktor z callbackiem logowania
        public USRDeviceManager(string ip, int port = 23, Action<string> logFn = null)
        {
            deviceIP = ip;
            devicePort = port;
            this.logFn = logFn ?? Console.WriteLine;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(deviceIP, devicePort);
                stream = tcpClient.GetStream();
                Log($"Połączono z USR-N520 na {deviceIP}:{devicePort}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Błąd połączenia: {ex.Message}");
                return false;
            }
        }

        public async Task SendDataAsync(string data)
        {
            if (stream != null && stream.CanWrite)
            {
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                await stream.WriteAsync(dataBytes, 0, dataBytes.Length);
            }
        }

        public async Task SendDataAsync(byte[] data)
        {
            if (stream != null && stream.CanWrite)
            {
                await stream.WriteAsync(data, 0, data.Length);
            }
        }

        public async Task<string> ReceiveDataAsync(int timeoutMs = 1000)
        {
            if (stream != null && stream.CanRead)
            {
                try
                {
                    using var cts = new CancellationTokenSource(timeoutMs);
                    byte[] buffer = new byte[1024];
                    var readTask = stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                    var timeoutTask = Task.Delay(timeoutMs, cts.Token);
                    var completedTask = await Task.WhenAny(readTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        Log($"Timeout podczas odbioru danych (>{timeoutMs}ms) - używam CancellationToken");
                        return string.Empty;
                    }
                    if (completedTask == readTask)
                    {
                        int bytesRead = await readTask;
                        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    }
                    return string.Empty;
                }
                catch (OperationCanceledException)
                {
                    Log($"Timeout podczas odbioru danych (>{timeoutMs}ms) - OperationCanceledException");
                    return string.Empty;
                }
                catch (TimeoutException)
                {
                    Log($"Timeout podczas odbioru danych (>{timeoutMs}ms) - TimeoutException");
                    return string.Empty;
                }
                catch (Exception ex)
                {
                    Log($"Błąd podczas odbioru danych: {ex.Message}");
                    Log($"Typ błędu: {ex.GetType().Name}");
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        public async Task<byte[]> ReceiveDataBytesAsync()
        {
            if (stream != null && stream.CanRead)
            {
                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                byte[] result = new byte[bytesRead];
                Array.Copy(buffer, result, bytesRead);
                return result;
            }
            return new byte[0];
        }

        public void Disconnect()
        {
            stream?.Close();
            tcpClient?.Close();
        }

        // Wyszukiwanie urządzeń USR w sieci - rozszerzone dla USR-N520
        public static async Task<List<USRDeviceInfo>> FindUSRDevicesAsync(string networkBase = "192.168.0", int port = 8233, Action<string> logFn = null)
        {
            var foundDevices = new List<USRDeviceInfo>();
            logFn?.Invoke($"Sprawdzam domyślny adres IP 192.168.0.7 na porcie {port}...");
            await CheckUSRDeviceAsync("192.168.0.7", foundDevices, port, logFn);
            if (foundDevices.Count == 0)
            {
                logFn?.Invoke($"Nie znaleziono urządzenia na 192.168.0.7:{port}, skanuję resztę sieci...");
                var tasks = new List<Task>();
                for (int i = 1; i < 255; i++)
                {
                    string ip = $"{networkBase}.{i}";
                    if (ip != "192.168.0.7")
                    {
                        tasks.Add(CheckUSRDeviceAsync(ip, foundDevices, port, logFn));
                    }
                }
                await Task.WhenAll(tasks);
            }
            return foundDevices;
        }

        private static async Task CheckUSRDeviceAsync(string ip, List<USRDeviceInfo> foundDevices, int port = 8233, Action<string> logFn = null)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(ip, 1000);
                    if (reply.Status == IPStatus.Success)
                    {
                        if (await CheckPortAsync(ip, port))
                        {
                            var deviceInfo = new USRDeviceInfo
                            {
                                IP = ip,
                                Port = port,
                                DeviceType = $"USR-N520 (port {port})",
                                IsAccessible = true
                            };
                            foundDevices.Add(deviceInfo);
                            logFn?.Invoke($"Znaleziono urządzenie USR-N520 na: {ip}:{port}");
                        }
                    }
                }
            }
            catch { }
        }

        private static async Task<bool> CheckPortAsync(string ip, int port)
        {
            try
            {
                using (var tcpClient = new TcpClient())
                {
                    var connectTask = tcpClient.ConnectAsync(ip, port);
                    if (await Task.WhenAny(connectTask, Task.Delay(2000)) == connectTask)
                    {
                        return tcpClient.Connected;
                    }
                }
            }
            catch
            {
                // Ignoruj błędy połączenia
            }
            return false;
        }

        // Metoda do skanowania portów RS-232 przez połączenie TCP z USR-N520
        public async Task<List<PortScanResult>> ScanRS232PortsAsync()
        {
            var results = new List<PortScanResult>();

            if (!IsConnected)
            {
                Log("Brak połączenia z urządzeniem USR-N520");
                return results;
            }

            Log("=== ROZPOCZYNAM SKANOWANIE PORTÓW NA USR-N520 ===");
            Log("USR-N520 ma 2 fizyczne porty: RS-232 (9-pin D-sub) i RS-485 (2-wire A+, B-)");
            Log("Sprawdzam aktualną konfigurację i testuję komunikację...");

            try
            {
                // 1. Sprawdź aktualną konfigurację urządzenia
                Log("Sprawdzam aktualną konfigurację USR-N520...");

                // Przełącz do trybu konfiguracyjnego
                await SendDataAsync("+++\r\n");
                await Task.Delay(1000);
                var configResponse = await ReceiveDataAsync();
                Log($"Odpowiedź na +++: '{configResponse}'");

                if (configResponse.Contains("OK") || configResponse.Contains("a"))
                {
                    Log("✓ Urządzenie w trybie konfiguracyjnym");

                    // Sprawdź aktualny tryb portu
                    await SendDataAsync("AT+MODE\r\n");
                    await Task.Delay(200);
                    var modeResponse = await ReceiveDataAsync();
                    Log($"Aktualny tryb portu: {modeResponse}");

                    // Sprawdź aktualną konfigurację UART
                    await SendDataAsync("AT+UART\r\n");
                    await Task.Delay(200);
                    var uartResponse = await ReceiveDataAsync();
                    Log($"Aktualna konfiguracja UART: {uartResponse}");

                    // Wyjdź z trybu konfiguracyjnego
                    await SendDataAsync("ATO\r\n");
                    await Task.Delay(200);
                    var exitResponse = await ReceiveDataAsync();
                    Log($"Wyjście z trybu konfiguracyjnego: {exitResponse}");

                    // 2. Testuj komunikację z różnymi prędkościami
                    int[] baudRates = { 19200, 115200, 9600 };

                    foreach (int baudRate in baudRates)
                    {
                        try
                        {
                            Log($"\n--- PRÓBA KOMUNIKACJI NA {baudRate} BAUD ---");

                            // Wyślij komendę identyfikacyjną do zgrzewarki
                            byte[] identifyCommand = WelderCommands.BuildIdentifyCommand(true); // bez szyfrowania
                            Log($"Wysyłam komendę identyfikacyjną ({identifyCommand.Length} bajtów)");
                            await SendDataAsync(identifyCommand);

                            // Czekaj na odpowiedź
                            Log($"Czekam na odpowiedź...");
                            await Task.Delay(500);

                            // Sprawdź czy otrzymaliśmy odpowiedź
                            var response = await ReceiveDataAsync();
                            Log($"Otrzymana odpowiedź: '{response}'");

                            if (!string.IsNullOrEmpty(response))
                            {
                                bool found = response.Contains("ZGRZ") || response.Contains("AGRE");

                                results.Add(new PortScanResult
                                {
                                    PortName = $"USR-N520-{baudRate}",
                                    BaudRate = baudRate,
                                    Success = found,
                                    Response = response
                                });

                                if (found)
                                {
                                    Log($"✓ ZNALEZIONO ZGRZEWARKĘ na USR-N520, {baudRate} baud: {response}");
                                    // Nie przerywamy - sprawdź wszystkie prędkości
                                }
                                else
                                {
                                    Log($"✗ Nie znaleziono zgrzewarki na {baudRate} baud (odpowiedź: {response})");
                                }
                            }
                            else
                            {
                                Log($"✗ Brak odpowiedzi na {baudRate} baud");
                                results.Add(new PortScanResult
                                {
                                    PortName = $"USR-N520-{baudRate}",
                                    BaudRate = baudRate,
                                    Success = false,
                                    Response = "Brak odpowiedzi"
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"✗ Błąd podczas skanowania na {baudRate} baud: {ex.Message}");
                            results.Add(new PortScanResult
                            {
                                PortName = $"USR-N520-{baudRate}",
                                BaudRate = baudRate,
                                Success = false,
                                Response = $"Błąd: {ex.Message}"
                            });
                        }
                    }
                }
                else
                {
                    Log("✗ Nie udało się przełączyć do trybu konfiguracyjnego");
                    Log("Testuję komunikację bezpośrednio...");

                    // Testuj komunikację bezpośrednio
                    int[] baudRates = { 19200, 115200, 9600 };

                    foreach (int baudRate in baudRates)
                    {
                        try
                        {
                            Log($"\n--- PRÓBA BEZPOŚREDNIEJ KOMUNIKACJI NA {baudRate} BAUD ---");

                            // Wyślij komendę identyfikacyjną do zgrzewarki
                            byte[] identifyCommand = WelderCommands.BuildIdentifyCommand(true);
                            Log($"Wysyłam komendę identyfikacyjną ({identifyCommand.Length} bajtów)");
                            await SendDataAsync(identifyCommand);

                            await Task.Delay(500);
                            var response = await ReceiveDataAsync();
                            Log($"Otrzymana odpowiedź: '{response}'");

                            if (!string.IsNullOrEmpty(response))
                            {
                                bool found = response.Contains("ZGRZ") || response.Contains("AGRE");
                                results.Add(new PortScanResult
                                {
                                    PortName = $"USR-N520-Direct-{baudRate}",
                                    BaudRate = baudRate,
                                    Success = found,
                                    Response = response
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"✗ Błąd podczas bezpośredniej komunikacji na {baudRate} baud: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"✗ Błąd podczas skanowania portów USR-N520: {ex.Message}");
            }

            Log($"=== ZAKOŃCZONO SKANOWANIE USR-N520 ===");
            Log($"Znaleziono {results.Count(r => r.Success)} aktywnych portów z zgrzewarką");
            Log($"Sprawdzono {results.Count} kombinacji port/prędkość");

            // Podsumowanie wyników
            foreach (var result in results)
            {
                string status = result.Success ? "✓ AKTYWNY" : "✗ NIEAKTYWNY";
                Log($"  {result.PortName}: {status} - {result.Response}");
            }

            return results;
        }

        // Metoda do konfiguracji portu RS-232 na USR-N520
        public async Task<bool> ConfigureRS232PortAsync(int baudRate = 19200, int dataBits = 8, int stopBits = 1, string parity = "N", string portMode = "0")
        {
            if (!IsConnected)
                return false;

            try
            {
                Log("=== KONFIGURACJA PORTU RS-232 NA USR-N520 ===");

                // 1. Wejdź w tryb AT
                Log("1. Wchodzę w tryb AT...");
                bool atMode = await EnterATModeAsync();
                if (!atMode)
                {
                    Log("✗ Nie udało się wejść w tryb AT");
                    return false;
                }
                Log("✓ Urządzenie w trybie AT");

                // 2. Przełącz na konkretny port (0=RS-232, 1=RS-485)
                Log($"2. Przełączam na port {portMode} (0=RS-232, 1=RS-485)...");
                string modeCommand = $"AT+MODE={portMode}\r\n";
                await SendDataAsync(modeCommand);
                await Task.Delay(200);

                var modeResponse = await ReceiveDataAsync();
                Log($"Odpowiedź na AT+MODE={portMode}: '{modeResponse}'");
                if (!modeResponse.Contains("OK"))
                {
                    Log($"✗ Nie udało się przełączyć na port {portMode}");
                    await ExitATModeAsync();
                    return false;
                }
                Log($"✓ Przełączono na port {portMode}");

                // 3. Konfiguruj parametry portu
                Log($"3. Konfiguruję parametry UART: {baudRate},{dataBits},{stopBits},{parity}...");
                string configCommand = $"AT+UART={baudRate},{dataBits},{stopBits},{parity}\r\n";
                await SendDataAsync(configCommand);

                // Czekamy na potwierdzenie
                await Task.Delay(200);
                var response = await ReceiveDataAsync();
                Log($"Odpowiedź na AT+UART: '{response}'");

                bool success = response.Contains("OK") || response.Contains("SUCCESS");
                if (success)
                {
                    Log($"✓ Konfiguracja UART udana");
                }
                else
                {
                    Log($"✗ Konfiguracja UART nie powiodła się");
                }

                // 4. Wyjdź z trybu AT
                Log("4. Wychodzę z trybu AT...");
                await ExitATModeAsync();
                Log("✓ Wyszedłem z trybu AT");

                return success;
            }
            catch (Exception ex)
            {
                Log($"✗ Błąd konfiguracji portu {portMode}: {ex.Message}");
                // Próbuj wyjść z trybu AT w przypadku błędu
                try
                {
                    await ExitATModeAsync();
                }
                catch { }
                return false;
            }
        }

        public bool IsConnected => tcpClient?.Connected == true;

        // Metoda do wykrywania portów COM związanych z urządzeniami USR
        public static List<string> FindUSRComPorts()
        {
            var usrPorts = new List<string>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string name = obj["Name"]?.ToString() ?? "";
                        string deviceId = obj["DeviceID"]?.ToString() ?? "";

                        // Szukaj po nazwie zawierającej "USR" lub znanych VID/PID urządzeń USR
                        if (name.Contains("USR", StringComparison.OrdinalIgnoreCase) ||
                            deviceId.Contains("VID_1A86") || // Przykład: CH340 (często używany w konwerterach)
                            deviceId.Contains("VID_0403") || // FTDI
                            deviceId.Contains("VID_10C4"))   // Silicon Labs CP210x
                        {
                            usrPorts.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // W metodzie statycznej nie możemy używać pól instancji
                Console.WriteLine($"Błąd podczas wyszukiwania portów COM USR: {ex.Message}");
            }
            return usrPorts;
        }

        // Wejście w tryb AT USR-N520 z zachowaniem ciszy przed i po
        public async Task<bool> EnterATModeAsync()
        {
            try
            {
                Log("=== WEJŚCIE W TRYB AT ===");
                Log("1. Czekam 1 sekundę ciszy przed komendą +++");
                // 1 sekunda ciszy przed
                await Task.Delay(1000);
                Log("   ✓ Cisza przed komendą zakończona");

                Log("2. Wysyłam komendę +++ (bez CRLF)");
                // Wyślij +++ bez CRLF
                byte[] pluses = System.Text.Encoding.ASCII.GetBytes("+++");
                await stream.WriteAsync(pluses, 0, pluses.Length);
                await stream.FlushAsync();
                Log("   ✓ Komenda +++ wysłana");

                Log("3. Czekam 1 sekundę ciszy po komendzie +++");
                // 1 sekunda ciszy po
                await Task.Delay(1000);
                Log("   ✓ Cisza po komendzie zakończona");

                Log("4. Odbieram odpowiedź (timeout 2 sekundy)");
                Log("   Rozpoczynam odbiór danych...");
                // Odbierz odpowiedź z timeout
                var response = await ReceiveDataAsync(2000);
                Log($"5. Otrzymana odpowiedź: '{response}' (długość: {response.Length})");

                bool success = response.Trim().Equals("a") || response.Trim().Equals("OK", StringComparison.OrdinalIgnoreCase);
                Log($"6. Rezultat wejścia w tryb AT: {(success ? "✓ SUKCES" : "✗ BŁĄD")}");

                if (!success)
                {
                    Log("   Oczekiwana odpowiedź: 'a' lub 'OK'");
                    Log("   Otrzymana odpowiedź: '" + response.Trim() + "'");
                    if (string.IsNullOrEmpty(response))
                    {
                        Log("   Brak odpowiedzi - urządzenie nie przeszło w tryb AT");
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas wejścia w tryb AT: {ex.Message}");
                Log($"Typ błędu: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Log($"Błąd wewnętrzny: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        // Wyjście z trybu AT do trybu danych
        public async Task<bool> ExitATModeAsync()
        {
            try
            {
                Log("=== WYJŚCIE Z TRYBU AT ===");
                Log("1. Wysyłam komendę ATO");
                await SendDataAsync("ATO\r\n");

                Log("2. Czekam 200ms na odpowiedź");
                await Task.Delay(200);

                Log("3. Odbieram odpowiedź (timeout 2 sekundy)");
                var response = await ReceiveDataAsync(2000);
                Log($"4. Otrzymana odpowiedź: '{response}' (długość: {response.Length})");

                // Odpowiedź może być pusta lub OK
                bool success = string.IsNullOrEmpty(response) || response.Contains("OK");
                Log($"5. Rezultat wyjścia z trybu AT: {(success ? "✓ SUKCES" : "✗ BŁĄD")}");

                return success;
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas wyjścia z trybu AT: {ex.Message}");
                Log($"Typ błędu: {ex.GetType().Name}");
                return false;
            }
        }

        // Poprawiona metoda pobierania szczegółowych informacji o portach USR-N520
        public async Task<USRPortInfo> GetPortInfoAsync()
        {
            var portInfo = new USRPortInfo();
            if (!IsConnected)
            {
                Log("Brak połączenia z urządzeniem USR-N520");
                return portInfo;
            }
            Log("=== POBIERANIE INFORMACJI O PORTACH USR-N520 ===");
            try
            {
                // Próba wejścia w tryb AT z timeout
                Log("Próbuję wejść w tryb AT...");
                bool atMode = await EnterATModeAsync();
                if (!atMode)
                {
                    Log("✗ Nie udało się wejść w tryb AT. Urządzenie nie przeszło w stan trybu COMMAND.");
                    Log("Możliwe przyczyny:");
                    Log("  - Urządzenie nie jest w trybie AT");
                    Log("  - Nie ma wystarczającej ciszy przed/po komendzie +++");
                    Log("  - Urządzenie jest zajęte transmisją danych");
                    Log("  - Nieprawidłowa konfiguracja urządzenia");
                    portInfo.DeviceInfo = "Nie udało się wejść w tryb AT - urządzenie nie przeszło w stan trybu COMMAND";
                    return portInfo;
                }
                Log("✓ Urządzenie w trybie komend AT");

                // Pobierz informacje z timeout dla każdej komendy
                Log("Pobieram informacje o urządzeniu...");

                // Wersja firmware
                await SendDataAsync("AT+VER\r\n");
                await Task.Delay(200);
                var versionResponse = await ReceiveDataAsync(3000); // 3 sekundy timeout
                if (string.IsNullOrEmpty(versionResponse))
                {
                    Log("⚠ Timeout podczas pobierania wersji firmware");
                    versionResponse = "Timeout";
                }
                Log($"Wersja firmware: {versionResponse}");

                // Adres MAC
                await SendDataAsync("AT+MAC\r\n");
                await Task.Delay(200);
                var macResponse = await ReceiveDataAsync(3000);
                if (string.IsNullOrEmpty(macResponse))
                {
                    Log("⚠ Timeout podczas pobierania adresu MAC");
                    macResponse = "Timeout";
                }
                Log($"Adres MAC: {macResponse}");

                // Tryb portu
                await SendDataAsync("AT+MODE\r\n");
                await Task.Delay(200);
                var modeConfigResponse = await ReceiveDataAsync(3000);
                if (string.IsNullOrEmpty(modeConfigResponse))
                {
                    Log("⚠ Timeout podczas pobierania trybu portu");
                    modeConfigResponse = "Timeout";
                }
                Log($"Tryb portu: {modeConfigResponse}");

                // Konfiguracja UART
                await SendDataAsync("AT+UART\r\n");
                await Task.Delay(200);
                var uartConfigResponse = await ReceiveDataAsync(3000);
                if (string.IsNullOrEmpty(uartConfigResponse))
                {
                    Log("⚠ Timeout podczas pobierania konfiguracji UART");
                    uartConfigResponse = "Timeout";
                }
                Log($"Konfiguracja UART: {uartConfigResponse}");

                // Adres IP
                await SendDataAsync("AT+IP\r\n");
                await Task.Delay(200);
                var ipResponse = await ReceiveDataAsync(3000);
                if (string.IsNullOrEmpty(ipResponse))
                {
                    Log("⚠ Timeout podczas pobierania adresu IP");
                    ipResponse = "Timeout";
                }
                Log($"Adres IP: {ipResponse}");

                // Port TCP
                await SendDataAsync("AT+PORT\r\n");
                await Task.Delay(200);
                var tcpPortResponse = await ReceiveDataAsync(3000);
                if (string.IsNullOrEmpty(tcpPortResponse))
                {
                    Log("⚠ Timeout podczas pobierania portu TCP");
                    tcpPortResponse = "Timeout";
                }
                Log($"Port TCP: {tcpPortResponse}");

                // Zapisz informacje
                portInfo.FirmwareVersion = versionResponse;
                portInfo.MacAddress = macResponse;
                portInfo.DeviceInfo = $"Mode: {modeConfigResponse}, UART: {uartConfigResponse}";
                portInfo.NetworkInfo = $"IP: {ipResponse}, Port: {tcpPortResponse}";

                if (modeConfigResponse.Contains("0"))
                {
                    portInfo.RS232Status = "Aktywny (RS-232)";
                    portInfo.RS232UART = uartConfigResponse;
                    portInfo.RS232Available = true;
                    portInfo.RS485Status = "Nieaktywny";
                    portInfo.RS485Available = false;
                }
                else if (modeConfigResponse.Contains("1"))
                {
                    portInfo.RS485Status = "Aktywny (RS-485)";
                    portInfo.RS485UART = uartConfigResponse;
                    portInfo.RS485Available = true;
                    portInfo.RS232Status = "Nieaktywny";
                    portInfo.RS232Available = false;
                }

                // Wyjście z trybu AT
                Log("Wychodzę z trybu AT...");
                await ExitATModeAsync();
                Log("✓ Pomyślnie wyszedłem z trybu AT");
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas pobierania informacji o portach: {ex.Message}");
                Log($"Typ błędu: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Log($"Błąd wewnętrzny: {ex.InnerException.Message}");
                }
                portInfo.DeviceInfo = $"Błąd: {ex.Message}";
            }
            Log("=== ZAKOŃCZONO POBIERANIE INFORMACJI ===");
            return portInfo;
        }

        // Struktura do przechowywania informacji o portach USR-N520
        public class USRPortInfo
        {
            // Informacje o porcie RS-232
            public string RS232Status { get; set; } = "Nieznany";
            public string RS232UART { get; set; } = "Nieznany";
            public bool RS232Available { get; set; } = false;
            public bool RS232Busy { get; set; } = false;

            // Informacje o porcie RS-485
            public string RS485Status { get; set; } = "Nieznany";
            public string RS485UART { get; set; } = "Nieznany";
            public bool RS485Available { get; set; } = false;
            public bool RS485Busy { get; set; } = false;

            // Ogólne informacje o urządzeniu
            public string DeviceInfo { get; set; } = "Nieznany";
            public string NetworkInfo { get; set; } = "Nieznany";
            public string FirmwareVersion { get; set; } = "Nieznany";
            public string MacAddress { get; set; } = "Nieznany";

            public override string ToString()
            {
                return $"USR-N520 Port Info:\n" +
                       $"Firmware: {FirmwareVersion}\n" +
                       $"MAC: {MacAddress}\n" +
                       $"RS-232: Status={RS232Status}, UART={RS232UART}, Available={RS232Available}, Busy={RS232Busy}\n" +
                       $"RS-485: Status={RS485Status}, UART={RS485UART}, Available={RS485Available}, Busy={RS485Busy}\n" +
                       $"Device: {DeviceInfo}\n" +
                       $"Network: {NetworkInfo}";
            }
        }
    }

    // Klasa do przechowywania informacji o znalezionych urządzeniach USR
    public class USRDeviceInfo
    {
        public string IP { get; set; } = string.Empty;
        public int Port { get; set; }
        public string DeviceType { get; set; } = string.Empty;
        public bool IsAccessible { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}