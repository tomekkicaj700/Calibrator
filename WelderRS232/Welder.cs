using System;
using System.IO.Ports;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Threading;
using System.IO;
using Logger;
using static Logger.LoggerService;
using System.Threading.Tasks;

namespace WelderRS232
{
  public enum WelderStatus
  {
    [Description("Brak połączenia ze zgrzewarką")]
    NO_CONNECTION,

    [Description("Zgrzewarka podłączona")]
    CONNECTED,

    [Description("Nie znaleziono zgrzewarki na żadnym porcie")]
    DEVICE_NOT_FOUND,

    [Description("Nowa zgrzewarka podłączona")]
    NEW_WELDER
  }

  public enum CommunicationType
  {
    [Description("Komunikacja przez port COM")]
    COM_PORT,

    [Description("Komunikacja przez USR-N520")]
    USR_N520
  }

  public class PortScanResult
  {
    public string PortName { get; set; } = string.Empty;
    public int BaudRate { get; set; }
    public bool Success { get; set; }
    public string Response { get; set; } = string.Empty;
  }

  public class WelderInfo
  {
    public bool IsNewUnit { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
  }

  // Struktura przechowująca parametry zgrzewania pobrane ze zgrzewarki.
  public class WeldParameters
  {
    /// <summary>U: Napięcie zgrzewania (V, np. 23.4)</summary>
    public double NapiecieZgrzewania { get; set; }
    /// <summary>I: Prąd zgrzewania (A, np. 12.7)</summary>
    public double PradZgrzewania { get; set; }
    /// <summary>ADCU: Wartość ADC napięcia zgrzewania (hex)</summary>
    public int ADCNapZgrzew { get; set; }
    /// <summary>ADCI: Wartość ADC prądu zgrzewania (hex)</summary>
    public int ADCPradZgrzew { get; set; }
    /// <summary>IVHC-U: Wartość uInputVoltageHighCurrent dla napięcia zgrzewania</summary>
    public int IVHC_U { get; set; }
    /// <summary>IVLC-U: Wartość uInputVoltageLowCurrent dla napięcia zgrzewania</summary>
    public int IVLC_U { get; set; }
    /// <summary>ADCIVHC-U: Wartość uADCValueHighCurrent dla napięcia zgrzewania</summary>
    public int ADCIVHC_U { get; set; }
    /// <summary>ADCIVLC-U: Wartość uADCValueLowCurrent dla napięcia zgrzewania</summary>
    public int ADCIVLC_U { get; set; }
    /// <summary>IVHC-I: Wartość uInputVoltageHighCurrent dla prądu zgrzewania</summary>
    public int IMHC_I { get; set; }
    /// <summary>IVLC-I: Wartość uInputVoltageLowCurrent dla prądu zgrzewania</summary>
    public int IMLC_I { get; set; }
    /// <summary>ADCIVHC-I: Wartość uADCValueHighCurrent dla prądu zgrzewania</summary>
    public int ADCIVHC_I { get; set; }
    /// <summary>ADCIVLC-I: Wartość uADCValueLowCurrent dla prądu zgrzewania</summary>
    public int ADCIVLC_I { get; set; }
    /// <summary>MMWVL: uMultimeterWeldVoltageLowCurrent</summary>
    public int MMWVL { get; set; }
    /// <summary>MMWVH: uMultimeterWeldVoltageHighCurrent</summary>
    public int MMWVH { get; set; }
    /// <summary>MMWCL: uMultimeterWeldCurrentLowCurrent</summary>
    public int MMWCL { get; set; }
    /// <summary>MMWCH: uMultimeterWeldCurrentHighCurrent</summary>
    public int MMWCH { get; set; }
  }

  public class Welder
  {
    private WelderStatus status = WelderStatus.NO_CONNECTION;
    private string? connectedPort = null;
    private int? connectedBaudRate = null;
    private const string RC4_KEY = "adA$2#34&1ASdq123";
    private const int COMMAND_SIZE = 30;  // Match C++ COMMAND_SIZE
    private WelderInfo? welderInfo = null;
    private readonly WelderSettings settings;

    // Dodatkowe pole do przechowywania połączenia USR-N520
    private USRDeviceManager? usrConnection = null;
    private string? usrConnectedPort = null;
    private string usrPortMode = "0"; // 0=RS-232, 1=RS-485
    private bool wasUSRConnection = false; // zapamiętuje, czy komunikacja była przez USR

    // Globalny typ komunikacji ustalony podczas skanowania
    private CommunicationType globalCommunicationType = CommunicationType.COM_PORT;

    private const int USR_RETRY_COUNT = 3;
    private const int USR_RETRY_DELAY_MS = 1000;

    public bool BezSzyfrowania { get; set; } = false;

    // Konstruktor domyślny
    public Welder()
    {
      settings = WelderSettings.Load();
    }

    // Konstruktor z callbackiem (dla kompatybilności wstecznej)
    public Welder(Action<string>? logCallback = null)
    {
      settings = WelderSettings.Load();
      // Ignorujemy logCallback - używamy LoggerService
    }

    public WelderStatus GetStatus() => status;
    public string? GetConnectedPort() => connectedPort;
    public int? GetConnectedBaudRate() => connectedBaudRate;
    public WelderInfo? GetWelderInfo() => welderInfo;

