using CameraTimelapseMod.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using UnityEngine;
using static CameraTimelapseMod.Util.GameTools;

namespace CameraTimelapseMod.Systems
{
    public static class AutoTimelapseSessionSystem
    {

        private static bool _isPaused = false;
        private static string _videoFolder = null;
        public static bool IsPaused => _isPaused;

        public static void RequestPause() { _isPaused = true; LogsTools.Info("Auto timelapse paused"); }
        public static void RequestResume() { _isPaused = false; LogsTools.Info("Auto timelapse resumed"); }

        private static bool _isRunning = false;
        private static int _currentStep = 0;
        private static int _totalEdgesProcessed = 0;
        private static string _sessionFolder = null;
        public static bool IsRunning => _isRunning;
        public static int CurrentStep => _currentStep;
        public static int TotalEdgesProcessed => _totalEdgesProcessed;
        public static string SessionFolder => _sessionFolder;
        public static int LastKnownEdgeCount { get; private set; } = 0;
        public static string CurrentPhase { get; private set; } = "";

        public static int InitialEdgeCount { get; private set; } = 0;
        public static int EstimatedTotalSteps { get; private set; } = 0;
        public static string VideoFolder => _videoFolder;

        private static System.Collections.IEnumerator WaitWhilePaused()
        {
            while (_isPaused && _isRunning)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }

        


        public static void RequestStartFromSettings()
        {

            if (!Tools.isInGameAndHasPresetOrCinematic()) return;

            if (_isRunning)
            {
                UITools.ShowError("Auto timelapse is already running.");
                return;
            }

            string warning =
                "This will gradually destroy your city to create a reverse timelapse.\n" +
                "Screenshots will be taken from all your camera presets at each step.\n\n" +
                "WARNING:\n" +
                "- This DESTROYS your current city, please save before.\n" +
                "- Make sure you also saved a copy (save two times with two different names).\n" +
                "- DO NOT save this game after this runs, just reload your save to continue playing.\n\n"               ;

            UITools.ShowConfirm("AUTO HISTORIC TIMELAPSE", warning, StartSession);
        }

        private static void StartSession()
        {
            try
            {
                UITools.CloseGameMenu();
                Systems.UISystem.RequestClosePanel();
                _isRunning = true;
                _currentStep = 0;
                _totalEdgesProcessed = 0;
                CurrentPhase = "Initializing";

                InitialEdgeCount = GameTools.CountRemainingEdges();
                int destroyCount = Mod.Setting?.AutoModRoadsToDeletePerClickInt ?? 1;
                if (destroyCount < 1) destroyCount = 1;
                if (destroyCount > 5000) destroyCount = 5000;
                EstimatedTotalSteps = Mathf.Max(1, Mathf.CeilToInt((float)InitialEdgeCount / destroyCount));

                string cityName = Tools.Sanitize(GameTools.GetCurrentCityName() ?? "AutoTimelapse");
                string sessionDate = DateTime.Now.ToString("yyyyMMdd-HHmmss");

                _sessionFolder = Path.Combine(Tools.getScreenshotFolder(), cityName, sessionDate);
                _videoFolder = Path.Combine(Tools.getVideosFolder(), cityName, sessionDate);
                Directory.CreateDirectory(_sessionFolder);
                Directory.CreateDirectory(_videoFolder);

                LogsTools.Info($"AutoTimelapse session started, folder = {_sessionFolder}");

                CoroutineSystem.Instance.StartCoroutine(SessionCoroutine());
            }
            catch (Exception ex)
            {
                LogsTools.Error($"StartSession failed: {ex}");
                _isRunning = false;
                CurrentPhase = "";
            }
        }

        public static void CancelSession()
        {
            LogsTools.Info("AutoTimelapse session cancelled by user");
            _isRunning = false;
            _isPaused = false;
        }

