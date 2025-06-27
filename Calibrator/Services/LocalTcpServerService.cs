using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using static Logger.LoggerService;

namespace Calibrator.Services
{
    public class LocalTcpServerService
    {
        private TcpListener? tcpServer;
        private CancellationTokenSource? tcpServerCts;
        private readonly ConcurrentBag<TcpClient> tcpClients = new ConcurrentBag<TcpClient>();
        public bool IsRunning { get; private set; } = false;

        public event Action<TcpClient>? ClientConnected;
        public event Action<TcpClient, string>? DataReceived;
        public event Action<TcpClient>? ClientDisconnected;

        public async Task<bool> StartAsync(string ip, int port)
        {
            if (IsRunning) return false;
            try
            {
                tcpServerCts = new CancellationTokenSource();
                tcpServer = new TcpListener(IPAddress.Parse(ip), port);
                tcpServer.Start();
                IsRunning = true;
                Log($"[TCP SERVER] Nasłuchuję na {ip}:{port}");
                _ = AcceptTcpClientsAsync(tcpServer, tcpServerCts.Token);
                return true;
            }
            catch (Exception ex)
            {
                Log($"[TCP SERVER] Błąd uruchamiania: {ex.Message}");
                IsRunning = false;
                return false;
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;
            try
            {
                tcpServerCts?.Cancel();
                tcpServer?.Stop();
                IsRunning = false;
                Log("[TCP SERVER] Serwer zatrzymany.");
            }
            catch (Exception ex)
            {
                Log($"[TCP SERVER] Błąd zatrzymywania: {ex.Message}");
            }
        }

        private async Task AcceptTcpClientsAsync(TcpListener listener, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    tcpClients.Add(client);
                    ClientConnected?.Invoke(client);
                    _ = HandleTcpClientAsync(client, token);
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                Log($"[TCP SERVER] Błąd AcceptTcpClients: {ex.Message}");
            }
        }

        private async Task HandleTcpClientAsync(TcpClient client, CancellationToken token)
        {
            var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "?";
            Log($"[TCP SERVER] Połączono z {endpoint}");
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    byte[] buffer = new byte[4096];
                    while (!token.IsCancellationRequested)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                        if (bytesRead == 0) break; // rozłączono
                        string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Log($"[TCP SERVER] Otrzymano od {endpoint}: {data}");
                        DataReceived?.Invoke(client, data);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[TCP SERVER] Błąd klienta {endpoint}: {ex.Message}");
            }
            ClientDisconnected?.Invoke(client);
            Log($"[TCP SERVER] Rozłączono {endpoint}");
        }

        public async Task<int> SendToAllAsync(string message)
        {
            if (!IsRunning) return 0;
            byte[] data = Encoding.UTF8.GetBytes(message);
            int sent = 0;
            foreach (var client in tcpClients)
            {
                try
                {
                    if (client.Connected)
                    {
                        await client.GetStream().WriteAsync(data, 0, data.Length);
                        sent++;
                    }
                }
                catch (Exception ex)
                {
                    Log($"[TCP SERVER] Błąd wysyłania do klienta: {ex.Message}");
                }
            }
            Log($"[TCP SERVER] Wysłano dane do {sent} klient(ów).");
            return sent;
        }

        public int ConnectedClientsCount => tcpClients.Count;
    }
}