    public List<PortScanResult> ScanAllPorts()
    {
      var results = new List<PortScanResult>();
      status = WelderStatus.NO_CONNECTION;
      connectedPort = null;
      connectedBaudRate = null;

      // Sprawdź czy preferujemy TCP/IP
      if (settings.PreferTcpIp)
      {
        Log("=== Próba połączenia z USR-N520 przez TCP/IP (192.168.0.7:23) - preferowane ===");

        // Próbuj połączenia tylko z 192.168.0.7:23
        string usrIp = "192.168.0.7";
        int usrPort = 23;
        try
        {
          var usrManager = new USRDeviceManager(usrIp, usrPort);
          if (usrManager.ConnectAsync().Result)
          {
            Log($"Połączono z USR-N520 na {usrIp}:{usrPort}");
            byte[] cmd = WelderCommands.BuildIdentifyCommand(BezSzyfrowania);
            usrManager.SendDataAsync(cmd).Wait();
            var response = usrManager.ReceiveDataAsync(2000).Result;
            bool found = response.Contains("ZGRZ") || response.Contains("AGRE");
            results.Add(new PortScanResult
            {
              PortName = $"USR-N520 {usrIp}:{usrPort}",
              BaudRate = 0,
              Success = found,
              Response = response
            });
            if (found)
            {
              status = WelderStatus.CONNECTED;
              connectedPort = $"USR-N520 {usrIp}:{usrPort}";
              globalCommunicationType = CommunicationType.USR_N520;

              // Zachowaj aktywne połączenie USR
              usrConnection = usrManager;
              usrConnectedPort = $"USR-N520 {usrIp}:{usrPort}";
              wasUSRConnection = true;

              // Ustaw domyślny baudrate dla USR-N520 (jeśli nie jest ustawiony)
              if (connectedBaudRate == null) connectedBaudRate = 115200;

              Log("Zgrzewarka znaleziona przez USR-N520!");
              return results;
            }
            else
            {
              Log("Brak odpowiedzi od zgrzewarki przez USR-N520. Przechodzę do skanowania portów COM...");
            }
            usrManager.Disconnect();
          }
          else
          {
            Log($"Nie udało się połączyć z USR-N520 na {usrIp}:{usrPort}. Przechodzę do skanowania portów COM...");
          }
        }
        catch (Exception ex)
        {
          Log($"Błąd podczas próby połączenia z USR-N520: {ex.Message}. Przechodzę do skanowania portów COM...");
        }

        // Jeśli TCP/IP się nie udało, wyzeruj flagę preferencji
        settings.PreferTcpIp = false;
        settings.Save();
        Log("Wyzerowano flagę preferencji TCP/IP - przechodzę do skanowania portów COM...");
      }

      Log("=== Próba połączenia z USR-N520 przez TCP/IP (192.168.0.7:23) ===");

      // Zawsze próbuj połączenia tylko z 192.168.0.7:23
      string usrIp2 = "192.168.0.7";
      int usrPort2 = 23;
      try
      {
        var usrManager = new USRDeviceManager(usrIp2, usrPort2);
        if (usrManager.ConnectAsync().Result)
        {
          Log($"Połączono z USR-N520 na {usrIp2}:{usrPort2}");
          byte[] cmd = WelderCommands.BuildIdentifyCommand(BezSzyfrowania);
          usrManager.SendDataAsync(cmd).Wait();
          var response = usrManager.ReceiveDataAsync(2000).Result;
          bool found = response.Contains("ZGRZ") || response.Contains("AGRE");
          results.Add(new PortScanResult
          {
            PortName = $"USR-N520 {usrIp2}:{usrPort2}",
            BaudRate = 0,
            Success = found,
            Response = response
          });
          if (found)
          {
            status = WelderStatus.CONNECTED;
            connectedPort = $"USR-N520 {usrIp2}:{usrPort2}";
            globalCommunicationType = CommunicationType.USR_N520;

            // Zachowaj aktywne połączenie USR
            usrConnection = usrManager;
            usrConnectedPort = $"USR-N520 {usrIp2}:{usrPort2}";
            wasUSRConnection = true;

            // Ustaw domyślny baudrate dla USR-N520 (jeśli nie jest ustawiony)
            if (connectedBaudRate == null) connectedBaudRate = 115200;

            // Ustaw flagę preferencji TCP/IP po udanym połączeniu
            settings.PreferTcpIp = true;
            settings.Save();

            Log("Zgrzewarka znaleziona przez USR-N520!");
            return results;
          }
          else
          {
            Log("Brak odpowiedzi od zgrzewarki przez USR-N520. Przechodzę do skanowania portów COM...");
          }
          usrManager.Disconnect();
        }
        else
        {
          Log($"Nie udało się połączyć z USR-N520 na {usrIp2}:{usrPort2}. Przechodzę do skanowania portów COM...");
        }
      }
      catch (Exception ex)
      {
        Log($"Błąd podczas próby połączenia z USR-N520: {ex.Message}. Przechodzę do skanowania portów COM...");
      }

      // --- Oryginalne skanowanie portów COM (wirtualnych i fizycznych) ---
      Log("=== Rozpoczynam skanowanie portów COM ===");
      if (settings.LastPort != null && settings.LastBaudRate != null)
      {
        Log($"Próba połączenia na ostatnio używanym porcie COM {settings.LastPort} z prędkością {settings.LastBaudRate} baud...");
        var result = TryConnectToPort(settings.LastPort, settings.LastBaudRate.Value);
        results.Add(result);
        if (result.Success)
        {
          Log("Znaleziono zgrzewarkę na ostatnio używanym porcie COM!");
          return results;
        }
      }

      int[] baudRates = { 19200, 115200 };
      var availablePorts = SerialPort.GetPortNames();
      Log($"Dostępne porty COM: {string.Join(", ", availablePorts)}");

      foreach (var portName in availablePorts)
      {
        if (portName == settings.LastPort)
          continue;

        for (int i = 0; i < baudRates.Length; i++)
        {
          int baud = baudRates[i];
          for (int attempt = 1; attempt <= 2; attempt++)
          {
            Log($"Próba {attempt}/2: Port {portName}, Baud {baud}");
            var result = TryConnectToPort(portName, baud);
            results.Add(result);
            if (result.Success)
            {
              Log($"Znaleziono zgrzewarkę na porcie COM {portName} ({baud} baud)!");
              settings.LastPort = portName;
              settings.LastBaudRate = baud;
              settings.Save();
              return results;
            }
          }
        }
      }

      Log("Nie znaleziono zgrzewarki ani przez USR-N520, ani na żadnym porcie COM.");
      if (status == WelderStatus.NO_CONNECTION)
      {
        status = WelderStatus.DEVICE_NOT_FOUND;
      }
      return results;
    }

    private PortScanResult TryConnectToPort(string portName, int baudRate)
    {
      try
      {
        using (var port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One))
        {
          port.ReadTimeout = 2000;
          port.WriteTimeout = 500;
          port.Open();
          port.DiscardInBuffer();
          port.DiscardOutBuffer();
          byte[] cmd = WelderCommands.BuildIdentifyCommand(BezSzyfrowania);
          Log($"Port: {portName}, Baud: {baudRate}");
          PrintFrameTable(cmd);
          port.Write(cmd, 0, cmd.Length);

          // Odbiór do CRLF lub timeout 1s bez znaku
          string response = ReadResponseToCRLF(port, 2000);
          if (string.IsNullOrEmpty(response))
          {
            Log($"Brak odpowiedzi od urządzenia na porcie {portName} ({baudRate} baud)");
            return new PortScanResult
            {
              PortName = portName,
              BaudRate = baudRate,
              Success = false,
              Response = "Brak odpowiedzi"
            };
          }

          bool found = response.Contains("ZGRZ") || response.Contains("AGRE");
          if (!found)
          {
            Log($"Odpowiedź o nieznanym formacie na porcie {portName} ({baudRate} baud): {response}");
            return new PortScanResult
            {
              PortName = portName,
              BaudRate = baudRate,
              Success = false,
              Response = $"Nieznana odpowiedź: {response}"
            };
          }

          if (found)
          {
            if (response.Contains("AGRE"))
            {
              status = WelderStatus.NEW_WELDER;
              welderInfo = new WelderInfo
              {
                IsNewUnit = true,
                Version = response.Length >= 6 && response[4] == 'V' && response[5] == 'C' ? "C" : "EE"
              };
            }
            else
            {
              status = WelderStatus.CONNECTED;
            }
            connectedPort = portName;
            connectedBaudRate = baudRate;

            // Ustaw globalny typ komunikacji na COM
            globalCommunicationType = CommunicationType.COM_PORT;

            // Zapisz ustawienia do ponownego wykorzystania
            settings.CommType = "COM";
            settings.COM_Port = portName;
            settings.COM_Baud = baudRate;
            settings.LastPort = portName;
            settings.LastBaudRate = baudRate;
            settings.Save();

            Log($"Poprawna odpowiedź od zgrzewarki na porcie {portName} ({baudRate} baud): {response}");
          }

          return new PortScanResult
          {
            PortName = portName,
            BaudRate = baudRate,
            Success = found,
            Response = response
          };
        }
      }
      catch (Exception ex)
      {
        Log($"Błąd podczas próby połączenia na porcie {portName} ({baudRate} baud): {ex.Message}");
        return new PortScanResult
        {
          PortName = portName,
          BaudRate = baudRate,
          Success = false,
          Response = $"Błąd: {ex.Message}"
        };
      }
    }

