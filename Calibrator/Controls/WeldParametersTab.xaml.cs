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
        }

        public void UpdateWeldParameters(WelderRS232.WeldParameters parameters)
        {
            // Aktualizuj komponent pomiarów zgrzewania
            weldMeasurementsComponent.UpdateWeldParameters(parameters);

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
            // Aktualizuj statystyki w komponencie pomiarów zgrzewania
            weldMeasurementsComponent.UpdateStatistics(welderService);
        }

        // Publiczne właściwości dla dostępu z MainWindow
        public VoltageCoefficients WspZgrzewaniaVoltage => wspZgrzewaniaVoltage;
        public CurrentCoefficients WspZgrzewaniaCurrent => wspZgrzewaniaCurrent;
        public WeldMeasurementsComponent WeldMeasurementsComponent => weldMeasurementsComponent;
    }
}