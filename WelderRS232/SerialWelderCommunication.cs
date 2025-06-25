using System.IO.Ports;
using System.Threading.Tasks;
using System;

namespace WelderRS232
{
    public class SerialWelderCommunication : IWelderCommunication
    {
        private SerialPort port;
        private string portName;
        private int baudRate;
        public bool IsConnected => port != null && port.IsOpen;

        public SerialWelderCommunication(string portName, int baudRate)
        {
            this.portName = portName;
            this.baudRate = baudRate;
        }

        public async Task<bool> ConnectAsync()
        {
            port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
            port.Open();
            return port.IsOpen;
        }

        public async Task SendDataAsync(byte[] data)
        {
            port.Write(data, 0, data.Length);
        }

        public async Task<byte[]> ReceiveDataAsync()
        {
            var buffer = new byte[1024];
            int bytesRead = await port.BaseStream.ReadAsync(buffer, 0, buffer.Length);
            var result = new byte[bytesRead];
            Array.Copy(buffer, result, bytesRead);
            return result;
        }

        public void Disconnect()
        {
            port?.Close();
        }
    }
}