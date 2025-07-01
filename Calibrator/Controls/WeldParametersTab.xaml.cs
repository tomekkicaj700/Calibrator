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

        public void UpdateWeldParameters(WelderRS232.WeldParameters parameters)
        {
            GaugeNapiecie.Value = parameters.NapiecieZgrzewania;
            GaugePrad.Value = parameters.PradZgrzewania;
            TxtNapiecieZgrzewania.Text = parameters.NapiecieZgrzewania.ToString("F2");
            TxtPradZgrzewania.Text = parameters.PradZgrzewania.ToString("F2");
            TxtADCNapZgrzew.Text = $"0x{parameters.ADCNapZgrzew:X4}";
            TxtADCPradZgrzew.Text = $"0x{parameters.ADCPradZgrzew:X4}";

            // Współczynniki kalibracji napięcia
            WspZgrzewaniaVoltage.MMWVHValue = parameters.MMWVH.ToString();
            WspZgrzewaniaVoltage.MMWVLValue = parameters.MMWVL.ToString();
            WspZgrzewaniaVoltage.IVHC_U_Value = parameters.IVHC_U.ToString();
            WspZgrzewaniaVoltage.IVLC_U_Value = parameters.IVLC_U.ToString();
            WspZgrzewaniaVoltage.ADCIVHC_U_Value = parameters.ADCIVHC_U.ToString();
            WspZgrzewaniaVoltage.ADCIVLC_U_Value = parameters.ADCIVLC_U.ToString();

            // Współczynniki kalibracji prądu
            WspZgrzewaniaCurrent.MMWCLValue = parameters.MMWCL.ToString();
            WspZgrzewaniaCurrent.MMWCHValue = parameters.MMWCH.ToString();
            WspZgrzewaniaCurrent.IVHC_I_Value = parameters.IMHC_I.ToString();
            WspZgrzewaniaCurrent.IVLC_I_Value = parameters.IMLC_I.ToString();
            WspZgrzewaniaCurrent.ADCIVHC_I_Value = parameters.ADCIVHC_I.ToString();
            WspZgrzewaniaCurrent.ADCIVLC_I_Value = parameters.ADCIVLC_I.ToString();
        }

        public void UpdateStatistics(WelderService welderService)
        {
            TxtNapiecieMin.Text = welderService.NapiecieSamples > 0 ? welderService.NapiecieMin.ToString("F2") : "—";
            TxtNapiecieMax.Text = welderService.NapiecieSamples > 0 ? welderService.NapiecieMax.ToString("F2") : "—";
            TxtNapiecieAvr.Text = welderService.NapiecieSamples > 0 ? welderService.NapiecieAverage.ToString("F2") : "—";
            TxtPradMin.Text = welderService.PradSamples > 0 ? welderService.PradMin.ToString("F2") : "—";
            TxtPradMax.Text = welderService.PradSamples > 0 ? welderService.PradMax.ToString("F2") : "—";
            TxtPradAvr.Text = welderService.PradSamples > 0 ? welderService.PradAverage.ToString("F2") : "—";
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