using System.Windows;
using System.Windows.Controls;
using LiveCharts.Wpf;
using WelderRS232;
using Calibrator.Services;

namespace Calibrator.Controls
{
    public partial class WeldMeasurementsComponent : UserControl
    {
        private bool _showStatistics = false;

        public WeldMeasurementsComponent()
        {
            InitializeComponent();
            GaugeNapiecie.LabelFormatter = value => value.ToString("F1");
            GaugePrad.LabelFormatter = value => value.ToString("F1");
        }

        public bool ShowStatistics
        {
            get => _showStatistics;
            set
            {
                _showStatistics = value;
                borderNapiecieStats.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                borderPradStats.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void UpdateWeldParameters(WelderRS232.WeldParameters parameters)
        {
            GaugeNapiecie.Value = parameters.NapiecieZgrzewania;
            GaugePrad.Value = parameters.PradZgrzewania;
            TxtNapiecieZgrzewania.Text = parameters.NapiecieZgrzewania.ToString("F2");
            TxtPradZgrzewania.Text = parameters.PradZgrzewania.ToString("F2");
            TxtADCNapZgrzew.Text = $"0x{parameters.ADCNapZgrzew:X4}";
            TxtADCPradZgrzew.Text = $"0x{parameters.ADCPradZgrzew:X4}";
        }

        public void UpdateStatistics(WelderService welderService)
        {
            if (!ShowStatistics) return;

            TxtNapiecieMin.Text = welderService.NapiecieSamples > 0 ? welderService.NapiecieMin.ToString("F2") : "—";
            TxtNapiecieMax.Text = welderService.NapiecieSamples > 0 ? welderService.NapiecieMax.ToString("F2") : "—";
            TxtNapiecieAvr.Text = welderService.NapiecieSamples > 0 ? welderService.NapiecieAverage.ToString("F2") : "—";
            TxtPradMin.Text = welderService.PradSamples > 0 ? welderService.PradMin.ToString("F2") : "—";
            TxtPradMax.Text = welderService.PradSamples > 0 ? welderService.PradMax.ToString("F2") : "—";
            TxtPradAvr.Text = welderService.PradSamples > 0 ? welderService.PradAverage.ToString("F2") : "—";
        }

        // Publiczne właściwości dla dostępu z zewnątrz
        public Gauge GaugeNapiecie => gaugeNapiecie;
        public Gauge GaugePrad => gaugePrad;
        public TextBlock TxtNapiecieZgrzewania => txtNapiecieZgrzewania;
        public TextBlock TxtPradZgrzewania => txtPradZgrzewania;
        public TextBlock TxtADCNapZgrzew => txtADCNapZgrzew;
        public TextBlock TxtADCPradZgrzew => txtADCPradZgrzew;
        public TextBlock TxtNapiecieMin => txtNapiecieMin;
        public TextBlock TxtNapiecieMax => txtNapiecieMax;
        public TextBlock TxtNapiecieAvr => txtNapiecieAvr;
        public TextBlock TxtPradMin => txtPradMin;
        public TextBlock TxtPradMax => txtPradMax;
        public TextBlock TxtPradAvr => txtPradAvr;
    }
}