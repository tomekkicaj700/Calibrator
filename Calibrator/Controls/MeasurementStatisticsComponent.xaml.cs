using System.Windows.Controls;

namespace Calibrator.Controls
{
    public partial class MeasurementStatisticsComponent : UserControl
    {
        public MeasurementStatisticsComponent()
        {
            InitializeComponent();
        }

        // Napięcie
        public void SetVoltageStats(string min, string max, string avg, string delta)
        {
            txtVoltageMin.Text = min;
            txtVoltageMax.Text = max;
            txtVoltageAvg.Text = avg;
            txtVoltageDelta.Text = delta;
        }
        // Prąd
        public void SetCurrentStats(string min, string max, string avg, string delta)
        {
            txtCurrentMin.Text = min;
            txtCurrentMax.Text = max;
            txtCurrentAvg.Text = avg;
            txtCurrentDelta.Text = delta;
        }
        // ADC Napięcia
        public void SetVoltageADCStats(string min, string max, string avg, string delta)
        {
            txtVoltageADCMin.Text = min;
            txtVoltageADCMax.Text = max;
            txtVoltageADCAvg.Text = avg;
            txtVoltageADCDelta.Text = delta;
        }
        // ADC Prądu
        public void SetCurrentADCStats(string min, string max, string avg, string delta)
        {
            txtCurrentADCMin.Text = min;
            txtCurrentADCMax.Text = max;
            txtCurrentADCAvg.Text = avg;
            txtCurrentADCDelta.Text = delta;
        }
        // Wyczyść
        public void ClearAll()
        {
            SetVoltageStats("Min: —", "Max: —", "Śr: —", "Δ: —");
            SetCurrentStats("Min: —", "Max: —", "Śr: —", "Δ: —");
            SetVoltageADCStats("Min: —", "Max: —", "Śr: —", "Δ: —");
            SetCurrentADCStats("Min: —", "Max: —", "Śr: —", "Δ: —");
        }
    }
}