using System;
using System.Threading.Tasks;
using Logger;
using static Logger.LoggerService;

namespace WelderRS232
{
    public class TcpWelderCommunication : IWelderCommunication
    {
        private USRDeviceManager manager;
        private string ip;
        private int port;
        public bool IsConnected { get; private set; }

        public TcpWelderCommunication(string ip, int port = 23)
        {
            this.ip = ip ?? throw new ArgumentNullException(nameof(ip));
            this.port = port;
            manager = new USRDeviceManager(ip, port);
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                IsConnected = await manager.ConnectAsync();
                if (IsConnected)
                {
                    Log($"Połączono z USR-N520 na {ip}:{port}");
                }
                return IsConnected;
            }
            catch (Exception ex)
            {
                Log($"Błąd połączenia z USR-N520 {ip}:{port}: {ex.Message}");
                IsConnected = false;
                return false;
            }
        }

        public async Task SendDataAsync(byte[] data)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Brak połączenia z USR-N520");
            }

            await manager.SendDataAsync(data);
        }

        public async Task<byte[]> ReceiveDataAsync()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Brak połączenia z USR-N520");
            }

            return await manager.ReceiveDataBytesAsync();
        }

        public void Disconnect()
        {
            try
            {
                manager.Disconnect();
                IsConnected = false;
                Log($"Rozłączono z USR-N520 {ip}:{port}");
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas rozłączania z USR-N520 {ip}:{port}: {ex.Message}");
            }
        }
    }
}