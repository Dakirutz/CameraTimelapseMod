using System;
using System.Reflection;

namespace CameraTimelapseMod.Util
{
    public static class CartoTools
    {
        private static bool _initialized = false;
        private static MethodInfo _exportMethod = null;
        private static bool _available = false;

        public static bool IsAvailable
        {
            get
            {
                if (!_initialized) Initialize();
                return _available;
            }
        }

        private static void Initialize()
        {
            _initialized = true;
            try
            {
                Type ioType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    ioType = asm.GetType("Carto.IO.IO");
                    if (ioType != null) break;
                }

                if (ioType == null)
                {
                    LogsTools.Info("Carto mod not detected, map export disabled");
                    return;
                }

                _exportMethod = ioType.GetMethod("Export",
                    BindingFlags.Public | BindingFlags.Static);

                if (_exportMethod == null)
                {
                    LogsTools.Warn("Carto.IO.IO found but Export() method not present");
                    return;
                }

                _available = true;
                LogsTools.Info("Carto integration ready");
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"Carto detection failed: {ex.Message}");
            }
        }

        public static bool TriggerExport()
        {
            if (!IsAvailable){
                LogsTools.Info("Carto not available");
                return false;
            }

            try
            {
                _exportMethod.Invoke(null, null);
                LogsTools.Info("Carto export triggered");
                return true;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"Carto export failed: {ex.Message}");
                return false;
            }
        }
    }
}