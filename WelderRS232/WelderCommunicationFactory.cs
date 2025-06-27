using System;
using System.Threading.Tasks;

namespace WelderRS232
{
    public static class WelderCommunicationFactory
    {
        public static IWelderCommunication CreateCommunication(WelderSettings settings)
        {
            if (string.IsNullOrEmpty(settings.CommType))
            {
                throw new InvalidOperationException("Typ komunikacji nie jest określony w ustawieniach");
            }

            switch (settings.CommType.ToUpper())
            {
                case "COM":
                    if (string.IsNullOrEmpty(settings.COM_Port))
                        throw new InvalidOperationException("Port COM nie jest określony w ustawieniach");

                    int baudRate = settings.COM_Baud ?? 115200;
                    return new SerialWelderCommunication(settings.COM_Port, baudRate);

                case "TCP":
                    if (string.IsNullOrEmpty(settings.USR_IP))
                        throw new InvalidOperationException("Adres IP USR nie jest określony w ustawieniach");

                    int port = settings.USR_Port ?? 23;
                    return new TcpWelderCommunication(settings.USR_IP, port);

                default:
                    throw new InvalidOperationException($"Nieznany typ komunikacji: {settings.CommType}");
            }
        }

        public static async Task<IWelderCommunication?> CreateCommunicationAsync(WelderSettings settings)
        {
            try
            {
                return CreateCommunication(settings);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static IWelderCommunication CreateSerialCommunication(string portName, int baudRate)
        {
            return new SerialWelderCommunication(portName, baudRate);
        }

        public static IWelderCommunication CreateTcpCommunication(string ip, int port = 23)
        {
            return new TcpWelderCommunication(ip, port);
        }
    }
}