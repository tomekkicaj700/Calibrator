using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Logger;
using static Logger.LoggerService;

namespace WelderRS232
{
    public class WelderCommunicationManager
    {
        private WelderCommunicationService? currentCommunication;
        private WelderSettings settings;
        private bool isConnected = false;

        public WelderCommunicationManager()
        {
            settings = WelderSettings.Load();
        }

        public bool IsConnected => isConnected && currentCommunication?.IsConnected == true;

        public async Task<bool> ConnectWithSavedSettingsAsync()
        {
            if (string.IsNullOrEmpty(settings.CommType))
            {
                Log("Brak zapisanych ustawień komunikacji");
                return false;
            }

            try
            {
                var communication = WelderCommunicationFactory.CreateCommunication(settings);
                currentCommunication = new WelderCommunicationService(communication);

                isConnected = await currentCommunication.ConnectAsync();
                return isConnected;
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas łączenia z zapisanymi ustawieniami: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ConnectAsync(string commType, string? portName = null, int? baudRate = null, string? ip = null, int? port = null)
        {
            try
            {
                IWelderCommunication communication;

                switch (commType.ToUpper())
                {
                    case "COM":
                        if (string.IsNullOrEmpty(portName))
                            throw new ArgumentException("Nazwa portu COM jest wymagana");

                        int baud = baudRate ?? 115200;
                        communication = WelderCommunicationFactory.CreateSerialCommunication(portName, baud);
                        break;

                    case "TCP":
                        if (string.IsNullOrEmpty(ip))
                            throw new ArgumentException("Adres IP jest wymagany");

                        int tcpPort = port ?? 23;
                        communication = WelderCommunicationFactory.CreateTcpCommunication(ip, tcpPort);
                        break;

                    default:
                        throw new ArgumentException($"Nieznany typ komunikacji: {commType}");
                }

                currentCommunication = new WelderCommunicationService(communication);
                isConnected = await currentCommunication.ConnectAsync();

                if (isConnected)
                {
                    // Zapisz ustawienia po udanym połączeniu
                    SaveConnectionSettings(commType, portName, baudRate, ip, port);
                }

                return isConnected;
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas łączenia: {ex.Message}");
                return false;
            }
        }

        public async Task<string> SendCommandAndReceiveResponseAsync(byte[] command, int timeoutMs = 2000)
        {
            if (currentCommunication == null || !isConnected)
            {
                throw new InvalidOperationException("Brak aktywnego połączenia");
            }

            return await currentCommunication.SendCommandAndReceiveResponseAsync(command, timeoutMs);
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (!isConnected || currentCommunication == null)
                return false;

            try
            {
                // Wyślij komendę identyfikacyjną
                byte[] cmd = WelderCommands.BuildIdentifyCommand(true); // Bez szyfrowania
                string response = await currentCommunication.SendCommandAndReceiveResponseAsync(cmd, 2000);

                bool isValidResponse = response.Contains("ZGRZ") || response.Contains("AGRE");
                Log($"Test połączenia: {(isValidResponse ? "SUKCES" : "BŁĄD")} - Odpowiedź: {response}");

                return isValidResponse;
            }
            catch (Exception ex)
            {
                Log($"Błąd testu połączenia: {ex.Message}");
                return false;
            }
        }

        public async Task<List<PortScanResult>> ScanComPortsAsync(string? preferredPort = null, int? preferredBaud = null)
        {
            var results = new List<PortScanResult>();
            var availablePorts = System.IO.Ports.SerialPort.GetPortNames();
            var baudsToScan = new int[] { 9600, 19200, 38400, 57600, 115200 };

            // Jeśli podano preferowany port i baud rate, spróbuj go najpierw
            if (!string.IsNullOrEmpty(preferredPort) && preferredBaud.HasValue)
            {
                Log($"Testuję preferowany port: {preferredPort} ({preferredBaud} baud)");
                var result = await TestComPortAsync(preferredPort, preferredBaud.Value);
                results.Add(result);

                if (result.Success)
                {
                    Log($"✓ Znaleziono zgrzewarkę na preferowanym porcie {preferredPort}");
                    return results;
                }
            }

            // Skanuj wszystkie porty
            foreach (var portName in availablePorts)
            {
                foreach (var baud in baudsToScan)
                {
                    Log($"Testuję port: {portName} ({baud} baud)");
                    var result = await TestComPortAsync(portName, baud);
                    results.Add(result);

                    if (result.Success)
                    {
                        Log($"✓ Znaleziono zgrzewarkę na porcie {portName} ({baud} baud)");
                        return results;
                    }
                }
            }

            Log("✗ Nie znaleziono zgrzewarki na żadnym porcie COM");
            return results;
        }

        public async Task<List<PortScanResult>> ScanUSRDevicesAsync()
        {
            var results = new List<PortScanResult>();

            // Testuj standardowy adres USR-N520
            string usrIp = "192.168.0.7";
            int usrPort = 23;

            Log($"Testuję USR-N520 na {usrIp}:{usrPort}");
            var result = await TestTcpConnectionAsync(usrIp, usrPort);
            results.Add(result);

            if (result.Success)
            {
                Log($"✓ Znaleziono zgrzewarkę przez USR-N520 na {usrIp}:{usrPort}");
            }
            else
            {
                Log("✗ Nie znaleziono zgrzewarki przez USR-N520");
            }

            return results;
        }

        public void Disconnect()
        {
            try
            {
                currentCommunication?.Disconnect();
                isConnected = false;
                currentCommunication = null;
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas rozłączania: {ex.Message}");
            }
        }

        private async Task<PortScanResult> TestComPortAsync(string portName, int baudRate)
        {
            try
            {
                var communication = WelderCommunicationFactory.CreateSerialCommunication(portName, baudRate);
                var commService = new WelderCommunicationService(communication);

                if (await commService.ConnectAsync())
                {
                    byte[] cmd = WelderCommands.BuildIdentifyCommand(true);
                    string response = await commService.SendCommandAndReceiveResponseAsync(cmd, 2000);
                    bool success = response.Contains("ZGRZ") || response.Contains("AGRE");

                    commService.Disconnect();

                    return new PortScanResult
                    {
                        PortName = portName,
                        BaudRate = baudRate,
                        Success = success,
                        Response = response
                    };
                }
            }
            catch (Exception ex)
            {
                Log($"Błąd testu portu {portName}: {ex.Message}");
            }

            return new PortScanResult
            {
                PortName = portName,
                BaudRate = baudRate,
                Success = false,
                Response = "Błąd połączenia"
            };
        }

        private async Task<PortScanResult> TestTcpConnectionAsync(string ip, int port)
        {
            try
            {
                var communication = WelderCommunicationFactory.CreateTcpCommunication(ip, port);
                var commService = new WelderCommunicationService(communication);

                if (await commService.ConnectAsync())
                {
                    byte[] cmd = WelderCommands.BuildIdentifyCommand(true);
                    string response = await commService.SendCommandAndReceiveResponseAsync(cmd, 2000);
                    bool success = response.Contains("ZGRZ") || response.Contains("AGRE");

                    commService.Disconnect();

                    return new PortScanResult
                    {
                        PortName = $"USR-N520 {ip}:{port}",
                        BaudRate = 0,
                        Success = success,
                        Response = response
                    };
                }
            }
            catch (Exception ex)
            {
                Log($"Błąd testu TCP {ip}:{port}: {ex.Message}");
            }

            return new PortScanResult
            {
                PortName = $"USR-N520 {ip}:{port}",
                BaudRate = 0,
                Success = false,
                Response = "Błąd połączenia"
            };
        }

        private void SaveConnectionSettings(string commType, string? portName, int? baudRate, string? ip, int? port)
        {
            settings.CommType = commType;

            if (commType.ToUpper() == "COM")
            {
                settings.COM_Port = portName;
                settings.COM_Baud = baudRate;
            }
            else if (commType.ToUpper() == "TCP")
            {
                settings.USR_IP = ip;
                settings.USR_Port = port;
            }

            settings.Save();
            Log("Zapisano ustawienia komunikacji");
        }
    }
}