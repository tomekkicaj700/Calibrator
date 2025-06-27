using System;
using System.Threading.Tasks;
using static Logger.LoggerService;

namespace Calibrator.Services
{
    public static class ServiceContainer
    {
        private static WelderService? _welderService;
        private static ConfigService? _configService;
        private static bool _isInitialized = false;

        public static ConfigService ConfigService
        {
            get
            {
                if (_configService == null)
                {
                    _configService = ConfigService.Instance;
                }
                return _configService;
            }
        }

        public static WelderService WelderService
        {
            get
            {
                if (_welderService == null)
                {
                    _welderService = new WelderService();
                }
                return _welderService;
            }
        }

        /// <summary>
        /// Initialize all services
        /// </summary>
        public static async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                Log("Initializing ServiceContainer...");

                // Initialize ConfigService first
                await ConfigService.InitializeAsync();

                // Initialize WelderService (it will use ConfigService)
                _ = WelderService; // Force initialization

                _isInitialized = true;
                Log("ServiceContainer initialized successfully");
            }
            catch (Exception ex)
            {
                Log($"Error initializing ServiceContainer: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Check if services are initialized
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        public static void Dispose()
        {
            _welderService = null;
            _configService = null;
            _isInitialized = false;
        }
    }
}