    private PortScanResult? TryConnectThroughUSR()
    {
      try
      {
        Log("=== Rozpoczęcie skanowania urządzeń USR-N520 ===");

        // Skanuj urządzenia USR-N520
        Log("Wywołuję USRDeviceManager.FindUSRDevicesAsync()...");
        var usrDevices = USRDeviceManager.FindUSRDevicesAsync().Result;

        Log($"Wynik skanowania USR-N520: znaleziono {usrDevices.Count} urządzeń");

        if (usrDevices.Count == 0)
        {
          Log("Nie znaleziono żadnych urządzeń USR-N520 w sieci.");
          return new PortScanResult
          {
            PortName = "USR-N520",
            BaudRate = 0,
            Success = false,
            Response = "Nie znaleziono urządzeń USR-N520"
          };
        }

        Log($"Znaleziono {usrDevices.Count} urządzeń USR-N520:");
        foreach (var device in usrDevices)
        {
          Log($"  - {device.DeviceType} na {device.IP}:{device.Port}");
        }

        // Użyj pierwszego znalezionego urządzenia
        var firstDevice = usrDevices.First();
        Log($"Próbuję połączyć się z pierwszym urządzeniem: {firstDevice.IP}:{firstDevice.Port}");

        var usrManager = new USRDeviceManager(firstDevice.IP, firstDevice.Port);

        Log("Wywołuję usrManager.ConnectAsync()...");
        if (usrManager.ConnectAsync().Result)
        {
          Log($"Połączono z USR-N520 na {firstDevice.IP}:{firstDevice.Port}");

          // Skanuj porty RS-232 na urządzeniu USR-N520
          Log("Skanuję porty RS-232 na urządzeniu USR-N520...");
          var rs232Results = usrManager.ScanRS232PortsAsync().Result;

          Log($"Wyniki skanowania portów RS-232 na USR-N520: {rs232Results.Count} wyników");

          foreach (var rs232Result in rs232Results)
          {
            Log($"Sprawdzam wynik: {rs232Result.PortName} ({rs232Result.BaudRate} baud) - Success: {rs232Result.Success}");
            if (rs232Result.Success)
            {
              Log($"Znaleziono zgrzewarkę na porcie RS-232 USR-N520: {rs232Result.PortName} ({rs232Result.BaudRate} baud)");
              Log($"Odpowiedź: {rs232Result.Response}");

              // Ustaw status połączenia przez USR-N520
              if (rs232Result.Response.Contains("AGRE"))
              {
                status = WelderStatus.NEW_WELDER;
                welderInfo = new WelderInfo
                {
                  IsNewUnit = true,
                  Version = rs232Result.Response.Length >= 6 && rs232Result.Response[4] == 'V' && rs232Result.Response[5] == 'C' ? "C" : "EE"
                };
              }
              else
              {
                status = WelderStatus.CONNECTED;
              }

              connectedPort = $"USR-N520:{firstDevice.IP}:{rs232Result.PortName}";
              connectedBaudRate = rs232Result.BaudRate;

              // Zapisz połączenie USR-N520 do późniejszego użycia
              usrConnection = usrManager;
              usrConnectedPort = rs232Result.PortName;

              // Zapisz informację o wybranym porcie (RS-232 lub RS-485)
              if (rs232Result.PortName.Contains("RS-232"))
                usrPortMode = "0";
              else if (rs232Result.PortName.Contains("RS-485"))
                usrPortMode = "1";

              // Oznacz, że komunikacja jest przez USR-N520
              wasUSRConnection = true;
              Log($"Ustawiono wasUSRConnection = true");

              // Ustaw globalny typ komunikacji na USR-N520
              globalCommunicationType = CommunicationType.USR_N520;
              Log($"Ustawiono globalCommunicationType = USR_N520");

              // Zapisz ustawienia do ponownego wykorzystania
              settings.CommType = "TCP";
              settings.USR_IP = firstDevice.IP;
              settings.USR_Port = firstDevice.Port;
              settings.LastPort = $"USR-N520:{firstDevice.IP}:{rs232Result.PortName}";
              settings.LastBaudRate = rs232Result.BaudRate;
              settings.Save();

              // Nie zamykaj połączenia - będzie używane później
              // usrManager.Disconnect();

              return new PortScanResult
              {
                PortName = $"USR-N520:{firstDevice.IP}:{rs232Result.PortName}",
                BaudRate = rs232Result.BaudRate,
                Success = true,
                Response = rs232Result.Response
              };
            }
          }

          Log("Nie znaleziono zgrzewarki na żadnym porcie RS-232 urządzenia USR-N520.");
          usrManager.Disconnect();
        }
        else
        {
          Log($"Nie udało się połączyć z urządzeniem USR-N520 na {firstDevice.IP}:{firstDevice.Port}");
        }

        return new PortScanResult
        {
          PortName = "USR-N520",
          BaudRate = 0,
          Success = false,
          Response = "Nie znaleziono zgrzewarki na urządzeniach USR-N520"
        };
      }
      catch (Exception ex)
      {
        Log($"Błąd podczas próby połączenia przez USR-N520: {ex.Message}");
        Log($"Stack trace: {ex.StackTrace}");
        return new PortScanResult
        {
          PortName = "USR-N520",
          BaudRate = 0,
          Success = false,
          Response = $"Błąd USR-N520: {ex.Message}"
        };
      }
    }

    private void PrintFrameTable(byte[] frame)
    {
      // Wiersz z numerami bajtów
      Log("Bajt:   " + string.Join(" ", Enumerable.Range(0, frame.Length).Select(i => i.ToString("D2")).ToArray()));
      // Wiersz z wartościami HEX
      Log("HEX:    " + string.Join(" ", frame.Select(b => b.ToString("X2")).ToArray()));
      // Wiersz z wartościami ASCII (drukowalne znaki, reszta kropka)
      Log("ASCII:  " + string.Join(" ", frame.Select(b => b >= 32 && b <= 126 ? ((char)b).ToString() : ".").ToArray()));
      Log("");
    }

    public void Connect()
    {
      status = WelderStatus.NO_CONNECTION;
      connectedPort = null;
      connectedBaudRate = null;

      // Najpierw spróbuj na ostatnio używanym porcie
      if (settings.LastPort != null && settings.LastBaudRate != null)
      {
        var result = TryConnectToPort(settings.LastPort, settings.LastBaudRate.Value);
        if (result.Success)
          return;
      }

      // Jeśli nie udało się połączyć na ostatnim porcie, skanuj wszystkie
      int[] baudRates = { 19200, 115200 };
      foreach (var portName in SerialPort.GetPortNames())
      {
        if (portName == settings.LastPort)
          continue;

        foreach (var baud in baudRates)
        {
          var result = TryConnectToPort(portName, baud);
          if (result.Success)
          {
            settings.LastPort = portName;
            settings.LastBaudRate = baud;
            settings.Save();
            return;
          }
        }
      }

      // Jeśli nie znaleziono zgrzewarki na portach COM, spróbuj przez USR-N520
      Log("Nie znaleziono zgrzewarki na portach COM. Próbuję połączyć się przez urządzenia USR-N520...");
      var usrResult = TryConnectThroughUSR();
      if (usrResult?.Success == true)
      {
        // Zapisz informacje o połączeniu USR-N520
        settings.LastPort = usrResult.PortName;
        settings.LastBaudRate = usrResult.BaudRate;
        settings.Save();

        // Oznacz, że komunikacja jest przez USR-N520
        wasUSRConnection = true;

        // Ustaw globalny typ komunikacji na USR-N520
        globalCommunicationType = CommunicationType.USR_N520;

        return;
      }

      // Jeśli nie znaleziono zgrzewarki, ustaw odpowiedni status
      if (status == WelderStatus.NO_CONNECTION)
      {
        status = WelderStatus.DEVICE_NOT_FOUND;
      }
    }

