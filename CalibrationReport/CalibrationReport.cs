using System;
using System.IO;
using System.Text;
using Logger;

namespace CalibrationReportLib
{
    public class SKonfiguracjaSystemu
    {
        public byte dummy;
        public byte Typ;
        public byte KeypadSE;
        public byte nrJezyka;
        public byte NazwaZgrzewarki;
        public short Offset_MCP3425; // 2 bajty
        public byte[] WolneMiejsce = new byte[18];
        public byte[] NumerSeryjny = new byte[16]; // 15 + 1 bajt
        public byte[] DaneWlasciciela0 = new byte[21];
        public byte[] DaneWlasciciela1 = new byte[21];
        public byte[] DaneWlasciciela2 = new byte[21];
        public byte[] DataSprzedazy = new byte[3];
        public byte[] DataPierwszegoZgrzewu = new byte[3];
        public byte[] DataOstatniejKalibracji = new byte[3];
        public ushort LiczbaZgrzOstKalibr; // 2 bajty
        public byte OkresKalibracji;
        public ushort RejestrKonfiguracji; // 2 bajty
        public byte TempOtRefVal;
        public byte TempOtRefADC;
        public sbyte KorekcjaTempWewn;
        public sbyte KorekcjaTempZewn;
        public ushort[] uInputVoltageHighCurrent = new ushort[8];
        public ushort[] uInputVoltageLowCurrent = new ushort[8];
        public ushort[] uADCValueHighCurrent = new ushort[8];
        public ushort[] uADCValueLowCurrent = new ushort[8];
        public ushort uMultimeterWeldVoltageLowCurrent;
        public ushort uMultimeterWeldVoltageHighCurrent;
        public ushort uMultimeterWeldCurrentLowCurrent;
        public ushort uMultimeterWeldCurrentHighCurrent;
        public ushort KodBlokady;
        public byte TypBlokady;
        public ushort uSupplyVoltageHigh_TriacRelay_On;
        public ushort uSupplyVoltageLow_TriacRelay_On;
        public ushort uSupplyADCValueHigh_TriacRelay_On;
        public ushort uSupplyADCValueLow_TriacRelay_On;
        public byte GPSconfiguration;
        public ushort RejestrKonfiguracjiBankTwo;
        public byte[] temp = new byte[46];
        public ushort CRC;
    }

    public class KanałyZgrzewarki
    {
        public uint NapiecieZasilaniaMax { get; set; }
        public uint NapiecieZgrzewaniaMax { get; set; }
        public uint PradZgrzewaniaMax { get; set; }
        public uint NapiecieZasilaniaMin { get; set; }
        public uint NapiecieZgrzewaniaMin { get; set; }
        public uint PradZgrzewaniaMin { get; set; }
        public uint NapiecieZasilaniaMaxADC { get; set; }
        public uint NapiecieZgrzewaniaMaxADC { get; set; }
        public uint PradZgrzewaniaMaxADC { get; set; }
        public uint NapiecieZasilaniaMinADC { get; set; }
        public uint NapiecieZgrzewaniaMinADC { get; set; }
        public uint PradZgrzewaniaMinADC { get; set; }
    }

    public class CalibrationReport
    {
        private readonly Action<string> logger;

        public CalibrationReport(Action<string>? logger = null)
        {
            this.logger = logger ?? Console.WriteLine;
        }

        // Prywatna metoda do logowania
        private void Log(string message)
        {
            if (logger != null)
                logger(message);
            else
                Console.WriteLine(message);
        }

