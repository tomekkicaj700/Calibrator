using System.Windows;
using System.Windows.Controls;

namespace Calibrator.Controls
{
    /// <summary>
    /// Interaction logic for WelderChannels.xaml
    /// </summary>
    public partial class WelderChannels : UserControl
    {
        public WelderChannels()
        {
            InitializeComponent();
        }

        // Właściwości do ustawiania wartości kanałów
        public string UInputVoltageHighCurrent
        {
            get { return txtUInputVoltageHighCurrent.Text; }
            set { txtUInputVoltageHighCurrent.Text = value; }
        }

        public string UInputVoltageLowCurrent
        {
            get { return txtUInputVoltageLowCurrent.Text; }
            set { txtUInputVoltageLowCurrent.Text = value; }
        }

        public string UADCValueHighCurrent
        {
            get { return txtUADCValueHighCurrent.Text; }
            set { txtUADCValueHighCurrent.Text = value; }
        }

        public string UADCValueLowCurrent
        {
            get { return txtUADCValueLowCurrent.Text; }
            set { txtUADCValueLowCurrent.Text = value; }
        }

        public string UMultimeterWeldVoltageLowCurrent
        {
            get { return txtUMultimeterWeldVoltageLowCurrent.Text; }
            set { txtUMultimeterWeldVoltageLowCurrent.Text = value; }
        }

        public string UMultimeterWeldVoltageHighCurrent
        {
            get { return txtUMultimeterWeldVoltageHighCurrent.Text; }
            set { txtUMultimeterWeldVoltageHighCurrent.Text = value; }
        }

        public string UMultimeterWeldCurrentLowCurrent
        {
            get { return txtUMultimeterWeldCurrentLowCurrent.Text; }
            set { txtUMultimeterWeldCurrentLowCurrent.Text = value; }
        }

        public string UMultimeterWeldCurrentHighCurrent
        {
            get { return txtUMultimeterWeldCurrentHighCurrent.Text; }
            set { txtUMultimeterWeldCurrentHighCurrent.Text = value; }
        }
    }
}