    public bool QueryWelderType()
    {
      if (status != WelderStatus.CONNECTED) return false;

      try
      {
        using (var port = new SerialPort(connectedPort!, connectedBaudRate!.Value, Parity.None, 8, StopBits.One))
        {
          port.ReadTimeout = 2000;
          port.WriteTimeout = 500;
          port.Open();
          port.DiscardInBuffer();
          port.DiscardOutBuffer();

          byte[] cmd = WelderCommands.BuildTypeQueryCommand(BezSzyfrowania);
          port.Write(cmd, 0, cmd.Length);

          // Odbiór do CRLF lub timeout 1s bez znaku
          string response = ReadResponseToCRLF(port, 2000);
          if (!string.IsNullOrEmpty(response))
          {
            welderInfo = new WelderInfo();
            if (response.StartsWith("AGRE"))
            {
              welderInfo.IsNewUnit = true;
              if (response.Length >= 6 && response[4] == 'V' && response[5] == 'C')
              {
                welderInfo.Version = "C";
              }
              else
              {
                welderInfo.Version = "EE";
              }
              status = WelderStatus.NEW_WELDER;
              return true;
            }
            else
            {
              welderInfo.Type = response.TrimEnd('\r', '\n');
              return true;
            }
          }
        }
      }
      catch { /* ignoruj błędy portów */ }
      return false;
    }

    // Nowa metoda do wyświetlania statusu połączenia
    public void DisplayConnectionStatus()
    {
      string statusDescription = GetStatusDescription(status);
      if ((status == WelderStatus.CONNECTED || status == WelderStatus.NEW_WELDER) && connectedPort != null && connectedBaudRate != null)
      {
        Log($"{statusDescription} na porcie {connectedPort} z prędkością {connectedBaudRate} baud");
      }
      else
      {
        Log(statusDescription);
      }
    }

    private string GetStatusDescription(WelderStatus status)
    {
      var field = status.GetType().GetField(status.ToString());
      if (field == null) return status.ToString();

      var attribute = (DescriptionAttribute?)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
      return attribute?.Description ?? status.ToString();
    }

    public bool ReadConfigurationRegister(out byte[] configData)
    {
      configData = new byte[256];  // Inicjalizacja bufora na dane konfiguracji
      if (status != WelderStatus.CONNECTED && status != WelderStatus.NEW_WELDER) return false;

      // Sprawdź czy połączenie jest przez USR-N520
      if (usrConnection != null && !string.IsNullOrEmpty(usrConnectedPort))
      {
        return ReadConfigurationRegisterThroughUSR(out configData);
      }

      try
      {
        using (var port = new SerialPort(connectedPort!, connectedBaudRate!.Value, Parity.None, 8, StopBits.One))
        {
          port.ReadTimeout = 2000;  // Zwiększony timeout do 2 sekund
          port.WriteTimeout = 500;
          port.Open();
          port.DiscardInBuffer();
          port.DiscardOutBuffer();

          byte[] cmd = WelderCommands.BuildReadConfigCommand(BezSzyfrowania);
          Log("Wysyłanie komendy odczytu rejestru konfiguracji:");
          PrintFrameTable(cmd);

          port.Write(cmd, 0, cmd.Length);

          // Odbiór do CRLF lub timeout 1s bez znaku
          string response = ReadResponseToCRLF(port, 2000);
          if (string.IsNullOrEmpty(response))
          {
            Log("Błąd: Nie otrzymano odpowiedzi");
            return false;
          }

          if (response.Length > 256)
          {
            Log("Błąd: Odpowiedź zbyt długa");
            return false;
          }

          // Kopiuj dane konfiguracji do bufora wyjściowego (bez CRLF)
          Array.Copy(System.Text.Encoding.ASCII.GetBytes(response), configData, response.Length);

          Log($"\nOdebrano {response.Length} bajtów:");
          Log("Offset    00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F    ASCII");
          Log("--------  -----------------------------------------------    ----------------");

          for (int i = 0; i < response.Length; i += 16)
          {
            // Wyświetl offset
            var line = $"{i:X8}  ";

            // Wyświetl bajty w hex
            for (int j = 0; j < 16; j++)
            {
              if (i + j < response.Length)
                line += $"{response[i + j]:X2} ";
              else
                line += "   ";
            }

            // Wyświetl ASCII
            line += "   ";
            for (int j = 0; j < 16 && i + j < response.Length; j++)
            {
              char c = response[i + j];
              if (c >= 32 && c <= 126)  // Drukowalne znaki ASCII
                line += c;
              else
                line += ".";
            }
            Log(line);
          }
          return true;
        }
      }
      catch (Exception ex)
      {
        Log($"Błąd podczas odczytu rejestru konfiguracji: {ex.Message}");
      }
      return false;
    }

    private bool ReadConfigurationRegisterThroughUSR(out byte[] configData)
    {
      configData = new byte[256];
      try
      {
        if (usrConnection == null || string.IsNullOrEmpty(usrConnectedPort))
        {
          Log("Błąd: Brak aktywnego połączenia USR-N520");
          return false;
        }

        byte[] cmd = WelderCommands.BuildReadConfigCommand(BezSzyfrowania);
        Log("Wysyłanie komendy odczytu rejestru konfiguracji przez USR-N520:");
        PrintFrameTable(cmd);

        // Wyślij komendę przez USR-N520 - używamy Task.Run aby uniknąć deadlock
        var sendTask = Task.Run(async () => await usrConnection.SendDataAsync(cmd));
        if (!sendTask.Wait(5000)) // 5 sekund timeout
        {
          Log("Timeout podczas wysyłania komendy przez USR-N520");
          return false;
        }

        // Odbierz odpowiedź - używamy Task.Run aby uniknąć deadlock
        var receiveTask = Task.Run(async () => await usrConnection.ReceiveDataBytesAsync());
        if (!receiveTask.Wait(5000)) // 5 sekund timeout
        {
          Log("Timeout podczas odbioru odpowiedzi przez USR-N520");
          return false;
        }

        var responseBytes = receiveTask.Result;
        if (responseBytes == null || responseBytes.Length == 0)
        {
          Log("Błąd: Nie otrzymano odpowiedzi przez USR-N520");
          return false;
        }

        string response = System.Text.Encoding.ASCII.GetString(responseBytes);
        if (string.IsNullOrEmpty(response))
        {
          Log("Błąd: Pusta odpowiedź przez USR-N520");
          return false;
        }

        // Kopiuj dane konfiguracji do bufora wyjściowego
        int copyLen = Math.Min(responseBytes.Length, configData.Length);
        Array.Copy(responseBytes, configData, copyLen);

        Log($"\nOdebrano {response.Length} bajtów przez USR-N520:");
        Log("Offset    00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F    ASCII");
        Log("--------  -----------------------------------------------    ----------------");

        for (int i = 0; i < response.Length; i += 16)
        {
          // Wyświetl offset
          var line = $"{i:X8}  ";

          // Wyświetl bajty w hex
          for (int j = 0; j < 16; j++)
          {
            if (i + j < response.Length)
              line += $"{responseBytes[i + j]:X2} ";
            else
              line += "   ";
          }

          // Wyświetl ASCII
          line += "   ";
          for (int j = 0; j < 16 && i + j < response.Length; j++)
          {
            char c = response[i + j];
            if (c >= 32 && c <= 126)  // Drukowalne znaki ASCII
              line += c;
            else
              line += ".";
          }
          Log(line);
        }
        return true;
      }
      catch (Exception ex)
      {
        Log($"Błąd podczas odczytu rejestru konfiguracji przez USR-N520: {ex.Message}");
      }
      return false;
    }

