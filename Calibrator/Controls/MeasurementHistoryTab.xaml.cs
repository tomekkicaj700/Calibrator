using System.Windows.Controls;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

namespace Calibrator.Controls
{
    public partial class MeasurementHistoryTab : UserControl
    {
        private List<WelderService.CalibrationRecord> calibrationHistory = new();
        private List<WelderService.CalibrationRecord> filteredHistory = new();

        public MeasurementHistoryTab()
        {
            InitializeComponent();
            DataGridHistory.LoadingRow += DataGridHistory_LoadingRow;
            BtnRefreshHistory.Click += BtnRefreshHistory_Click;
            BtnClearHistory.Click += BtnClearHistory_Click;
            BtnOpenFileHistory.Click += BtnOpenFileHistory_Click;
            BtnToggleDetails.Click += BtnToggleDetails_Click;
            BtnClearFilter.Click += BtnClearFilter_Click;
            TxtFilterDeviceType.TextChanged += Filter_TextChanged;
            TxtFilterSerialNumber.TextChanged += Filter_TextChanged;
            DataGridHistory.MouseDoubleClick += DataGridHistory_MouseDoubleClick;
            DataGridHistory.SelectionChanged += DataGridHistory_SelectionChanged;
        }

        public void SetHistory(List<WelderService.CalibrationRecord> history)
        {
            calibrationHistory = history;
            ApplyFilter();
        }

        private void BtnRefreshHistory_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            calibrationHistory.Clear();
            ApplyFilter();
        }

        private void BtnOpenFileHistory_Click(object sender, RoutedEventArgs e)
        {
            // TODO: implement file open logic if needed
        }

        private void BtnToggleDetails_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryDetailsPanelControl.Visibility == Visibility.Collapsed)
                HistoryDetailsPanelControl.Visibility = Visibility.Visible;
            else
                HistoryDetailsPanelControl.Visibility = Visibility.Collapsed;
        }

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            TxtFilterDeviceType.Text = string.Empty;
            TxtFilterSerialNumber.Text = string.Empty;
            ApplyFilter();
        }

        private void Filter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string deviceTypeFilter = TxtFilterDeviceType?.Text?.Trim() ?? "";
            string serialNumberFilter = TxtFilterSerialNumber?.Text?.Trim() ?? "";
            filteredHistory = calibrationHistory
                .Where(r => (string.IsNullOrEmpty(deviceTypeFilter) || r.DeviceType.Contains(deviceTypeFilter))
                         && (string.IsNullOrEmpty(serialNumberFilter) || r.SerialNumber.Contains(serialNumberFilter)))
                .ToList();
            DataGridHistory.ItemsSource = null;
            DataGridHistory.ItemsSource = filteredHistory;
        }

        private void DataGridHistory_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataGridHistory.SelectedItem is WelderService.CalibrationRecord selectedRecord)
            {
                // TODO: implement details logic if needed
            }
        }

        private void DataGridHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridHistory.SelectedItem is WelderService.CalibrationRecord selectedRecord)
            {
                // TODO: implement details logic if needed
            }
        }

        private void DataGridHistory_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        // Publiczne właściwości dla dostępu z MainWindow
        public DataGrid DataGridHistory => dataGridHistory;
        public TextBox TxtFilterDeviceType => txtFilterDeviceType;
        public TextBox TxtFilterSerialNumber => txtFilterSerialNumber;
        public Button BtnRefreshHistory => btnRefreshHistory;
        public Button BtnClearHistory => btnClearHistory;
        public Button BtnOpenFileHistory => btnOpenFileHistory;
        public Button BtnToggleDetails => btnToggleDetails;
        public Button BtnClearFilter => btnClearFilter;
        public StackPanel HistoryDetailsPanelControl => HistoryDetailsPanel;
    }
}