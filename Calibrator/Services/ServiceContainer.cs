using System;

namespace Calibrator.Services
{
    public static class ServiceContainer
    {
        private static WelderService? _welderService;

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

        public static void Dispose()
        {
            _welderService = null;
        }
    }
}