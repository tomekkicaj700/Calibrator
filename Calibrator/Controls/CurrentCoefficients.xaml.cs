using System.Windows;
using System.Windows.Controls;

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
    }
}