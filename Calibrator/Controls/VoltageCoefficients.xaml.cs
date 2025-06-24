using System.Windows;
using System.Windows.Controls;

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
    }
}