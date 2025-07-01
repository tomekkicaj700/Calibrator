using System.Windows;
using System.Windows.Controls;
using Logger;
using System.IO;
using System.Text.Json;

namespace Calibrator.Controls
{
    public partial class LogPanel : UserControl
    {
        public bool EnableLogging { get; set; } = true;

        public LogPanel()
        {
            InitializeComponent();
            // Odczytaj stan logowania z ustawień
            var settings = Calibrator.WindowSettings.Load();
            if (settings.LogEnabled.HasValue)
                EnableLogging = LoggerService.Instance.EnableLogging = settings.LogEnabled.Value;
            if (btnToggleLog != null)
                btnToggleLog.Content = EnableLogging ? "Wyłącz log" : "Włącz log";
            btnToggleLog.Background = EnableLogging ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Green;
            btnToggleLog.Foreground = System.Windows.Media.Brushes.White;
        }

        public void AppendLog(string text)
        {
            if (!EnableLogging) return;
            txtLog.AppendText(text);
            txtLog.ScrollToEnd();
        }

        public void ClearLog()
        {
            txtLog.Clear();
        }

        private void btnClearLog_Click(object sender, RoutedEventArgs e)
        {
            ClearLog();
        }

        private void btnClearLogFile_Click(object sender, RoutedEventArgs e)
        {
            // Wywołaj logikę czyszczenia pliku log.txt (można podpiąć event do MainWindow)
            ClearLog();
        }

        private void btnCopyLog_Click(object sender, RoutedEventArgs e)
        {
            if (txtLog == null) return;
            Clipboard.SetText(txtLog.Text);
        }

        private void btnCopySelected_Click(object sender, RoutedEventArgs e)
        {
            if (txtLog == null) return;
            if (!string.IsNullOrEmpty(txtLog.SelectedText))
                Clipboard.SetText(txtLog.SelectedText);
        }

        private void btnToggleLog_Click(object sender, RoutedEventArgs e)
        {
            EnableLogging = !EnableLogging;
            LoggerService.Instance.EnableLogging = EnableLogging;
            if (btnToggleLog != null)
            {
                btnToggleLog.Content = EnableLogging ? "Wyłącz log" : "Włącz log";
                btnToggleLog.Background = EnableLogging ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Green;
                btnToggleLog.Foreground = System.Windows.Media.Brushes.White;
            }
            // Zapisz stan do ustawień
            var settings = Calibrator.WindowSettings.Load();
            settings.LogEnabled = EnableLogging;
            settings.Save();
        }

        public string GetLogText()
        {
            return txtLog == null ? string.Empty : txtLog.Text;
        }

        public string GetSelectedText()
        {
            return txtLog == null ? string.Empty : txtLog.SelectedText;
        }
    }
}