using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Threading.Tasks;
using Calibrator.Services;

namespace Calibrator.Controls
{
    public partial class CommunicationTab : UserControl
    {
        private LocalTcpServerService tcpServerService = new LocalTcpServerService();

        public CommunicationTab()
        {
            InitializeComponent();
            BtnSendSampleData.Click += BtnSendSampleData_Click;
        }

        public void SetTcpServerService(LocalTcpServerService service)
        {
            tcpServerService = service;
        }

        public void UpdateStatus(string status, bool isActive)
        {
            TxtTcpServerStatus.Text = status;
            TxtTcpServerStatus.Foreground = isActive ? Brushes.Green : Brushes.Red;
        }

        public string ServerIp => TxtTcpServerIp.Text.Trim();
        public int ServerPort => int.TryParse(TxtTcpServerPort.Text.Trim(), out int p) ? p : 20108;

        private async void BtnSendSampleData_Click(object sender, RoutedEventArgs e)
        {
            await tcpServerService.SendToAllAsync("Przykładowe dane");
        }

        // Publiczne właściwości dla dostępu z MainWindow
        public TextBox TxtTcpServerIp => txtTcpServerIp;
        public TextBox TxtTcpServerPort => txtTcpServerPort;
        public TextBlock TxtTcpServerStatus => txtTcpServerStatus;
        public Button BtnSendSampleData => btnSendSampleData;
    }
}