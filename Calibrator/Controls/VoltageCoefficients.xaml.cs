using System.Windows;
using System.Windows.Controls;
using CalibrationReportLib;

namespace Calibrator.Controls
{
    /// <summary>
    /// Interaction logic for VoltageCoefficients.xaml
    /// </summary>
    public partial class VoltageCoefficients : UserControl
    {
        public VoltageCoefficients()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty MMWVHValueProperty =
            DependencyProperty.Register("MMWVHValue", typeof(string), typeof(VoltageCoefficients), new PropertyMetadata("—"));
        public string MMWVHValue
        {
            get { return (string)GetValue(MMWVHValueProperty); }
            set { SetValue(MMWVHValueProperty, value); }
        }

        public static readonly DependencyProperty MMWVLValueProperty =
            DependencyProperty.Register("MMWVLValue", typeof(string), typeof(VoltageCoefficients), new PropertyMetadata("—"));
        public string MMWVLValue
        {
            get { return (string)GetValue(MMWVLValueProperty); }
            set { SetValue(MMWVLValueProperty, value); }
        }

        public static readonly DependencyProperty IVHC_U_ValueProperty =
            DependencyProperty.Register("IVHC_U_Value", typeof(string), typeof(VoltageCoefficients), new PropertyMetadata("—"));
        public string IVHC_U_Value
        {
            get { return (string)GetValue(IVHC_U_ValueProperty); }
            set { SetValue(IVHC_U_ValueProperty, value); }
        }

        public static readonly DependencyProperty IVLC_U_ValueProperty =
            DependencyProperty.Register("IVLC_U_Value", typeof(string), typeof(VoltageCoefficients), new PropertyMetadata("—"));
        public string IVLC_U_Value
        {
            get { return (string)GetValue(IVLC_U_ValueProperty); }
            set { SetValue(IVLC_U_ValueProperty, value); }
        }

        public static readonly DependencyProperty ADCIVHC_U_ValueProperty =
            DependencyProperty.Register("ADCIVHC_U_Value", typeof(string), typeof(VoltageCoefficients), new PropertyMetadata("—"));
        public string ADCIVHC_U_Value
        {
            get { return (string)GetValue(ADCIVHC_U_ValueProperty); }
            set { SetValue(ADCIVHC_U_ValueProperty, value); }
        }

        public static readonly DependencyProperty ADCIVLC_U_ValueProperty =
            DependencyProperty.Register("ADCIVLC_U_Value", typeof(string), typeof(VoltageCoefficients), new PropertyMetadata("—"));
        public string ADCIVLC_U_Value
        {
            get { return (string)GetValue(ADCIVLC_U_ValueProperty); }
            set { SetValue(ADCIVLC_U_ValueProperty, value); }
        }

        public void SetConfiguration(SKonfiguracjaSystemu config)
        {
            // Mapowanie wartości kanałów napięciowych
            var wartosci = MapujWartosciKanalowZgrzewarki(config);
            MMWVHValue = wartosci.MMWVH.ToString();
            MMWVLValue = wartosci.MMWVL.ToString();
            IVHC_U_Value = wartosci.IVHC_U.ToString();
            IVLC_U_Value = wartosci.IVLC_U.ToString();
            ADCIVHC_U_Value = wartosci.ADCIVHC_U.ToString();
            ADCIVLC_U_Value = wartosci.ADCIVLC_U.ToString();
        }

        private dynamic MapujWartosciKanalowZgrzewarki(SKonfiguracjaSystemu konf)
        {
            return new
            {
                MMWVH = konf.uMultimeterWeldVoltageHighCurrent,
                MMWVL = konf.uMultimeterWeldVoltageLowCurrent,
                IVHC_U = konf.uInputVoltageHighCurrent.Length > 5 ? konf.uInputVoltageHighCurrent[5] : 0,
                IVLC_U = konf.uInputVoltageLowCurrent.Length > 5 ? konf.uInputVoltageLowCurrent[5] : 0,
                ADCIVHC_U = konf.uADCValueHighCurrent.Length > 5 ? konf.uADCValueHighCurrent[5] : 0,
                ADCIVLC_U = konf.uADCValueLowCurrent.Length > 5 ? konf.uADCValueLowCurrent[5] : 0
            };
        }
    }
}