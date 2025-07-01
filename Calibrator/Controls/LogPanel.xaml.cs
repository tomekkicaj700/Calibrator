using System.Windows;
using System.Windows.Controls;
using Logger;

namespace Calibrator.Controls
{
    public partial class LogPanel : UserControl
    {
        public bool EnableLogging { get; set; } = true;

        public LogPanel()
        {
            InitializeComponent();
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

        private void chkShowLog_Checked(object sender, RoutedEventArgs e)
        {
            EnableLogging = true;
            LoggerService.Instance.EnableLogging = true;
        }

        private void chkShowLog_Unchecked(object sender, RoutedEventArgs e)
        {
            EnableLogging = false;
            LoggerService.Instance.EnableLogging = false;
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