    public int? ReadWeldCount()
    {
      if (status != WelderStatus.CONNECTED && status != WelderStatus.NEW_WELDER) return null;

      try
      {
        using (var port = new SerialPort(connectedPort!, connectedBaudRate!.Value, Parity.None, 8, StopBits.One))
        {
          port.ReadTimeout = 2000;  // Zwiększony timeout do 2 sekund
          port.WriteTimeout = 500;
          port.Open();
          port.DiscardInBuffer();
          port.DiscardOutBuffer();

          byte[] cmd = WelderCommands.BuildReadWeldCountCommand(BezSzyfrowania);
          Log("Wysyłanie komendy odczytu liczby zgrzewów:");
          PrintFrameTable(cmd);

          port.Write(cmd, 0, cmd.Length);

          // Odbiór do CRLF lub timeout 1s bez znaku
          string response = ReadResponseToCRLF(port, 2000);
          if (!string.IsNullOrEmpty(response))
          {
            // Konwertuj string na liczbę
            if (int.TryParse(response, out int result))
            {
              Log($"Liczba zgrzewów w pamięci: {result}");
              return result;
            }
            else
            {
              Log("Błąd: Nie można przekonwertować odpowiedzi na liczbę");
            }
          }
          else
          {
            Log("Błąd: Nie otrzymano odpowiedzi");
          }
        }
      }
      catch (Exception ex)
      {
        Log($"Błąd podczas odczytu liczby zgrzewów: {ex.Message}");
      }
      return null;
    }

    public static WeldParameters ParseWeldParameters(string response)
    {
      var result = new WeldParameters();
      var parts = response.Split(';');
      foreach (var part in parts)
      {
        var kv = part.Split(':');
        if (kv.Length != 2)
        {
          continue;
        }
        switch (kv[0])
        {
          case "U":
            result.NapiecieZgrzewania = double.Parse(kv[1].Replace('.', ',')); break;
          case "I":
            result.PradZgrzewania = double.Parse(kv[1].Replace('.', ',')); break;
          case "ADCU":
            result.ADCNapZgrzew = Convert.ToInt32(kv[1], 16); break;
          case "ADCI":
            result.ADCPradZgrzew = Convert.ToInt32(kv[1], 16); break;
          case "IVHC-U":
            result.IVHC_U = int.Parse(kv[1]); break;
          case "IVLC-U":
            result.IVLC_U = int.Parse(kv[1]); break;
          case "ADCIVHC-U":
            result.ADCIVHC_U = int.Parse(kv[1]); break;
          case "ADCIVLC-U":
            result.ADCIVLC_U = int.Parse(kv[1]); break;
          case "IVHC-I":
            result.IMHC_I = int.Parse(kv[1]); break;
          case "IVLC-I":
            result.IMLC_I = int.Parse(kv[1]); break;
          case "ADCIVHC-I":
            result.ADCIVHC_I = int.Parse(kv[1]); break;
          case "ADCIVLC-I":
            result.ADCIVLC_I = int.Parse(kv[1]); break;
          case "MMWVL":
            result.MMWVL = int.Parse(kv[1]); break;
          case "MMWVH":
            result.MMWVH = int.Parse(kv[1]); break;
          case "MMWCL":
            result.MMWCL = int.Parse(kv[1]); break;
          case "MMWCH":
            result.MMWCH = int.Parse(kv[1]); break;
          default:
            break;
        }
      }
      return result;
    }


    public WeldParameters? ReadWeldParameters(out string? errorDetails)
    {
      errorDetails = null;
      if (status != WelderStatus.CONNECTED && status != WelderStatus.NEW_WELDER)
      {
        errorDetails = "Brak połączenia ze zgrzewarką.";
        return null;
      }

      // Dodaj szczegółowe logowanie stanu połączenia
      Log($"=== DIAGNOSTYKA STANU POŁĄCZENIA ===");
      Log($"Status: {status}");
      Log($"ConnectedPort: {connectedPort}");
      Log($"ConnectedBaudRate: {connectedBaudRate}");
      Log($"GlobalCommunicationType: {globalCommunicationType}");
      Log($"WasUSRConnection: {wasUSRConnection}");
      Log($"USRConnection: {(usrConnection != null ? "istnieje" : "null")}");
      Log($"USRConnectedPort: {usrConnectedPort}");
      Log($"USRConnection.IsConnected: {(usrConnection?.IsConnected == true ? "true" : "false")}");
      Log($"Settings.CommType: {settings.CommType}");
      Log($"Settings.USR_IP: {settings.USR_IP}");
      Log($"Settings.USR_Port: {settings.USR_Port}");
      Log($"Settings.COM_Port: {settings.COM_Port}");
      Log($"Settings.COM_Baud: {settings.COM_Baud}");
      Log($"=== KONIEC DIAGNOSTYKI ===");

      // Użyj globalnego typu komunikacji ustalonego podczas skanowania
      Log($"Używam globalnego typu komunikacji: {globalCommunicationType}");

      switch (globalCommunicationType)
      {
        case CommunicationType.USR_N520:
          Log("WYBIERAM: ReadWeldParametersThroughUSR");
          return ReadWeldParametersThroughUSR(out errorDetails);

        case CommunicationType.COM_PORT:
          Log("WYBIERAM: ReadWeldParametersThroughCOM");
          return ReadWeldParametersThroughCOM(out errorDetails);

        default:
          errorDetails = $"Nieznany typ komunikacji: {globalCommunicationType}";
          Log($"BŁĄD: Nieznany typ komunikacji: {globalCommunicationType}");
          return null;
      }
    }

    private WeldParameters? ReadWeldParametersThroughUSR(out string? errorDetails)
    {
      errorDetails = null;

      // Jeśli połączenie jest nieaktywne, spróbuj ponownie nawiązać je kilka razy
      for (int attempt = 0; attempt < USR_RETRY_COUNT; attempt++)
      {
        Log($"Próba {attempt + 1}/{USR_RETRY_COUNT} połączenia z USR-N520");

        if (usrConnection != null && usrConnection.IsConnected)
        {
          Log("Połączenie USR-N520 jest aktywne, próbuję odczytać parametry");
          var result = TryReadWeldParametersThroughUSR(out errorDetails);
          if (result != null)
          {
            Log("Pomyślnie odczytano parametry przez USR-N520");
            return result;
          }
          else
          {
            Log($"Błąd odczytu przez USR-N520: {errorDetails}");
          }
        }
        else
        {
          Log("Połączenie USR-N520 nie jest aktywne, próbuję ponownie nawiązać połączenie");
          if (!TryReconnectToUSR())
          {
            Log($"Nie udało się ponownie połączyć z USR-N520 (próba {attempt + 1})");
          }
        }

        if (attempt < USR_RETRY_COUNT - 1)
        {
          Log($"Czekam {USR_RETRY_DELAY_MS}ms przed kolejną próbą...");
          System.Threading.Thread.Sleep(USR_RETRY_DELAY_MS);
        }
      }

      // Po kilku próbach nie udało się połączyć z USR-N520
      errorDetails = "Utracono połączenie z urządzeniem USR-N520. Sprawdź połączenie sieciowe lub zasilanie urządzenia.";
      Log("Wszystkie próby połączenia z USR-N520 nie powiodły się");
      return null;
    }

