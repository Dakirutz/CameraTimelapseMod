using CameraTimelapseMod.Util;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Colossal.IO.AssetDatabase;
using Game.Assets;
using Game.CinematicCamera;
using Game.Rendering;
using Game.UI.InGame;
using Unity.Entities;
using UnityEngine;

namespace CameraTimelapseMod.Systems
{

    public static class CinematicSystem
    {

        private static bool _cancelRequested = false;

        public static IEnumerator PlayAndRecordCoroutine(CinematicCameraAsset asset)
        {
            if (asset == null) yield break;

            if (!Mod.IsInGame)
            {
                UITools.ShowError("You must load a city before recording a cinematic.");
                yield break;
            }

            yield return ObsClientSystem.ConnectIfEnabledCoroutine();
            if (ObsClientSystem.LastConnectFailed)
            {
                UITools.ShowMessage(
                    "OBS connection failed",
                    "Could not connect to OBS Studio.\n\n" +
                    "Cinematic recording aborted.\n\n" +
                    "Checklist:\n" +
                    "- OBS Studio is running\n" +
                    "- Tools → WebSocket Server Settings → Enable WebSocket Server\n" +
                    "- Port and password match your settings");
                yield break;
            }

            string outputDir = Path.Combine(
                Tools.getVideosFolder(),
                "Cinematics",
                Tools.Sanitize(GameTools.GetCurrentCityName()),
                Tools.Sanitize(asset.name ?? "unnamed"));

            yield return PlayAndRecordCoroutineToDir(asset, outputDir);
        }

        public static IEnumerator PlayAndRecordCoroutineToDir(
    CinematicCameraAsset asset, string outputDir)
        {
            if (asset == null) yield break;

            try { asset.Load(); }
            catch (Exception ex)
            {
                LogsTools.Error($"Cinematic asset load failed: {ex.Message}");
                yield break;
            }
            if (asset.target == null)
            {
                LogsTools.Warn($"Cinematic '{asset.name}' has no target sequence");
                yield break;
            }
            float duration = asset.target.playbackDuration;
            if (duration < 0.5f)
            {
                LogsTools.Warn($"Cinematic '{asset.name}': duration too short ({duration}s)");
                yield break;
            }
            var world = World.DefaultGameObjectInjectionWorld;
            var uiSystem = world?.GetExistingSystemManaged<CinematicCameraUISystem>();
            var photoMode = world?.GetExistingSystemManaged<PhotoModeRenderSystem>();
            if (uiSystem == null || photoMode == null)
            {
                LogsTools.Error("Cinematic systems not available");
                yield break;
            }

            _cancelRequested = false;
            IsCinematicPlaying = true;

            bool obsActive = ObsClientSystem.IsConnected
                && (Mod.Setting?.VideoRecordingEnabled ?? false);
            bool uiWas = false;
            bool recordingStarted = false;


            // --- Masquage overlays / icônes ---
            var renderingSys = world.GetExistingSystemManaged<Game.Rendering.RenderingSystem>();
            bool prevHideOverlay = false;
            bool prevMarkersVisible = true;
            bool overlaysOverridden = false;
            if (renderingSys != null)
            {
                prevHideOverlay = renderingSys.hideOverlay;
                prevMarkersVisible = renderingSys.markersVisible;
                renderingSys.hideOverlay = true;
                renderingSys.markersVisible = false;
                overlaysOverridden = true;
            }

            LogsTools.Info($"Cinematic '{asset.name}' starting ({duration:F1}s) → {outputDir}");
            CameraTools.EnterCinematicMode();

            try
            {
                yield return new WaitForSeconds(0.5f);
                uiWas = UITools.SetUIVisible(false);

                if (obsActive)
                {
                    try { Directory.CreateDirectory(outputDir); } catch { }

                    var dirTask = ObsClientSystem.SetRecordDirectory(outputDir);
                    yield return new WaitUntil(() => dirTask.IsCompleted);
                    if (!dirTask.Result)
                        LogsTools.Warn($"OBS SetRecordDirectory failed for: {outputDir}");
                    else
                        LogsTools.Info($"OBS record dir set to: {outputDir}");

                    yield return new WaitForSeconds(0.3f);

                    var startTask = ObsClientSystem.StartRecording();
                    yield return new WaitUntil(() => startTask.IsCompleted);
                    if (!startTask.Result)
                        LogsTools.Warn("OBS StartRecord failed for cinematic");
                    else
                        recordingStarted = true;
                }

                uiSystem.Autoplay(asset);

                const float SAFETY_MARGIN = 1.0f;  

                float elapsed = 0f;
                while (elapsed < duration + SAFETY_MARGIN && !_cancelRequested)
                {
                    // Force le masquage à chaque frame, au cas où le cinematic player réactiverait les overlays pendant la lecture
                    if (overlaysOverridden && renderingSys != null)
                    {
                        renderingSys.hideOverlay = true;
                        renderingSys.markersVisible = false;
                    }
                    yield return null;
                    elapsed += UnityEngine.Time.unscaledDeltaTime;
                }
                if (_cancelRequested)
                    LogsTools.Info($"Cinematic '{asset.name}' cancelled by user");

                uiSystem.StopAutoplay();

                if (recordingStarted)
                {
                    var stopTask = ObsClientSystem.StopRecording();
                    yield return new WaitUntil(() => stopTask.IsCompleted);
                }

                yield return new WaitForSeconds(0.3f);
            }
            finally
            {
                if (overlaysOverridden && renderingSys != null)
                {
                    try
                    {
                        renderingSys.hideOverlay = prevHideOverlay;
                        renderingSys.markersVisible = prevMarkersVisible;
                    }
                    catch { }
                }
                if (uiWas) { try { UITools.SetUIVisible(true); } catch { } }
                try { CameraTools.ExitCinematicMode(); } catch { }
                IsCinematicPlaying = false;
                _cancelRequested = false;
                LogsTools.Info($"Cinematic '{asset.name}' done");
            }
        }

