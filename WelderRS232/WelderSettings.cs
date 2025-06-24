using System;
using System.IO;
using System.Text.Json;

namespace WelderRS232
{
    public class WelderSettings
    {
        private const string SETTINGS_FILE = "welder_settings.json";

        public string? LastPort { get; set; }
        public int? LastBaudRate { get; set; }
        public double? LogPanelHeight { get; set; }

        // Nowe właściwości dla rozmiaru okna
        public double? WindowWidth { get; set; }
        public double? WindowHeight { get; set; }
        public bool? WindowMaximized { get; set; }

        public static WelderSettings Load()
        {
            try
            {
                if (File.Exists(SETTINGS_FILE))
                {
                    string jsonString = File.ReadAllText(SETTINGS_FILE);
                    var settings = JsonSerializer.Deserialize<WelderSettings>(jsonString);
                    return settings ?? new WelderSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas odczytu ustawień: {ex.Message}");
            }
            return new WelderSettings();
        }

        public void Save()
        {
            try
            {
                string jsonString = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SETTINGS_FILE, jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas zapisu ustawień: {ex.Message}");
            }
        }
    }
}