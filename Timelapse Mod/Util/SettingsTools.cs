using CameraTimelapseMod;
using Game.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game.SceneFlow;
using CameraTimelapseMod.Util;
using System;
using System.Collections;
using System.IO;
using Unity.Entities;
using UnityEngine;

namespace CameraTimelapseMod.Util
{
    internal class SettingsTools
    {
        public static (int width, int height, bool useScreenCapture) GetScreenshotQualityTarget()
        {
            var quality = Mod.Setting?.Quality ?? Setting.CaptureQuality.ScreenResolution;
            switch (quality)
            {
                case Setting.CaptureQuality.FullHD_1920x1080: return (1920, 1080, false);
                case Setting.CaptureQuality.QHD_2560x1440: return (2560, 1440, false);
                case Setting.CaptureQuality.UHD_4K_3840x2160: return (3840, 2160, false);
                case Setting.CaptureQuality.UHD_8K_7680x4320: return (7680, 4320, false);
                case Setting.CaptureQuality.ScreenResolution:
                default:
                    return (Screen.width, Screen.height, true);
            }
        }

        public static System.Collections.Generic.List<CinematicCameraAsset> GetConfiguredCinematics()
        {
            var result = new System.Collections.Generic.List<CinematicCameraAsset>();
            string raw = Mod.Setting?.CinematicsToRecord ?? "";
            if (string.IsNullOrEmpty(raw)) return result;

            foreach (var part in raw.Split(','))
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var asset = CameraTools.FindCinematicByName(trimmed);
                if (asset != null) result.Add(asset);
                else LogsTools.Warn($"Cinematic '{trimmed}' not found, skipping");
            }
            return result;
        }
        public static bool ShouldForceWeather(bool presetHasPhotoMode)
        {
            var mode = Mod.Setting?.ForceClearWeatherMode ?? Setting.ClearWeatherMode.AlwaysForce;
            if (mode == Setting.ClearWeatherMode.Off) return false;
            if (mode == Setting.ClearWeatherMode.AlwaysForce) return true;
            return !presetHasPhotoMode;   // ForceExceptCamModes
        }

        public static void StartCrashWatchdogIfRequired()
        {
            if (Mod.Setting.AutoRestartOnCrash)
            {
                Tools.StartCrashWatchdog();
            }
        }

        public static void cartoExportIfRequired()
        {
            if (Mod.Setting?.TriggerCartoExport ?? false)
            {
                CartoTools.TriggerExport();
            }
        }

        public static void ShutDownOrExitIfRequired()
        {

            switch (Mod.Setting?.ShutdownAfterSession ?? Setting.ShutdownMode.None)
            {
                case Setting.ShutdownMode.ExitGame:
                    LogsTools.Info("Auto timelapse done, exiting game...");
                    UnityEngine.Application.Quit();
                    break;

                case Setting.ShutdownMode.ShutdownComputer:
                    LogsTools.Info("Auto timelapse done, shutting down computer in 30s...");
                    Tools.ShutdownComputer();
                    break;
            }
        }
    }
}
