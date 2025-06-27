using System.IO.Ports;
using System.Threading.Tasks;
using System;
using System.Text;
using Logger;
using static Logger.LoggerService;

namespace WelderRS232
{
    public class SerialWelderCommunication : IWelderCommunication
    {
        private SerialPort? port;
        private string portName;
        private int baudRate;
        public bool IsConnected => port != null && port.IsOpen;

        public SerialWelderCommunication(string portName, int baudRate)
        {
            this.portName = portName ?? throw new ArgumentNullException(nameof(portName));
            this.baudRate = baudRate;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                port.ReadTimeout = 2000;
                port.WriteTimeout = 500;
                port.Open();
                port.DiscardInBuffer();
                port.DiscardOutBuffer();

                Log($"Połączono z portem COM {portName} ({baudRate} baud)");
                return port.IsOpen;
            }
            catch (Exception ex)
            {
                Log($"Błąd połączenia z portem COM {portName}: {ex.Message}");
                return false;
            }
        }

        public async Task SendDataAsync(byte[] data)
        {
            if (port == null || !port.IsOpen)
            {
                throw new InvalidOperationException("Port nie jest otwarty");
            }

            port.Write(data, 0, data.Length);
            await Task.CompletedTask; // Dla kompatybilności z interfejsem
        }

        public async Task<byte[]> ReceiveDataAsync()
        {
            if (port == null || !port.IsOpen)
            {
                throw new InvalidOperationException("Port nie jest otwarty");
            }

            // Implementacja odbioru do CRLF lub timeout
            var response = ReadResponseToCRLF(port, 2000);
            return Encoding.ASCII.GetBytes(response);
        }

        public void Disconnect()
        {
            try
            {
                port?.Close();
                port?.Dispose();
                port = null;
                Log($"Rozłączono z portem COM {portName}");
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas rozłączania z portem COM {portName}: {ex.Message}");
            }
        }

        // Pomocnicza metoda do odbioru odpowiedzi do CRLF lub timeout
        private string ReadResponseToCRLF(SerialPort port, int timeoutMs = 1000)
        {
            StringBuilder responseBuilder = new StringBuilder();
            DateTime startTime = DateTime.Now;
            DateTime lastByteTime = DateTime.Now;
            bool crlfFound = false;

            while ((DateTime.Now - lastByteTime).TotalMilliseconds < timeoutMs)
            {
                if (port.BytesToRead > 0)
                {
                    int b = port.ReadByte();
                    responseBuilder.Append((char)b);
                    lastByteTime = DateTime.Now;  // Resetuj timeout od ostatniego bajtu

                    if (responseBuilder.Length >= 2 &&
                        responseBuilder[^2] == '\r' && responseBuilder[^1] == '\n')
                    {
                        crlfFound = true;
                        break;
                    }
                }
                else
                {
                    // Jeśli nie ma danych, sprawdź czy minął timeout od ostatniego bajtu
                    if ((DateTime.Now - lastByteTime).TotalMilliseconds >= timeoutMs)
                    {
                        break;
                    }
                    System.Threading.Thread.Sleep(10);
                }
            }

            string result = responseBuilder.ToString();
            Log($"Odebrano {result.Length} znaków z COM {portName}, CRLF: {crlfFound}, Timeout: {(DateTime.Now - startTime).TotalMilliseconds:F0}ms");
            return result;
        }
    }
}