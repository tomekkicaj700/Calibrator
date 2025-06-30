using System.Windows.Controls;
using System.Windows;
using LiveCharts.Wpf;
using WelderRS232;
using CalibrationReportLib;
using Calibrator.Services;

namespace Calibrator.Controls
{
    public partial class WeldParametersTab : UserControl
    {
        public WeldParametersTab()
        {
            InitializeComponent();
            GaugeNapiecie.LabelFormatter = value => value.ToString("F1");
            GaugePrad.LabelFormatter = value => value.ToString("F1");
        }

        public void UpdateWeldParameters(WeldParameters parameters)
        {
            GaugeNapiecie.Value = parameters.NapiecieZgrzewania;
            GaugePrad.Value = parameters.PradZgrzewania;
            TxtNapiecieZgrzewania.Text = parameters.NapiecieZgrzewania.ToString("F2");
            TxtPradZgrzewania.Text = parameters.PradZgrzewania.ToString("F2");
            TxtADCNapZgrzew.Text = $"0x{parameters.ADCNapZgrzew:X4}";
            TxtADCPradZgrzew.Text = $"0x{parameters.ADCPradZgrzew:X4}";
            // TODO: Uzupełnij aktualizację pozostałych pól jeśli potrzeba
        }

        public void UpdateStatistics(WelderService welderService)
        {
            TxtNapiecieMin.Text = welderService.NapiecieMin.ToString("F2");
            TxtNapiecieMax.Text = welderService.NapiecieMax.ToString("F2");
            TxtNapiecieAvr.Text = welderService.NapiecieAverage.ToString("F2");
            TxtPradMin.Text = welderService.PradMin.ToString("F2");
            TxtPradMax.Text = welderService.PradMax.ToString("F2");
            TxtPradAvr.Text = welderService.PradAverage.ToString("F2");
        }

        // Publiczne właściwości dla dostępu z MainWindow
        public Gauge GaugeNapiecie => gaugeNapiecie;
        public Gauge GaugePrad => gaugePrad;
        public TextBlock TxtNapiecieMin => txtNapiecieMin;
        public TextBlock TxtNapiecieMax => txtNapiecieMax;
        public TextBlock TxtNapiecieAvr => txtNapiecieAvr;
        public TextBlock TxtPradMin => txtPradMin;
        public TextBlock TxtPradMax => txtPradMax;
        public TextBlock TxtPradAvr => txtPradAvr;
        public TextBlock TxtNapiecieZgrzewania => txtNapiecieZgrzewania;
        public TextBlock TxtPradZgrzewania => txtPradZgrzewania;
        public TextBlock TxtADCNapZgrzew => txtADCNapZgrzew;
        public TextBlock TxtADCPradZgrzew => txtADCPradZgrzew;
        public VoltageCoefficients WspZgrzewaniaVoltage => wspZgrzewaniaVoltage;
        public CurrentCoefficients WspZgrzewaniaCurrent => wspZgrzewaniaCurrent;
    }
}