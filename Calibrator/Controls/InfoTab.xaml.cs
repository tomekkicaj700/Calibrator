using System.Windows.Controls;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;

namespace Calibrator.Controls
{
    public partial class InfoTab : UserControl
    {
        public InfoTab()
        {
            InitializeComponent();
            Loaded += InfoTab_Loaded;
        }

        private async void InfoTab_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            await InitializeInfoWebView();
        }

        private async Task InitializeInfoWebView()
        {
            string htmlFile = "InfoContent.html";
            if (File.Exists(htmlFile))
            {
                string htmlContent = await File.ReadAllTextAsync(htmlFile);
                await InfoWebViewControl.EnsureCoreWebView2Async();
                InfoWebViewControl.NavigateToString(htmlContent);
            }
        }

        // Publiczne właściwości dla dostępu z MainWindow
        public WebView2 InfoWebViewControl => InfoWebView;
    }
}