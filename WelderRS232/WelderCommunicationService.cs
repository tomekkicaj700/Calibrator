using System;
using System.Threading.Tasks;
using System.Text;
using Logger;
using static Logger.LoggerService;

namespace WelderRS232
{
    public class WelderCommunicationService
    {
        private IWelderCommunication communication;
        private bool isConnected = false;

        public WelderCommunicationService(IWelderCommunication comm)
        {
            communication = comm ?? throw new ArgumentNullException(nameof(comm));
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                isConnected = await communication.ConnectAsync();
                if (isConnected)
                {
                    Log($"Połączono przez {communication.GetType().Name}");
                }
                return isConnected;
            }
            catch (Exception ex)
            {
                Log($"Błąd połączenia: {ex.Message}");
                isConnected = false;
                return false;
            }
        }

        public async Task SendDataAsync(byte[] data)
        {
            if (!isConnected)
            {
                throw new InvalidOperationException("Brak połączenia");
            }
            await communication.SendDataAsync(data);
        }

        public async Task<byte[]> ReceiveDataAsync()
        {
            if (!isConnected)
            {
                throw new InvalidOperationException("Brak połączenia");
            }
            return await communication.ReceiveDataAsync();
        }

        public async Task<string> ReceiveDataAsStringAsync()
        {
            var data = await ReceiveDataAsync();
            return Encoding.ASCII.GetString(data);
        }

        public async Task<string> SendCommandAndReceiveResponseAsync(byte[] command, int timeoutMs = 2000)
        {
            try
            {
                await SendDataAsync(command);

                // Implementacja timeout dla odbioru danych
                var receiveTask = ReceiveDataAsStringAsync();
                var timeoutTask = Task.Delay(timeoutMs);

                var completedTask = await Task.WhenAny(receiveTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException($"Timeout {timeoutMs}ms podczas odbioru odpowiedzi");
                }

                return await receiveTask;
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas wysyłania komendy: {ex.Message}");
                throw;
            }
        }

        public void Disconnect()
        {
            try
            {
                communication.Disconnect();
                isConnected = false;
                Log("Rozłączono komunikację");
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas rozłączania: {ex.Message}");
            }
        }

        public bool IsConnected => isConnected && communication.IsConnected;

        public void Dispose()
        {
            Disconnect();
        }
    }
}