        public static SKonfiguracjaSystemu ReadFromBuffer(byte[] data)
        {
            var konf = new SKonfiguracjaSystemu();
            int i = 0;
            konf.dummy = data[i++];
            konf.Typ = data[i++];
            konf.KeypadSE = data[i++];
            konf.nrJezyka = data[i++];
            konf.NazwaZgrzewarki = data[i++];
            konf.Offset_MCP3425 = (short)(data[i] | (data[i + 1] << 8)); i += 2;
            Array.Copy(data, i, konf.WolneMiejsce, 0, 18); i += 18;
            Array.Copy(data, i, konf.NumerSeryjny, 0, 16); i += 16;
            Array.Copy(data, i, konf.DaneWlasciciela0, 0, 21); i += 21;
            Array.Copy(data, i, konf.DaneWlasciciela1, 0, 21); i += 21;
            Array.Copy(data, i, konf.DaneWlasciciela2, 0, 21); i += 21;
            Array.Copy(data, i, konf.DataSprzedazy, 0, 3); i += 3;
            Array.Copy(data, i, konf.DataPierwszegoZgrzewu, 0, 3); i += 3;
            Array.Copy(data, i, konf.DataOstatniejKalibracji, 0, 3); i += 3;
            konf.LiczbaZgrzOstKalibr = (ushort)(data[i] | (data[i + 1] << 8)); i += 2;
            konf.OkresKalibracji = data[i++];
            konf.RejestrKonfiguracji = (ushort)(data[i] | (data[i + 1] << 8)); i += 2;
            konf.TempOtRefVal = data[i++];
            konf.TempOtRefADC = data[i++];
            konf.KorekcjaTempWewn = (sbyte)data[i++];
            konf.KorekcjaTempZewn = (sbyte)data[i++];
            for (int j = 0; j < 8; j++) { konf.uInputVoltageHighCurrent[j] = (ushort)(data[i] | (data[i + 1] << 8)); i += 2; }
            for (int j = 0; j < 8; j++) { konf.uInputVoltageLowCurrent[j] = (ushort)(data[i] | (data[i + 1] << 8)); i += 2; }
            for (int j = 0; j < 8; j++) { konf.uADCValueHighCurrent[j] = (ushort)(data[i] | (data[i + 1] << 8)); i += 2; }
            for (int j = 0; j < 8; j++) { konf.uADCValueLowCurrent[j] = (ushort)(data[i] | (data[i + 1] << 8)); i += 2; }
            konf.uMultimeterWeldVoltageLowCurrent = (ushort)(data[i] | (data[i + 1] << 8)); i += 2;
            konf.uMultimeterWeldVoltageHighCurrent = (ushort)(data[i] | (data[i + 1] << 8)); i += 2;
            konf.uMultimeterWeldCurrentLowCurrent = (ushort)(data[i] | (data[i + 1] << 8)); i += 2;
            konf.uMultimeterWeldCurrentHighCurrent = (ushort)(data[i] | (data[i + 1] << 8)); i += 2;
            konf.KodBlokady = (ushort)(data[i] | (data[i + 1] << 8)); i += 2;
            konf.TypBlokady = data[i++];
            konf.uSupplyVoltageHigh_TriacRelay_On = (ushort)(data[i] | (data[i + 1] << 8)); i += 2;
            konf.uSupplyVoltageLow_TriacRelay_On = (ushort)(data[i] | (data[i + 1] << 8)); i += 2;
            konf.uSupplyADCValueHigh_TriacRelay_On = (ushort)(data[i] | (data[i + 1] << 8)); i += 2;
            konf.uSupplyADCValueLow_TriacRelay_On = (ushort)(data[i] | (data[i + 1] << 8)); i += 2;
            konf.GPSconfiguration = data[i++];
            konf.RejestrKonfiguracjiBankTwo = (ushort)(data[i] | (data[i + 1] << 8)); i += 2;
            Array.Copy(data, i, konf.temp, 0, 46); i += 46;
            konf.CRC = (ushort)(data[i] | (data[i + 1] << 8)); i += 2;
            return konf;
        }

        public static SKonfiguracjaSystemu ReadFromFile(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            return ReadFromBuffer(data);
        }

        private static string FormatDate(byte[] date)
        {
            if (date.Length != 3) return "-";
            return $"{date[0]:D2}-{date[1]:D2}-{2000 + date[2]}";
        }

        public static KanałyZgrzewarki MapujKanały(SKonfiguracjaSystemu konf)
        {
            return new KanałyZgrzewarki
            {
                NapiecieZasilaniaMax = konf.uInputVoltageHighCurrent[4],
                NapiecieZgrzewaniaMax = konf.uInputVoltageHighCurrent[5],
                PradZgrzewaniaMax = konf.uInputVoltageHighCurrent[6],
                NapiecieZasilaniaMin = konf.uInputVoltageLowCurrent[4],
                NapiecieZgrzewaniaMin = konf.uInputVoltageLowCurrent[5],
                PradZgrzewaniaMin = konf.uInputVoltageLowCurrent[6],
                NapiecieZasilaniaMaxADC = konf.uADCValueHighCurrent[4],
                NapiecieZgrzewaniaMaxADC = konf.uADCValueHighCurrent[5],
                PradZgrzewaniaMaxADC = konf.uADCValueHighCurrent[6],
                NapiecieZasilaniaMinADC = konf.uADCValueLowCurrent[4],
                NapiecieZgrzewaniaMinADC = konf.uADCValueLowCurrent[5],
                PradZgrzewaniaMinADC = konf.uADCValueLowCurrent[6],
            };
        }