    private bool TryReconnectToUSR()
    {
      try
      {
        // Spróbuj ponownie nawiązać połączenie z ostatnim znanym urządzeniem USR
        if (!string.IsNullOrEmpty(usrConnectedPort) && !string.IsNullOrEmpty(connectedPort))
        {
          // connectedPort format: "USR-N520 192.168.0.7:23"
          if (connectedPort!.StartsWith("USR-N520 "))
          {
            var parts = connectedPort.Substring("USR-N520 ".Length).Split(':');
            if (parts.Length >= 2)
            {
              string ip = parts[0];
              if (int.TryParse(parts[1], out int port))
              {
                Log($"Próbuję ponownie połączyć się z USR-N520 na {ip}:{port}");
                usrConnection = new USRDeviceManager(ip, port);
                if (usrConnection.ConnectAsync().Result)
                {
                  Log($"Ponownie połączono z USR-N520 na {ip}:{port}");
                  return true;
                }
                else
                {
                  Log($"Nie udało się ponownie połączyć z USR-N520 na {ip}:{port}");
                }
              }
              else
              {
                Log($"Nieprawidłowy port w connectedPort: {parts[1]}");
              }
            }
            else
            {
              Log($"Nieprawidłowy format connectedPort: {connectedPort}");
            }
          }
          else
          {
            Log($"ConnectedPort nie jest w formacie USR-N520: {connectedPort}");
          }
        }
        else
        {
          Log($"Brak informacji o połączeniu USR: usrConnectedPort='{usrConnectedPort}', connectedPort='{connectedPort}'");
        }
      }
      catch (Exception ex)
      {
        Log($"Błąd podczas ponownego łączenia z USR-N520: {ex.Message}");
      }
      return false;
    }

    private WeldParameters? TryReadWeldParametersThroughUSR(out string? errorDetails)
    {
      errorDetails = null;
      try
      {
        if (usrConnection == null || string.IsNullOrEmpty(usrConnectedPort))
        {
          errorDetails = "Brak aktywnego połączenia USR-N520";
          return null;
        }

        byte[] cmd = WelderCommands.BuildReadWeldParametersCommand(BezSzyfrowania);
        Log("Wysyłanie komendy odczytu parametrów zgrzewania przez USR-N520:");
        PrintFrameTable(cmd);

        // Wyślij komendę przez USR-N520 - używamy Task.Run aby uniknąć deadlock
        var sendTask = Task.Run(async () => await usrConnection.SendDataAsync(cmd));
        if (!sendTask.Wait(5000)) // 5 sekund timeout
        {
          errorDetails = "Timeout podczas wysyłania komendy przez USR-N520";
          return null;
        }

        // Odbierz odpowiedź - używamy Task.Run aby uniknąć deadlock
        var receiveTask = Task.Run(async () => await usrConnection.ReceiveDataBytesAsync());
        if (!receiveTask.Wait(5000)) // 5 sekund timeout
        {
          errorDetails = "Timeout podczas odbioru odpowiedzi przez USR-N520";
          return null;
        }

        var responseBytes = receiveTask.Result;
        if (responseBytes == null || responseBytes.Length == 0)
        {
          errorDetails = "Nie otrzymano odpowiedzi przez USR-N520";
          return null;
        }

        string response = System.Text.Encoding.ASCII.GetString(responseBytes);
        if (string.IsNullOrEmpty(response))
        {
          errorDetails = "Pusta odpowiedź przez USR-N520";
          return null;
        }

        try
        {
          var result = ParseWeldParameters(response);
          if (result == null)
          {
            errorDetails = "Nieprawidłowy format odpowiedzi przez USR-N520";
            return null;
          }

          // Logowanie wszystkich parametrów zgrzewania
          Log($"\nParametry zgrzewania (USR-N520):");
          Log($"Napięcie zgrzewania: {result.NapiecieZgrzewania:F2} V");
          Log($"Prąd zgrzewania: {result.PradZgrzewania:F2} A");
          Log($"\nWartości ADC:");
          Log($"ADC napięcia zgrzewania: {result.ADCNapZgrzew:X4}");
          Log($"ADC prądu zgrzewania: {result.ADCPradZgrzew:X4}");
          Log($"\nParametry kalibracji napięcia:");
          Log($"IVHC-U: {result.IVHC_U}");
          Log($"IVLC-U: {result.IVLC_U}");
          Log($"ADCIVHC-U: {result.ADCIVHC_U}");
          Log($"ADCIVLC-U: {result.ADCIVLC_U}");
          Log($"\nParametry kalibracji prądu:");
          Log($"IVHC-I: {result.IMHC_I}");
          Log($"IVLC-I: {result.IMLC_I}");
          Log($"ADCIVHC-I: {result.ADCIVHC_I}");
          Log($"ADCIVLC-I: {result.ADCIVLC_I}");
          Log($"\nWartości multimetru:");
          Log($"MMWVL: {result.MMWVL}");
          Log($"MMWVH: {result.MMWVH}");
          Log($"MMWCL: {result.MMWCL}");
          Log($"MMWCH: {result.MMWCH}");
          return result;
        }
        catch (Exception ex)
        {
          errorDetails = $"Błąd parsowania odpowiedzi przez USR-N520: {ex.Message}";
          return null;
        }
      }
      catch (Exception ex)
      {
        errorDetails = $"Błąd komunikacji przez USR-N520: {ex.Message}";
        return null;
      }
    }

    private WeldParameters? ReadWeldParametersThroughCOM(out string? errorDetails)
    {
      errorDetails = null;
      try
      {
        using (var port = new SerialPort(connectedPort!, connectedBaudRate!.Value, Parity.None, 8, StopBits.One))
        {
          port.ReadTimeout = 2000;
          port.WriteTimeout = 500;
          port.Open();
          port.DiscardInBuffer();
          port.DiscardOutBuffer();

          byte[] cmd = WelderCommands.BuildReadWeldParametersCommand(BezSzyfrowania);
          Log("Wysyłanie komendy odczytu parametrów zgrzewania przez COM:");
          PrintFrameTable(cmd);

          port.Write(cmd, 0, cmd.Length);

          // Odbiór do CRLF lub timeout 1s bez znaku
          string response = ReadResponseToCRLF(port, 2000);
          if (string.IsNullOrEmpty(response))
          {
            errorDetails = "Brak odpowiedzi od zgrzewarki przez COM.";
            return null;
          }

          try
          {
            var result = ParseWeldParameters(response);
            if (result == null)
            {
              errorDetails = "Nieprawidłowy format odpowiedzi przez COM.";
              return null;
            }
            // Logowanie wszystkich parametrów zgrzewania
            Log($"\nParametry zgrzewania (COM):");
            Log($"Napięcie zgrzewania: {result.NapiecieZgrzewania:F2} V");
            Log($"Prąd zgrzewania: {result.PradZgrzewania:F2} A");
            Log($"\nWartości ADC:");
            Log($"ADC napięcia zgrzewania: {result.ADCNapZgrzew:X4}");
            Log($"ADC prądu zgrzewania: {result.ADCPradZgrzew:X4}");
            Log($"\nParametry kalibracji napięcia:");
            Log($"IVHC-U: {result.IVHC_U}");
            Log($"IVLC-U: {result.IVLC_U}");
            Log($"ADCIVHC-U: {result.ADCIVHC_U}");
            Log($"ADCIVLC-U: {result.ADCIVLC_U}");
            Log($"\nParametry kalibracji prądu:");
            Log($"IVHC-I: {result.IMHC_I}");
            Log($"IVLC-I: {result.IMLC_I}");
            Log($"ADCIVHC-I: {result.ADCIVHC_I}");
            Log($"ADCIVLC-I: {result.ADCIVLC_I}");
            Log($"\nWartości multimetru:");
            Log($"MMWVL: {result.MMWVL}");
            Log($"MMWVH: {result.MMWVH}");
            Log($"MMWCL: {result.MMWCL}");
            Log($"MMWCH: {result.MMWCH}");
            return result;
          }
          catch (Exception ex)
          {
            errorDetails = $"Błąd parsowania odpowiedzi przez COM: {ex.Message}";
            return null;
          }
        }
      }
      catch (Exception ex)
      {
        errorDetails = $"Wyjątek komunikacji przez COM: {ex.Message}";
      }
      return null;
    }

