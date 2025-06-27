using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WelderRS232;
using static Logger.LoggerService;

namespace Calibrator.Services
{
    /// <summary>
    /// Singleton service for managing configuration settings accessible by all layers
    /// </summary>
    public class ConfigService
    {
        private static ConfigService? _instance;
        private static readonly object _lock = new object();

        private const string CONFIG_FILE = "welder_settings.json";
        private const string DETECTED_PORTS_FILE = "detected_ports.json";

        private WelderSettings _settings;
        private List<DetectedPort> _detectedPorts;
        private bool _isInitialized = false;

        public event Action<WelderSettings>? SettingsChanged;
        public event Action<List<DetectedPort>>? DetectedPortsChanged;

        private ConfigService()
        {
            _settings = new WelderSettings();
            _detectedPorts = new List<DetectedPort>();
        }

        public static ConfigService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ConfigService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initialize the configuration service
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                await LoadSettingsAsync();
                await LoadDetectedPortsAsync();
                _isInitialized = true;
                Log("ConfigService initialized successfully");
            }
            catch (Exception ex)
            {
                Log($"Error initializing ConfigService: {ex.Message}");
                throw;
            }
        }

        #region Settings Management

        /// <summary>
        /// Get current settings
        /// </summary>
        public WelderSettings GetSettings()
        {
            return _settings;
        }

        /// <summary>
        /// Update settings and save to file
        /// </summary>
        public async Task UpdateSettingsAsync(WelderSettings newSettings)
        {
            _settings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
            await SaveSettingsAsync();
            SettingsChanged?.Invoke(_settings);
            Log("Settings updated and saved");
        }

        /// <summary>
        /// Update specific setting properties
        /// </summary>
        public async Task UpdateSettingsAsync(Action<WelderSettings> updateAction)
        {
            updateAction?.Invoke(_settings);
            await SaveSettingsAsync();
            SettingsChanged?.Invoke(_settings);
            Log("Settings updated and saved");
        }

        /// <summary>
        /// Get communication type
        /// </summary>
        public string? GetCommunicationType()
        {
            return _settings.CommType;
        }

        /// <summary>
        /// Set communication type
        /// </summary>
        public async Task SetCommunicationTypeAsync(string commType)
        {
            _settings.CommType = commType;
            await SaveSettingsAsync();
            SettingsChanged?.Invoke(_settings);
            Log($"Communication type set to: {commType}");
        }

        /// <summary>
        /// Get COM port settings
        /// </summary>
        public (string? port, int? baudRate) GetComSettings()
        {
            return (_settings.COM_Port, _settings.COM_Baud);
        }

        /// <summary>
        /// Set COM port settings
        /// </summary>
        public async Task SetComSettingsAsync(string port, int baudRate)
        {
            _settings.COM_Port = port;
            _settings.COM_Baud = baudRate;
            await SaveSettingsAsync();
            SettingsChanged?.Invoke(_settings);
            Log($"COM settings updated: {port} at {baudRate} baud");
        }

        /// <summary>
        /// Get TCP/IP settings
        /// </summary>
        public (string? ip, int? port) GetTcpSettings()
        {
            return (_settings.USR_IP, _settings.USR_Port);
        }

        /// <summary>
        /// Set TCP/IP settings
        /// </summary>
        public async Task SetTcpSettingsAsync(string ip, int port)
        {
            _settings.USR_IP = ip;
            _settings.USR_Port = port;
            await SaveSettingsAsync();
            SettingsChanged?.Invoke(_settings);
            Log($"TCP settings updated: {ip}:{port}");
        }

        #endregion

        #region Detected Ports Management

        /// <summary>
        /// Get all detected ports
        /// </summary>
        public List<DetectedPort> GetDetectedPorts()
        {
            return new List<DetectedPort>(_detectedPorts);
        }

        /// <summary>
        /// Add or update detected port
        /// </summary>
        public async Task AddDetectedPortAsync(DetectedPort port)
        {
            var existingIndex = _detectedPorts.FindIndex(p => p.Equals(port));
            if (existingIndex >= 0)
            {
                _detectedPorts[existingIndex] = port;
            }
            else
            {
                _detectedPorts.Add(port);
            }

            await SaveDetectedPortsAsync();
            DetectedPortsChanged?.Invoke(_detectedPorts);
            Log($"Detected port updated: {port}");
        }

        /// <summary>
        /// Add multiple detected ports
        /// </summary>
        public async Task AddDetectedPortsAsync(IEnumerable<DetectedPort> ports)
        {
            foreach (var port in ports)
            {
                var existingIndex = _detectedPorts.FindIndex(p => p.Equals(port));
                if (existingIndex >= 0)
                {
                    _detectedPorts[existingIndex] = port;
                }
                else
                {
                    _detectedPorts.Add(port);
                }
            }

            await SaveDetectedPortsAsync();
            DetectedPortsChanged?.Invoke(_detectedPorts);
            Log($"Added {ports.Count()} detected ports");
        }

        /// <summary>
        /// Clear all detected ports
        /// </summary>
        public async Task ClearDetectedPortsAsync()
        {
            _detectedPorts.Clear();
            await SaveDetectedPortsAsync();
            DetectedPortsChanged?.Invoke(_detectedPorts);
            Log("Detected ports cleared");
        }

        /// <summary>
        /// Get detected ports by type
        /// </summary>
        public List<DetectedPort> GetDetectedPortsByType(CommunicationType type)
        {
            return _detectedPorts.FindAll(p => p.Type == type);
        }

        #endregion

        #region File Operations

        private async Task LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(CONFIG_FILE))
                {
                    string json = await File.ReadAllTextAsync(CONFIG_FILE);
                    _settings = JsonSerializer.Deserialize<WelderSettings>(json) ?? new WelderSettings();
                    Log("Settings loaded from file");
                }
                else
                {
                    _settings = new WelderSettings();
                    await SaveSettingsAsync();
                    Log("New settings file created");
                }
            }
            catch (Exception ex)
            {
                Log($"Error loading settings: {ex.Message}");
                _settings = new WelderSettings();
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(CONFIG_FILE, json);
            }
            catch (Exception ex)
            {
                Log($"Error saving settings: {ex.Message}");
                throw;
            }
        }

        private async Task LoadDetectedPortsAsync()
        {
            try
            {
                if (File.Exists(DETECTED_PORTS_FILE))
                {
                    string json = await File.ReadAllTextAsync(DETECTED_PORTS_FILE);
                    _detectedPorts = JsonSerializer.Deserialize<List<DetectedPort>>(json) ?? new List<DetectedPort>();
                    Log("Detected ports loaded from file");
                }
                else
                {
                    _detectedPorts = new List<DetectedPort>();
                    await SaveDetectedPortsAsync();
                    Log("New detected ports file created");
                }
            }
            catch (Exception ex)
            {
                Log($"Error loading detected ports: {ex.Message}");
                _detectedPorts = new List<DetectedPort>();
            }
        }

        private async Task SaveDetectedPortsAsync()
        {
            try
            {
                string json = JsonSerializer.Serialize(_detectedPorts, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(DETECTED_PORTS_FILE, json);
            }
            catch (Exception ex)
            {
                Log($"Error saving detected ports: {ex.Message}");
                throw;
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a detected communication port
    /// </summary>
    public class DetectedPort
    {
        public string Name { get; set; } = string.Empty;
        public CommunicationType Type { get; set; }
        public string? IpAddress { get; set; }
        public int? Port { get; set; }
        public int? BaudRate { get; set; }
        public bool IsConnected { get; set; }
        public DateTime LastDetected { get; set; }
        public string? Response { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is DetectedPort other)
            {
                return Name == other.Name && Type == other.Type;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Type);
        }

        public override string ToString()
        {
            return Type switch
            {
                CommunicationType.COM_PORT => $"{Name} ({BaudRate} baud) - {(IsConnected ? "Connected" : "Disconnected")}",
                CommunicationType.USR_N520 => $"{Name} {IpAddress}:{Port} - {(IsConnected ? "Connected" : "Disconnected")}",
                _ => Name
            };
        }
    }
}