        public void Print(SKonfiguracjaSystemu konf)
        {
            Log($"dummy: {konf.dummy}");
            Log($"Typ: {konf.Typ}");
            Log($"KeypadSE: {konf.KeypadSE}");
            Log($"nrJezyka: {konf.nrJezyka}");
            Log($"NazwaZgrzewarki: {konf.NazwaZgrzewarki}");
            Log($"Offset_MCP3425: {konf.Offset_MCP3425}");
            Log($"WolneMiejsce: {BitConverter.ToString(konf.WolneMiejsce)}");
            Log($"NumerSeryjny: {Encoding.ASCII.GetString(konf.NumerSeryjny).TrimEnd('\0')}");
            Log($"DaneWlasciciela0: {Encoding.ASCII.GetString(konf.DaneWlasciciela0).TrimEnd('\0')}");
            Log($"DaneWlasciciela1: {Encoding.ASCII.GetString(konf.DaneWlasciciela1).TrimEnd('\0')}");
            Log($"DaneWlasciciela2: {Encoding.ASCII.GetString(konf.DaneWlasciciela2).TrimEnd('\0')}");
            Log($"DataSprzedazy: {FormatDate(konf.DataSprzedazy)}");
            Log($"DataPierwszegoZgrzewu: {FormatDate(konf.DataPierwszegoZgrzewu)}");
            Log($"DataOstatniejKalibracji: {FormatDate(konf.DataOstatniejKalibracji)}");
            Log($"LiczbaZgrzOstKalibr: {konf.LiczbaZgrzOstKalibr}");
            Log($"OkresKalibracji: {konf.OkresKalibracji}");
            Log($"RejestrKonfiguracji: {konf.RejestrKonfiguracji}");
            Log($"TempOtRefVal: {konf.TempOtRefVal}");
            Log($"TempOtRefADC: {konf.TempOtRefADC}");
            Log($"KorekcjaTempWewn: {konf.KorekcjaTempWewn}");
            Log($"KorekcjaTempZewn: {konf.KorekcjaTempZewn}");
            Log($"uInputVoltageHighCurrent: {string.Join(", ", konf.uInputVoltageHighCurrent)}");
            Log($"uInputVoltageLowCurrent: {string.Join(", ", konf.uInputVoltageLowCurrent)}");
            Log($"uADCValueHighCurrent: {string.Join(", ", konf.uADCValueHighCurrent)}");
            Log($"uADCValueLowCurrent: {string.Join(", ", konf.uADCValueLowCurrent)}");
            Log($"uMultimeterWeldVoltageLowCurrent: {konf.uMultimeterWeldVoltageLowCurrent}");
            Log($"uMultimeterWeldVoltageHighCurrent: {konf.uMultimeterWeldVoltageHighCurrent}");
            Log($"uMultimeterWeldCurrentLowCurrent: {konf.uMultimeterWeldCurrentLowCurrent}");
            Log($"uMultimeterWeldCurrentHighCurrent: {konf.uMultimeterWeldCurrentHighCurrent}");
            Log($"KodBlokady: {konf.KodBlokady}");
            Log($"TypBlokady: {konf.TypBlokady}");
            Log($"uSupplyVoltageHigh_TriacRelay_On: {konf.uSupplyVoltageHigh_TriacRelay_On}");
            Log($"uSupplyVoltageLow_TriacRelay_On: {konf.uSupplyVoltageLow_TriacRelay_On}");
            Log($"uSupplyADCValueHigh_TriacRelay_On: {konf.uSupplyADCValueHigh_TriacRelay_On}");
            Log($"uSupplyADCValueLow_TriacRelay_On: {konf.uSupplyADCValueLow_TriacRelay_On}");
            Log($"GPSconfiguration: {konf.GPSconfiguration}");
            Log($"RejestrKonfiguracjiBankTwo: {konf.RejestrKonfiguracjiBankTwo}");
            Log($"temp: {BitConverter.ToString(konf.temp)}");
            Log($"CRC: {konf.CRC}");
            var kanaly = MapujKanały(konf);
            Log("--- Wartości kanałów zgrzewarki ---");
            Log("Napięcie zasilania:");
            Log($"  Max: {kanaly.NapiecieZasilaniaMax}");
            Log($"  Min: {kanaly.NapiecieZasilaniaMin}");
            Log($"  Max ADC: {kanaly.NapiecieZasilaniaMaxADC}");
            Log($"  Min ADC: {kanaly.NapiecieZasilaniaMinADC}");
            Log("Napięcie zgrzewania:");
            Log($"  Max: {kanaly.NapiecieZgrzewaniaMax}");
            Log($"  Min: {kanaly.NapiecieZgrzewaniaMin}");
            Log($"  Max ADC: {kanaly.NapiecieZgrzewaniaMaxADC}");
            Log($"  Min ADC: {kanaly.NapiecieZgrzewaniaMinADC}");
            Log("Prąd zgrzewania:");
            Log($"  Max: {kanaly.PradZgrzewaniaMax}");
            Log($"  Min: {kanaly.PradZgrzewaniaMin}");
            Log($"  Max ADC: {kanaly.PradZgrzewaniaMaxADC}");
            Log($"  Min ADC: {kanaly.PradZgrzewaniaMinADC}");
        }
    }
}