    // Pomocnicza metoda do odbioru odpowiedzi do CRLF lub timeout
    private string ReadResponseToCRLF(SerialPort port, int timeoutMs = 1000)
    {
      StringBuilder responseBuilder = new StringBuilder();
      DateTime startTime = DateTime.Now;
      DateTime lastByteTime = DateTime.Now;
      bool crlfFound = false;

      while ((DateTime.Now - lastByteTime).TotalMilliseconds < timeoutMs)
      {
        if (port.BytesToRead > 0)
        {
          int b = port.ReadByte();
          responseBuilder.Append((char)b);
          lastByteTime = DateTime.Now;  // Resetuj timeout od ostatniego bajtu

          if (responseBuilder.Length >= 2 &&
                responseBuilder[^2] == '\r' && responseBuilder[^1] == '\n')
          {
            crlfFound = true;
            break;
          }
        }
        else
        {
          // Jeśli nie ma danych, sprawdź czy minął timeout od ostatniego bajtu
          if ((DateTime.Now - lastByteTime).TotalMilliseconds >= timeoutMs)
          {
            break;
          }
          System.Threading.Thread.Sleep(10);
        }
      }

      string result = responseBuilder.ToString();
      Log($"Odebrano {result.Length} znaków, CRLF: {crlfFound}, Timeout: {(DateTime.Now - startTime).TotalMilliseconds:F0}ms");

      if (crlfFound)
      {
        return result.TrimEnd('\r', '\n');
      }
      return result;
    }

    public static string GetWelderName(int index)
    {
      string[] txt_nazwy_zgrzewarek = {
                                                                                    "n/d                 ", // 0
                                                                                    "ZK90ECO             ", // 1
                                                                                    "ZK160ECO            ", // 2
                                                                                    "ZK250PRO            ", // 3
                                                                                    "ZK400PRO            ", // 4
                                                                                    "ZK401SE800          ", // 5
                                                                                    "ZK401SE1200         ", // 6
                                                                                    "ZK401SE2000         ", // 7
                                                                                    "ZK401SE3000         ", // 8
                                                                                    "EUROTECH800S        ", // 9
                                                                                    "EUROTECH1200S       ", // 10
                                                                                    "EUROTECH2000S       ", // 11
                                                                                    "EUROTECH3000S       ", // 12
                                                                                    "ZK90PRO             ", // 13
                                                                                    "ZK160PRO            ", // 14
                                                                                    "ZK315PRO            "  // 15
                                                                                    };
      if (index >= 0 && index < txt_nazwy_zgrzewarek.Length)
        return txt_nazwy_zgrzewarek[index].Trim();
      return "n/d";
    }

    // Nowa metoda: Skanowanie tylko portów COM
    public bool ScanComPortsOnly(string? preferredPort = null, int? preferredBaud = null)
    {
      Log("=== SKANOWANIE TYLKO PORTÓW COM ===");
      int[] baudRates = { 19200, 115200 };
      var availablePorts = SerialPort.GetPortNames();

      if (availablePorts.Length == 0)
      {
        Log("Nie znaleziono żadnych dostępnych portów COM.");
        return false;
      }

      // Jeśli podano preferowany port, sprawdź czy jest dostępny
      if (!string.IsNullOrEmpty(preferredPort))
      {
        if (!availablePorts.Contains(preferredPort))
        {
          Log($"Preferowany port {preferredPort} nie jest dostępny.");
          Log($"Dostępne porty: {string.Join(", ", availablePorts)}");
          return false;
        }

        if (preferredBaud.HasValue)
        {
          Log($"Skanuję tylko preferowany port: {preferredPort} z prędkością {preferredBaud} baud");
        }
        else
        {
          Log($"Skanuję tylko preferowany port: {preferredPort}");
        }
        Log($"Dostępne porty: {string.Join(", ", availablePorts)}");
      }
      else
      {
        Log($"Skanuję wszystkie dostępne porty COM.");
        Log($"Znaleziono {availablePorts.Length} portów COM: {string.Join(", ", availablePorts)}");
      }

      // Słownik: [baud][port] = wynik
      var matrix = new Dictionary<int, Dictionary<string, string>>();
      bool foundAny = false;

      // Określ które porty i prędkości skanować
      var portsToScan = !string.IsNullOrEmpty(preferredPort) ? new[] { preferredPort } : availablePorts;
      var baudsToScan = preferredBaud.HasValue ? new[] { preferredBaud.Value } : baudRates;

      foreach (var baud in baudsToScan)
      {
        matrix[baud] = new Dictionary<string, string>();
        foreach (var portName in portsToScan)
        {
          try
          {
            Log($"Sprawdzam port {portName} z prędkością {baud} baud...");
            using (var port = new SerialPort(portName, baud, Parity.None, 8, StopBits.One))
            {
              port.ReadTimeout = 2000;
              port.WriteTimeout = 500;
              port.Open();
              port.DiscardInBuffer();
              port.DiscardOutBuffer();
              byte[] cmd = WelderCommands.BuildIdentifyCommand(BezSzyfrowania);
              port.Write(cmd, 0, cmd.Length);
              string response = ReadResponseToCRLF(port, 2000);
              if (!string.IsNullOrEmpty(response) && (response.Contains("ZGRZ") || response.Contains("AGRE")))
              {
                settings.CommType = "COM";
                settings.COM_Port = portName;
                settings.COM_Baud = baud;
                settings.Save();
                Log($"X Zgrzewarka znaleziona na porcie COM {portName} ({baud} baud)!");
                matrix[baud][portName] = "X";
                foundAny = true;

                // Jeśli skanujemy tylko preferowany port i znaleźliśmy zgrzewarkę, przerwij
                if (!string.IsNullOrEmpty(preferredPort))
                {
                  Log($"Znaleziono zgrzewarkę na preferowanym porcie {preferredPort}!");
                  return true;
                }
              }
              else
              {
                Log($"  Brak odpowiedzi z {portName} ({baud} baud)");
                matrix[baud][portName] = " ";
              }
            }
          }
          catch (Exception ex)
          {
            Log($"  Błąd COM {portName} {baud}: {ex.Message}");
            matrix[baud][portName] = "ERR";
          }
        }
      }

      // Wyświetl tabelę tylko jeśli skanujemy wszystkie porty
      if (string.IsNullOrEmpty(preferredPort))
      {
        // Wyznacz szerokości kolumn
        int baudColWidth = 8;
        var portColWidths = availablePorts.Select(p => Math.Max(8, p.Length + 2)).ToArray();

        // Nagłówek
        string header = "|" + CenterText("Baud", baudColWidth) + "|";
        for (int i = 0; i < availablePorts.Length; i++)
          header += CenterText(availablePorts[i], portColWidths[i]) + "|";
        // Separator
        string sep = "|" + new string('-', baudColWidth) + "|";
        for (int i = 0; i < portColWidths.Length; i++)
          sep += new string('-', portColWidths[i]) + "|";

        Log("");
        Log("Podsumowanie skanowania portów COM:");
        Log(header);
        Log(sep);
        // Wiersze
        foreach (var baud in baudsToScan)
        {
          string row = "|" + CenterText(baud.ToString(), baudColWidth) + "|";
          for (int i = 0; i < availablePorts.Length; i++)
          {
            string val = matrix[baud][availablePorts[i]];
            row += CenterText(val, portColWidths[i]) + "|";
          }
          Log(row);
        }
        Log("");
      }

      if (foundAny)
        return true;
      Log("X Nie znaleziono zgrzewarki na żadnym porcie COM.");
      return false;
    }

