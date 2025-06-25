using System.Threading.Tasks;

namespace WelderRS232
{
    public interface IWelderCommunication
    {
        Task<bool> ConnectAsync();
        Task SendDataAsync(byte[] data);
        Task<byte[]> ReceiveDataAsync();
        void Disconnect();
        bool IsConnected { get; }
    }
}