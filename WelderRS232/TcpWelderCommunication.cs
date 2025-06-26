using System;
using System.Threading.Tasks;

namespace WelderRS232
{
    public class TcpWelderCommunication : IWelderCommunication
    {
        private USRDeviceManager manager;
        public bool IsConnected { get; private set; }

        public TcpWelderCommunication(string ip, int port = 23) : this(ip, port, Console.WriteLine)
        {
        }

        public TcpWelderCommunication(string ip, int port, Action<string> logFn)
        {
            manager = new USRDeviceManager(ip, port, logFn);
        }

        public async Task<bool> ConnectAsync()
        {
            IsConnected = await manager.ConnectAsync();
            return IsConnected;
        }

        public async Task SendDataAsync(byte[] data)
        {
            await manager.SendDataAsync(data);
        }

        public async Task<byte[]> ReceiveDataAsync()
        {
            return await manager.ReceiveDataBytesAsync();
        }

        public void Disconnect()
        {
            manager.Disconnect();
            IsConnected = false;
        }
    }
}