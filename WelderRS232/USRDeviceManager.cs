using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Management;

namespace WelderRS232
{
    public class USRDeviceManager
    {
        private TcpClient tcpClient;
        private NetworkStream stream;
        private string deviceIP;
        private int devicePort;

        public USRDeviceManager(string ip, int port = 23)
        {
            deviceIP = ip;
            devicePort = port;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(deviceIP, devicePort);
                stream = tcpClient.GetStream();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd połączenia: {ex.Message}");
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

        public async Task<string> ReceiveDataAsync()
        {
            if (stream != null && stream.CanRead)
            {
                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                return Encoding.UTF8.GetString(buffer, 0, bytesRead);
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
        public static async Task<List<USRDeviceInfo>> FindUSRDevicesAsync(string networkBase = "192.168.0")
        {
            var foundDevices = new List<USRDeviceInfo>();

            // Najpierw sprawdź domyślny adres IP 192.168.0.7
            LogToConsole("Sprawdzam domyślny adres IP 192.168.0.7...");
            await CheckUSRDeviceAsync("192.168.0.7", foundDevices);

            // Jeśli nie znaleziono na domyślnym adresie, skanuj resztę sieci
            if (foundDevices.Count == 0)
            {
                LogToConsole("Nie znaleziono urządzenia na 192.168.0.7, skanuję resztę sieci...");
                var tasks = new List<Task>();

                for (int i = 1; i < 255; i++)
                {
                    string ip = $"{networkBase}.{i}";
                    // Pomiń 192.168.0.7 bo już sprawdziliśmy
                    if (ip != "192.168.0.7")
                    {
                        tasks.Add(CheckUSRDeviceAsync(ip, foundDevices));
                    }
                }

                await Task.WhenAll(tasks);
            }

            return foundDevices;
        }

        private static async Task CheckUSRDeviceAsync(string ip, List<USRDeviceInfo> foundDevices)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(ip, 1000);
                    if (reply.Status == IPStatus.Success)
                    {
                        // Sprawdź tylko port 23 (Telnet) - domyślny port USR-N520
                        if (await CheckPortAsync(ip, 23))
                        {
                            var deviceInfo = new USRDeviceInfo
                            {
                                IP = ip,
                                Port = 23,
                                DeviceType = "USR-N520 (Telnet)",
                                IsAccessible = true
                            };
                            foundDevices.Add(deviceInfo);
                            LogToConsole($"Znaleziono urządzenie USR-N520 na: {ip}:23");
                        }
                    }
                }
            }
            catch
            {
                // Ignoruj błędy skanowania
            }
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

        private static void LogToConsole(string message)
        {
            Console.WriteLine($"[USR-N520] {message}");
        }

        // Metoda do skanowania portów RS-232 przez połączenie TCP z USR-N520
        public async Task<List<PortScanResult>> ScanRS232PortsAsync()
        {
            var results = new List<PortScanResult>();

            if (!IsConnected)
            {
                Console.WriteLine("Brak połączenia z urządzeniem USR-N520");
                return results;
            }

            // USR-N520 ma 2 porty: RS-232 (MODE=0) i RS-485 (MODE=1)
            // Sprawdzamy oba porty
            string[] portModes = { "0", "1" }; // 0=RS-232, 1=RS-485
            int[] baudRates = { 19200, 115200, 9600 };

            foreach (string portMode in portModes)
            {
                try
                {
                    // 1. Przełącz na konkretny port
                    string modeCommand = $"AT+MODE={portMode}\r\n";
                    await SendDataAsync(modeCommand);
                    await Task.Delay(200);

                    // Sprawdź czy przełączenie się udało
                    var modeResponse = await ReceiveDataAsync();
                    if (!modeResponse.Contains("OK"))
                    {
                        Console.WriteLine($"Nie udało się przełączyć na port {portMode}");
                        continue;
                    }

                    Console.WriteLine($"Przełączono na port {portMode} ({(portMode == "0" ? "RS-232" : "RS-485")})");

                    foreach (int baudRate in baudRates)
                    {
                        try
                        {
                            // 2. Konfiguruj parametry portu
                            string configCommand = $"AT+UART={baudRate},8,1,N\r\n";
                            await SendDataAsync(configCommand);
                            await Task.Delay(100);

                            var configResponse = await ReceiveDataAsync();
                            if (!configResponse.Contains("OK"))
                            {
                                Console.WriteLine($"Nie udało się skonfigurować portu {portMode} na {baudRate} baud");
                                continue;
                            }

                            // 3. Wyślij komendę identyfikacyjną do zgrzewarki
                            byte[] identifyCommand = WelderCommands.BuildIdentifyCommand(true); // bez szyfrowania
                            await SendDataAsync(identifyCommand);

                            // 4. Czekaj na odpowiedź
                            await Task.Delay(500);

                            // 5. Sprawdź czy otrzymaliśmy odpowiedź
                            var response = await ReceiveDataAsync();
                            if (!string.IsNullOrEmpty(response))
                            {
                                bool found = response.Contains("ZGRZ") || response.Contains("AGRE");
                                string portName = portMode == "0" ? "RS-232" : "RS-485";

                                results.Add(new PortScanResult
                                {
                                    PortName = $"USR-N520-{portName}-{baudRate}",
                                    BaudRate = baudRate,
                                    Success = found,
                                    Response = response
                                });

                                if (found)
                                {
                                    Console.WriteLine($"Znaleziono zgrzewarkę na USR-N520 {portName}, {baudRate} baud: {response}");
                                    // Nie przerywamy - sprawdź wszystkie prędkości na tym porcie
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Błąd podczas skanowania portu {portMode} na {baudRate} baud: {ex.Message}");
                            results.Add(new PortScanResult
                            {
                                PortName = $"USR-N520-{(portMode == "0" ? "RS-232" : "RS-485")}-{baudRate}",
                                BaudRate = baudRate,
                                Success = false,
                                Response = $"Błąd: {ex.Message}"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd podczas przełączania na port {portMode}: {ex.Message}");
                }
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
                // 1. Przełącz na konkretny port (0=RS-232, 1=RS-485)
                string modeCommand = $"AT+MODE={portMode}\r\n";
                await SendDataAsync(modeCommand);
                await Task.Delay(200);

                var modeResponse = await ReceiveDataAsync();
                if (!modeResponse.Contains("OK"))
                {
                    Console.WriteLine($"Nie udało się przełączyć na port {portMode}");
                    return false;
                }

                // 2. Konfiguruj parametry portu
                string configCommand = $"AT+UART={baudRate},{dataBits},{stopBits},{parity}\r\n";
                await SendDataAsync(configCommand);

                // Czekamy na potwierdzenie
                await Task.Delay(200);
                var response = await ReceiveDataAsync();

                return response.Contains("OK") || response.Contains("SUCCESS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd konfiguracji portu {portMode}: {ex.Message}");
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
                Console.WriteLine($"Błąd podczas wyszukiwania portów COM USR: {ex.Message}");
            }
            return usrPorts;
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