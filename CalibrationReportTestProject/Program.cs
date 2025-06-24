using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CalibrationReportLib;
using WelderRS232;

namespace CalibrationReportTestProject
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Test komunikacji ze zgrzewarką z raportem
            var welder = new Welder(Console.WriteLine);
            var scanResults = await Task.Run(() => welder.ScanAllPorts());
            welder.DisplayConnectionStatus();

            if (welder.GetStatus() == WelderStatus.CONNECTED || welder.GetStatus() == WelderStatus.NEW_WELDER)
            {
                Console.WriteLine("Zgrzewarka znaleziona!");

                //welder.ReadWeldCount();

                byte[] configData = new byte[256];  // Initialize with default size
                if (await Task.Run(() => welder.ReadConfigurationRegister(out configData)))
                {
                    var konfiguracja = await Task.Run(() => CalibrationReport.ReadFromBuffer(configData));
                    await Task.Run(() => CalibrationReport.Print(konfiguracja));
                }
                else
                {
                    Console.WriteLine("Nie udało się odczytać rejestru konfiguracji.");
                }
            }
            else
            {
                Console.WriteLine("Nie znaleziono zgrzewarki.");
            }

            // Dotychczasowy raport kalibracji
            /*         string path = "e:\\CALIBRAT.BIN";
                    try
                    {
                        var konf = CalibrationReportLib.CalibrationReport.ReadFromFile(path);
                        CalibrationReportLib.CalibrationReport.Print(konf);
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        Console.WriteLine($"Plik {path} nie został znaleziony.");
                    } */
        }
    }
}