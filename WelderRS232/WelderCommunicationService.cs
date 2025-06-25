using System.Threading.Tasks;

namespace WelderRS232
{
    public class WelderCommunicationService
    {
        private IWelderCommunication communication;

        public WelderCommunicationService(IWelderCommunication comm)
        {
            communication = comm;
        }

        public async Task<bool> ConnectAsync() => await communication.ConnectAsync();
        public async Task SendDataAsync(byte[] data) => await communication.SendDataAsync(data);
        public async Task<byte[]> ReceiveDataAsync() => await communication.ReceiveDataAsync();
        public void Disconnect() => communication.Disconnect();
        public bool IsConnected => communication.IsConnected;
    }
}