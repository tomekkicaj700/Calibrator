using System.Windows.Controls;
using System.Text;

namespace Calibrator.Controls
{
    public partial class CalibrationParametersTab : UserControl
    {
        public CalibrationParametersTab()
        {
            InitializeComponent();
        }

        // Publiczne właściwości dla dostępu z MainWindow
        public WelderChannels WelderChannels => welderChannels;
        public TextBox TxtTyp => txtTyp;
        public TextBox TxtKeypadSE => txtKeypadSE;
        public TextBox TxtNrJezyka => txtNrJezyka;
        public TextBox TxtNazwaZgrzewarki => txtNazwaZgrzewarki;
        public TextBox TxtNumerSeryjny => txtNumerSeryjny;
        public TextBox TxtDaneWlasciciela0 => txtDaneWlasciciela0;
        public TextBox TxtDaneWlasciciela1 => txtDaneWlasciciela1;
        public TextBox TxtDaneWlasciciela2 => txtDaneWlasciciela2;
        public TextBox TxtDataSprzedazy => txtDataSprzedazy;
        public TextBox TxtDataPierwszegoZgrzewu => txtDataPierwszegoZgrzewu;
        public TextBox TxtDataOstatniejKalibracji => txtDataOstatniejKalibracji;
        public TextBox TxtOffsetMCP3425 => txtOffsetMCP3425;
        public TextBox TxtWolneMiejsce => txtWolneMiejsce;
        public TextBox TxtLiczbaZgrzOstKalibr => txtLiczbaZgrzOstKalibr;
        public TextBox TxtOkresKalibracji => txtOkresKalibracji;
        public TextBox TxtRejestrKonfiguracji => txtRejestrKonfiguracji;
        public TextBox TxtRejestrKonfiguracjiBankTwo => txtRejestrKonfiguracjiBankTwo;
        public TextBox TxtTempOtRefVal => txtTempOtRefVal;
        public TextBox TxtTempOtRefADC => txtTempOtRefADC;
        public TextBox TxtKorekcjaTempWewn => txtKorekcjaTempWewn;
        public TextBox TxtKorekcjaTempZewn => txtKorekcjaTempZewn;
        public TextBox TxtKodBlokady => txtKodBlokady;
        public TextBox TxtTypBlokady => txtTypBlokady;
        public TextBox TxtGPSconfiguration => txtGPSconfiguration;
        public TextBox KanalyZgrzewarkiVoltage => kanalyZgrzewarkiVoltage;
        public TextBox KanalyZgrzewarkiCurrent => kanalyZgrzewarkiCurrent;

        public void SetConfiguration(SKonfiguracjaSystemu config)
        {
            WelderChannels.SetConfiguration(config);
            KanalyZgrzewarkiVoltage.SetConfiguration(config);
            KanalyZgrzewarkiCurrent.SetConfiguration(config);
            TxtTyp.Text = config.Typ.ToString();
            TxtKeypadSE.Text = config.KeypadSE.ToString();
            TxtNrJezyka.Text = config.nrJezyka.ToString();
            TxtNazwaZgrzewarki.Text = WelderService.GetWelderName(config.NazwaZgrzewarki);
            TxtNumerSeryjny.Text = Encoding.ASCII.GetString(config.NumerSeryjny).TrimEnd('\0');
            TxtDaneWlasciciela0.Text = Encoding.ASCII.GetString(config.DaneWlasciciela0).TrimEnd('\0');
            TxtDaneWlasciciela1.Text = Encoding.ASCII.GetString(config.DaneWlasciciela1).TrimEnd('\0');
            TxtDaneWlasciciela2.Text = Encoding.ASCII.GetString(config.DaneWlasciciela2).TrimEnd('\0');
            TxtDataSprzedazy.Text = FormatDate(config.DataSprzedazy);
            TxtDataPierwszegoZgrzewu.Text = FormatDate(config.DataPierwszegoZgrzewu);
            TxtDataOstatniejKalibracji.Text = FormatDate(config.DataOstatniejKalibracji);
            TxtOffsetMCP3425.Text = config.Offset_MCP3425.ToString();
            TxtWolneMiejsce.Text = BitConverter.ToString(config.WolneMiejsce);
            TxtLiczbaZgrzOstKalibr.Text = config.LiczbaZgrzOstKalibr.ToString();
            TxtOkresKalibracji.Text = config.OkresKalibracji.ToString();
            TxtRejestrKonfiguracji.Text = config.RejestrKonfiguracji.ToString();
            TxtRejestrKonfiguracjiBankTwo.Text = config.RejestrKonfiguracjiBankTwo.ToString();
            TxtTempOtRefVal.Text = config.TempOtRefVal.ToString();
            TxtTempOtRefADC.Text = config.TempOtRefADC.ToString();
            TxtKorekcjaTempWewn.Text = config.KorekcjaTempWewn.ToString();
            TxtKorekcjaTempZewn.Text = config.KorekcjaTempZewn.ToString();
            TxtKodBlokady.Text = config.KodBlokady.ToString();
            TxtTypBlokady.Text = config.TypBlokady.ToString();
            TxtGPSconfiguration.Text = config.GPSconfiguration.ToString();
        }

        private string FormatDate(byte[] date)
        {
            if (date == null || date.Length < 3) return "";
            return $"{date[0]:D2}.{date[1]:D2}.{2000 + date[2]:D4}";
        }
    }
}