using System.Windows;
using System.Windows.Controls;
using CalibrationReportLib;

namespace Calibrator.Controls
{
    /// <summary>
    /// Interaction logic for CurrentCoefficients.xaml
    /// </summary>
    public partial class CurrentCoefficients : UserControl
    {
        public CurrentCoefficients()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty MMWCLValueProperty =
            DependencyProperty.Register("MMWCLValue", typeof(string), typeof(CurrentCoefficients), new PropertyMetadata("—"));
        public string MMWCLValue
        {
            get { return (string)GetValue(MMWCLValueProperty); }
            set { SetValue(MMWCLValueProperty, value); }
        }

        public static readonly DependencyProperty MMWCHValueProperty =
            DependencyProperty.Register("MMWCHValue", typeof(string), typeof(CurrentCoefficients), new PropertyMetadata("—"));
        public string MMWCHValue
        {
            get { return (string)GetValue(MMWCHValueProperty); }
            set { SetValue(MMWCHValueProperty, value); }
        }

        public static readonly DependencyProperty IVHC_I_ValueProperty =
            DependencyProperty.Register("IVHC_I_Value", typeof(string), typeof(CurrentCoefficients), new PropertyMetadata("—"));
        public string IVHC_I_Value
        {
            get { return (string)GetValue(IVHC_I_ValueProperty); }
            set { SetValue(IVHC_I_ValueProperty, value); }
        }

        public static readonly DependencyProperty IVLC_I_ValueProperty =
            DependencyProperty.Register("IVLC_I_Value", typeof(string), typeof(CurrentCoefficients), new PropertyMetadata("—"));
        public string IVLC_I_Value
        {
            get { return (string)GetValue(IVLC_I_ValueProperty); }
            set { SetValue(IVLC_I_ValueProperty, value); }
        }

        public static readonly DependencyProperty ADCIVHC_I_ValueProperty =
            DependencyProperty.Register("ADCIVHC_I_Value", typeof(string), typeof(CurrentCoefficients), new PropertyMetadata("—"));
        public string ADCIVHC_I_Value
        {
            get { return (string)GetValue(ADCIVHC_I_ValueProperty); }
            set { SetValue(ADCIVHC_I_ValueProperty, value); }
        }

        public static readonly DependencyProperty ADCIVLC_I_ValueProperty =
            DependencyProperty.Register("ADCIVLC_I_Value", typeof(string), typeof(CurrentCoefficients), new PropertyMetadata("—"));
        public string ADCIVLC_I_Value
        {
            get { return (string)GetValue(ADCIVLC_I_ValueProperty); }
            set { SetValue(ADCIVLC_I_ValueProperty, value); }
        }

        public void SetConfiguration(SKonfiguracjaSystemu config)
        {
            // Mapowanie wartości kanałów prądowych
            var wartosci = MapujWartosciKanalowZgrzewarki(config);
            MMWCLValue = wartosci.MMWCL.ToString();
            MMWCHValue = wartosci.MMWCH.ToString();
            IVHC_I_Value = wartosci.IVHC_I.ToString();
            IVLC_I_Value = wartosci.IVLC_I.ToString();
            ADCIVHC_I_Value = wartosci.ADCIVHC_I.ToString();
            ADCIVLC_I_Value = wartosci.ADCIVLC_I.ToString();
        }

        private dynamic MapujWartosciKanalowZgrzewarki(SKonfiguracjaSystemu konf)
        {
            return new
            {
                MMWCL = konf.uMultimeterWeldCurrentLowCurrent,
                MMWCH = konf.uMultimeterWeldCurrentHighCurrent,
                IVHC_I = konf.uInputVoltageHighCurrent.Length > 6 ? konf.uInputVoltageHighCurrent[6] : 0,
                IVLC_I = konf.uInputVoltageLowCurrent.Length > 6 ? konf.uInputVoltageLowCurrent[6] : 0,
                ADCIVHC_I = konf.uADCValueHighCurrent.Length > 6 ? konf.uADCValueHighCurrent[6] : 0,
                ADCIVLC_I = konf.uADCValueLowCurrent.Length > 6 ? konf.uADCValueLowCurrent[6] : 0
            };
        }
    }
}