        private static IEnumerator SessionCoroutine()
        {
            int destroyCount = Mod.Setting?.AutoModRoadsToDeletePerClickInt ?? 1;
            int secondsBetween = Mod.Setting?.AutoModPlayWaitSeconds ?? 3;
            bool wantConstruction = Mod.Setting?.AutoModPreviewConstruction ?? false;
            if (secondsBetween < 1) secondsBetween = 1;

            int constructCount = wantConstruction ? destroyCount : 0;

            yield return ObsClientSystem.ConnectIfEnabledCoroutine();

            if ((Mod.Setting?.VideoRecordingEnabled ?? false) && ObsClientSystem.LastConnectFailed)
            {
                LogsTools.Warn("Auto timelapse aborted: OBS connection failed");
                UITools.ShowMessage(
                    "OBS connection failed",
                    "Could not connect to OBS Studio.\n\n" +
                    "Auto timelapse session aborted.\n\n" +
                    "Checklist:\n" +
                    "- OBS Studio is running\n" +
                    "- Tools → WebSocket Server Settings → Enable WebSocket Server\n" +
                    "- Port and password match your settings");

                _isRunning = false;
                _isPaused = false;
                _sessionFolder = null;
                _currentStep = 0;
                CurrentPhase = "";
                yield break;
            }

            var cinematics = SettingsTools.GetConfiguredCinematics();
            if (cinematics.Count > 0 && !ObsClientSystem.IsConnected)
            {
                LogsTools.Warn($"Auto timelapse aborted: {cinematics.Count} cinematic(s) configured but OBS not available");
                UITools.ShowMessage(
                    "Cinematics cannot be recorded",
                    $"You have {cinematics.Count} cinematic(s) configured to record, but OBS is not connected " +
                    "(video recording disabled or OBS unavailable).\n\n" +
                    "Session aborted before any city modification.\n\n" +
                    "Either remove the cinematics from the 'Cinematics to record' field, " +
                    "or enable video recording and verify OBS is reachable.");

                _isRunning = false;
                _isPaused = false;
                _sessionFolder = null;
                _currentStep = 0;
                CurrentPhase = "";
                yield break;
            }

            CurrentPhase = "Initial capture";
            yield return TakeScreenshotsForStep(0);

            while (_isRunning)
            {
                yield return WaitWhilePaused();   
                if (!_isRunning) break;

                _currentStep++;


                LastKnownEdgeCount = GameTools.CountRemainingEdges();
                if (LastKnownEdgeCount == 0)
                {
                    LogsTools.Info($"AutoTimelapse: no edges left, ending at step {_currentStep}");
                    break;
                }

                CurrentPhase = $"Step {_currentStep}: destroying {destroyCount}, marking {constructCount}";
                LogsTools.Info(
                    $"AutoTimelapse step {_currentStep}: " +
                    $"destroying {destroyCount}, construction {constructCount}, " +
                    $"edges left = {LastKnownEdgeCount}");

                int processed = StepTimelapse(destroyCount, constructCount);
                _totalEdgesProcessed += processed;

                yield return WaitWhilePaused();


                CurrentPhase = $"Step {_currentStep}: simulation playing for {secondsBetween}s";
                bool wasPaused = GameTools.IsSimulationPaused();
                GameTools.SetSimulationPaused(false);   

                float waited = 0f;
                while (_isRunning && waited < secondsBetween)
                {
                    yield return new WaitForSeconds(1f);
                    waited += 1f;
                }

                yield return WaitWhilePaused();

                GameTools.SetSimulationPaused(true);
                yield return new WaitForSeconds(0.5f);  



                yield return WaitWhilePaused();
                if (!_isRunning) break;

                CurrentPhase = $"Step {_currentStep}: capturing presets";
                yield return TakeScreenshotsForStep(_currentStep);


                SettingsTools.cartoExportIfRequired();

                GameTools.SetSimulationPaused(wasPaused);

                if (!_isRunning) break;
            }

            yield return ObsClientSystem.DisconnectCoroutine();

            CurrentPhase = "Finished";
            LogsTools.Info(
                $"AutoTimelapse session finished after {_currentStep} steps " +
                $"({_totalEdgesProcessed} edges processed)");

            string displayPath = (_sessionFolder ?? "").Replace('\\', '/');
            UITools.ShowMessage("Auto Historic Timelapse",
                $"Auto Historic Timelapse finished after {_currentStep} steps.\n" +
                $"Screenshots saved to:\n{displayPath}\n\n" +
                "DO NOT save the game.");


            SettingsTools.ShutDownOrExitIfRequired();

            CameraTools.ExitCinematicMode();

            _isRunning = false;
            _sessionFolder = null;
            _currentStep = 0;
            CurrentPhase = "";
        }



        public static int StepTimelapse(int edgesToDestroy, int edgesToMarkAsConstruction)
        {
            try
            {
                if (edgesToDestroy < 0) edgesToDestroy = 0;
                if (edgesToMarkAsConstruction < 0) edgesToMarkAsConstruction = 0;

                int totalNeeded = edgesToDestroy + edgesToMarkAsConstruction;
                if (totalNeeded == 0)
                {
                    LogsTools.Warn("StepTimelapse: nothing to do");
                    return 0;
                }

                var recent = GameTools.GetMostRecentEdges(totalNeeded);
                if (recent.Countable.Count == 0 && recent.Bonus.Count == 0)
                {
                    LogsTools.Warn("StepTimelapse: no edges found in city");
                    return 0;
                }

                // les comptables sont déjà triés du plus récent au plus ancien
                var toDestroy = recent.Countable.GetRange(
                    0, Math.Min(edgesToDestroy, recent.Countable.Count));

                var toConstruct = recent.Countable.GetRange(
                    toDestroy.Count,
                    Math.Min(edgesToMarkAsConstruction, recent.Countable.Count - toDestroy.Count));

                // les bonus (tunnels etc.) partent avec les destructions
                var allToDestroy = new List<Entity>(toDestroy);
                allToDestroy.AddRange(recent.Bonus);

                if (allToDestroy.Count > 0)
                    CameraTools.MoveCameraToEdge(allToDestroy[0]);

                LogsTools.Info(
                    $"StepTimelapse: destroying {toDestroy.Count} countable + {recent.Bonus.Count} bonus, " +
                    $"marking {toConstruct.Count} as construction");

                GameTools.MarkBuildingsAroundEdgesAsConstruction(toConstruct,true);
                GameTools.DestroyEdgesAndAllAdjacentBuildings(allToDestroy);

                return toDestroy.Count;
            }
            catch (Exception ex)
            {
                LogsTools.Error($"StepTimelapse failed: {ex}");
                return 0;
            }
        }