    // Pomocnicza metoda do centrowania tekstu w kolumnie
    private string CenterText(string text, int width)
    {
      if (string.IsNullOrEmpty(text)) text = "";
      int padding = width - text.Length;
      int padLeft = padding / 2 + text.Length;
      return text.PadLeft(padLeft).PadRight(width);
    }

    // Nowa metoda: Skanowanie tylko urządzeń USR-N520
    public bool ScanUSRDevicesOnly()
    {
      Log("=== SKANOWANIE TYLKO URZĄDZEŃ USR-N520 ===");
      Log("USR-N520 ma 2 fizyczne porty: RS-232 (9-pin D-sub) i RS-485 (2-wire A+, B-)");

      string usrIp = "192.168.0.7";
      int usrPort = 23;

      try
      {
        Log($"Próbuję połączyć się z USR-N520 na {usrIp}:{usrPort}...");

        if (usrConnection == null)
        {
          usrConnection = new USRDeviceManager(usrIp, usrPort);
        }

        if (!usrConnection.IsConnected)
        {
          if (!usrConnection.ConnectAsync().Result)
          {
            Log($"✗ Nie udało się połączyć z USR-N520 na {usrIp}:{usrPort}");
            return false;
          }
        }

        Log($"✓ Połączono z USR-N520 na {usrIp}:{usrPort}");

        // Skanowanie tylko wysyła komendę identyfikacyjną
        byte[] cmd = WelderCommands.BuildIdentifyCommand(BezSzyfrowania);
        usrConnection.SendDataAsync(cmd).Wait();
        var response = usrConnection.ReceiveDataAsync(2000).Result;
        bool found = response.Contains("ZGRZ") || response.Contains("AGRE");

        if (found)
        {
          // Ustaw wszystkie zmienne stanu po udanym znalezieniu zgrzewarki
          status = WelderStatus.CONNECTED;
          globalCommunicationType = CommunicationType.USR_N520;
          connectedPort = $"USR-N520 {usrIp}:{usrPort}";
          connectedBaudRate = 115200; // Domyślny baud rate dla USR
          wasUSRConnection = true;
          usrConnectedPort = $"{usrIp}:{usrPort}";

          // Zapisz ustawienia do pliku
          settings.CommType = "TCP";
          settings.USR_IP = usrIp;
          settings.USR_Port = usrPort;
          settings.Save();

          Log("✓ Zgrzewarka znaleziona przez TCP/IP (USR-N520)!");
          Log($"✓ Ustawiono globalCommunicationType = {globalCommunicationType}");
          Log($"✓ Ustawiono connectedPort = {connectedPort}");
          // NIE rozłączaj usrConnection - zostaw je aktywne
          return true;
        }
        else
        {
          Log("✗ Brak odpowiedzi od zgrzewarki przez USR-N520");
          return false;
        }
      }
      catch (Exception ex)
      {
        Log($"✗ Błąd TCP/IP: {ex.Message}");
        return false;
      }
    }

    // Nowa metoda: Skanowanie i zapisywanie ustawień
    public bool ScanAndSaveSettings()
    {
      Log("=== SKANOWANIE WSZYSTKICH URZĄDZEŃ ===");

      // Najpierw próbuj TCP/IP (USR-N520)
      if (ScanUSRDevicesOnly())
      {
        return true;
      }

      // Jeśli nie znaleziono przez TCP/IP, próbuj portów COM
      Log("Przechodzę do skanowania portów COM...");
      return ScanComPortsOnly();
    }

    // Nowa metoda: RUN z użyciem zapisanych ustawień
    public bool RunWithSavedSettings()
    {
      if (settings.CommType == "TCP")
      {
        string ip = settings.USR_IP ?? "";
        int port = settings.USR_Port ?? 23;
        try
        {
          if (usrConnection == null)
          {
            usrConnection = new USRDeviceManager(ip, port);
          }
          if (!usrConnection.IsConnected)
          {
            if (!usrConnection.ConnectAsync().Result)
            {
              Log($"Nie udało się połączyć przez TCP/IP. Skanuj ponownie.");
              return false;
            }
          }
          Log($"Połączono z USR-N520 na {ip}:{port}");
          // Ustaw zmienne stanu po udanym połączeniu
          status = WelderStatus.CONNECTED;
          globalCommunicationType = CommunicationType.USR_N520;
          connectedPort = $"USR-N520 {ip}:{port}";
          connectedBaudRate = 115200; // Domyślny baud rate dla USR
          wasUSRConnection = true;
          usrConnectedPort = $"{ip}:{port}";
          // NIE zamykaj połączenia - zostaw je aktywne dla kolejnych operacji
          // Przykład: odczyt parametrów
          byte[] cmd = WelderCommands.BuildReadWeldParametersCommand(BezSzyfrowania);
          usrConnection.SendDataAsync(cmd).Wait();
          var responseBytes = usrConnection.ReceiveDataBytesAsync().Result;
          string response = System.Text.Encoding.ASCII.GetString(responseBytes);
          Log($"Odpowiedź: {response}");
          return true;
        }
        catch (Exception ex)
        {
          Log($"Błąd TCP/IP: {ex.Message}");
          return false;
        }
      }
      else if (settings.CommType == "COM")
      {
        string portName = settings.COM_Port ?? "";
        int baud = settings.COM_Baud ?? 115200;
        try
        {
          using (var port = new SerialPort(portName, baud, Parity.None, 8, StopBits.One))
          {
            port.ReadTimeout = 2000;  // Zwiększony timeout do 2 sekund
            port.WriteTimeout = 500;
            port.Open();
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
            // Ustaw zmienne stanu po udanym połączeniu
            status = WelderStatus.CONNECTED;
            globalCommunicationType = CommunicationType.COM_PORT;
            connectedPort = portName;
            connectedBaudRate = baud;
            wasUSRConnection = false;
            byte[] cmd = WelderCommands.BuildReadWeldParametersCommand(BezSzyfrowania);
            port.Write(cmd, 0, cmd.Length);
            string response = ReadResponseToCRLF(port, 2000);
            Log($"Odpowiedź: {response}");
            return true;
          }
        }
        catch (Exception ex)
        {
          Log($"Błąd COM: {ex.Message}");
          return false;
        }
      }
      else
      {
        Log("Nieznany typ komunikacji.");
        return false;
      }
    }
  }
}