        public static void RequestCancel()
        {
            _cancelRequested = true;
            LogsTools.Info("Cinematic cancel requested");
        }

        public static bool IsCinematicPlaying { get; private set; } = false;

        public static void PlayAndRecordByName(string cinematicName)
        {
            CoroutineSystem.Instance.StartCoroutine(
                PlayAndRecordByNameCoroutine(cinematicName));
        }

        public static IEnumerator PlayAndRecordByNameCoroutine(string cinematicName)
        {
            if (!Mod.IsInGame)
            {
                UITools.ShowError("You must load a city before recording a cinematic.");
                yield break;
            }

            var asset = CameraTools.FindCinematicByName(cinematicName);
            if (asset == null)
            {
                LogsTools.Warn($"Cinematic '{cinematicName}' not found");
                UITools.ShowError(
                    $"Cinematic '{cinematicName}' not found.\n\n" +
                    "Saved cinematics are in CS2 Photo Mode → Cinematic Camera. " +
                    "Save one first, then call this with its exact name.");
                yield break;
            }

            yield return PlayAndRecordCoroutine(asset);
        }

        public static IEnumerator PlayAndRecordConfiguredCoroutine()
        {
            var assets = SettingsTools.GetConfiguredCinematics();
            if (assets.Count == 0) yield break;

            bool obsWasConnected = ObsClientSystem.IsConnected;
            if (!obsWasConnected)
            {
                yield return ObsClientSystem.ConnectIfEnabledCoroutine();
                if (ObsClientSystem.LastConnectFailed)
                {
                    LogsTools.Warn("Cinematics skipped: OBS connection failed");
                    yield break;
                }
            }

            foreach (var asset in assets)
            {
                yield return PlayAndRecordCoroutine(asset);
                yield return new WaitForSeconds(1f);
            }

            if (!obsWasConnected)
            {
                yield return ObsClientSystem.DisconnectCoroutine();
            }
        }

        public static IEnumerator PlayConfiguredCinematicsToBaseDir(
            string baseDir,
            System.Func<bool> shouldContinue = null)
        {
            var assets = SettingsTools.GetConfiguredCinematics();
            if (assets.Count == 0) yield break;

            foreach (var asset in assets)
            {
                if (shouldContinue != null && !shouldContinue()) yield break;

                string videoDir = Path.Combine(baseDir, Tools.Sanitize(asset.name));
                try { Directory.CreateDirectory(videoDir); } catch { }

                yield return PlayAndRecordCoroutineToDir(asset, videoDir);
                yield return new UnityEngine.WaitForSeconds(1f);
            }
        }

        public static void RecordSelectedCinematicsNow()
        {
            if (!Mod.IsInGame)
            {
                UITools.ShowError("You must load a city first.");
                return;
            }

            var assets = SettingsTools.GetConfiguredCinematics();
            if (assets.Count == 0)
            {
                UITools.ShowError(
                    "No valid cinematics in the list.\n\n" +
                    "Type cinematic names separated by commas in the 'Cinematics to record' field, " +
                    "then click 'List available cinematics' to verify the names.");
                return;
            }

            string warning =
                $"This will play and record {assets.Count} cinematic(s) via OBS:\n  - " +
                string.Join("\n  - ", assets.ConvertAll(a => a.name)) +
                "\n\nMake sure OBS is running with WebSocket enabled.\n\n" +
                "DO NOT touch keyboard or mouse during playback.";

            UITools.ShowConfirm(
                "RECORD CINEMATICS",
                warning,
                () => CoroutineSystem.Instance.StartCoroutine(PlayAndRecordConfiguredCoroutine()));
        }
    }
}