        private static IEnumerator TakeScreenshotsForStep(int stepNumber)
        {
            var presets = PresetsSystem.Presets;
            bool hasPresets = presets != null && presets.Count > 0;

            var times = Tools.ParseTimes(Mod.Setting?.CaptureTimes ?? "");
            bool useCaptureTimes = times.Count > 0;
            if (!useCaptureTimes)
            {
                times = new List<float> { float.NaN };
            }

            bool hideUI = Mod.Setting?.HideUIInScreenshots ?? true;

            foreach (float hour in times)
            {
                if (!_isRunning) yield break;
                yield return WaitWhilePaused();

                string hourLabel = Tools.FormatHourLabel(hour);

                if (hasPresets)
                {
                    for (int i = 0; i < presets.Count; i++)
                    {
                        if (!_isRunning) yield break;
                        yield return WaitWhilePaused();

                        var preset = presets[i];
                        bool presetHasPhotoMode = preset.PhotoModeProperties != null && preset.PhotoModeProperties.Count > 0;
                        bool forceClear = SettingsTools.ShouldForceWeather(presetHasPhotoMode);

                        if (!float.IsNaN(hour))
                        {
                            GameTools.ApplyTimeAndWeather(hour, forceClear);
                        }
                        else if (forceClear)
                        {
                            GameTools.ApplyClearWeather();
                        }
                        yield return new WaitForSeconds(0.5f);

                        if (useCaptureTimes)
                            PresetsSystem.ApplyIgnoreTimeOfDay(preset);
                        else
                            PresetsSystem.Apply(preset);


                        // Photo Mode presets need more time to settle (DoF, exposure, etc.)
                        float settleSeconds = presetHasPhotoMode ? 1.5f : 0.5f;
                        yield return new WaitForSeconds(settleSeconds);


                        string presetSafeName = Tools.Sanitize(preset.Name ?? $"preset{i}");

                        string videoTargetDir = Path.Combine(_videoFolder, presetSafeName, hourLabel);

                        bool wantVideo = (Mod.Setting?.VideoRecordingEnabled ?? false)
                            && (Mod.Setting?.VideoRecordSeconds ?? 0) > 0
                            && ObsClientSystem.IsConnected;

                        bool uiWasVisible = false;
                        if (hideUI && wantVideo)
                            uiWasVisible = UITools.SetUIVisible(false);

                        if (wantVideo)
                            yield return ObsClientSystem.RecordWithSimulationCoroutine(videoTargetDir);

                        if (hideUI && uiWasVisible)
                            UITools.SetUIVisible(true);

                        GameTools.SetSimulationPaused(true);
                        yield return new WaitForSeconds(0.5f);

                        string targetDir = Path.Combine(_sessionFolder, presetSafeName, hourLabel);
                        Directory.CreateDirectory(targetDir);
                        string fileName = $"autostep{stepNumber:D4}.png";
                        string fullPath = Path.Combine(targetDir, fileName);


                        yield return CaptureSystem.CaptureNowAndWait(fullPath);
                    }
                }
            }

            if (_isRunning)
            {
                yield return PlayCinematicsForStep(stepNumber);
            }
        }
        private static IEnumerator PlayCinematicsForStep(int stepNumber)
        {
            var assets = SettingsTools.GetConfiguredCinematics();
            if (assets.Count == 0) yield break;

            if (!ObsClientSystem.IsConnected)
            {
                //we need a warning otherwise user may not understand why nothing happen if he forgot to turn on video or to open obs
                UITools.ShowError(
                    "OBS not connected or video recording not enabled.\n\n" +
                    "Cinematics are configured but cannot be recorded, aborting auto timelapse session.\n\n" +
                    "Either disable cinematics by emptying the cinematic field under video settings of the mod, or enable video recording and check OBS settings, then restart the session.");
                LogsTools.Warn($"Auto timelapse aborted at step {stepNumber}: OBS not connected, {assets.Count} cinematic(s) configured");
                CancelSession();
                yield break;
            }

            LogsTools.Info($"Playing {assets.Count} cinematic(s) for step {stepNumber}");
            yield return CinematicSystem.PlayConfiguredCinematicsToBaseDir(
                _videoFolder, () => _isRunning);
        }


    }
}