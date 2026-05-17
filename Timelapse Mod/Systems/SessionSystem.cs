using CameraTimelapseMod.Data;
using CameraTimelapseMod.Util;
using Colossal.Serialization.Entities;
using Game;
using Game.Input;
using Game.SceneFlow;
using Game.Zones;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CameraTimelapseMod.Systems
{


    public partial class SessionSystem : Game.GameSystemBase
    {
        private SessionState _state = new SessionState();
        private int _settleFrames;
        private bool _captureInFlight;
        private float _reminderTimer;
        private static string _pendingAbortMessage = null; 
       
        private bool _settleStarted = false;

        private List<SaveTools.SaveEntry> _saveQueue = new List<SaveTools.SaveEntry>();
        private List<float> _timeQueue = new List<float>();
        private static void ShowError(string message) => UITools.ShowError(message);

        private static bool _reqStartCurrent, _reqStartAll, _reqStop, _reqResume;
        private bool _pendingResume = false;
        public static void RequestStartCurrent() => _reqStartCurrent = true;
        public static void RequestStartAll() => _reqStartAll = true;
        public static void RequestStop() => _reqStop = true;

        private bool _videoRecordingTriggered = false;
        private bool _videoRecordingDone = false; 
        private bool _cinematicsTriggered = false;
        private bool _cinematicsDone = false;

        private ProxyAction _stopAction;
        private ProxyAction _pauseResumeAction;


        public class ProgressSnapshot
        {
            public bool IsActive;
            public bool IsPaused;
            public int SaveIdx;
            public int SaveTotal;
            public int ViewIdx;
            public int ViewTotal;
            public int TimeIdx;
            public int TimeTotal;
            public float CurrentTime;
            public string CurrentSave;
            public string Phase;
            public int EtaSeconds;
            public int CompletedScreenshots;
        }

        private static SessionSystem _instance;

        protected override void OnCreate()
        {
            base.OnCreate();
            _instance = this;
            CaptureSystem.OnCaptureFinished += () =>
            {
                _captureInFlight = false;
                _state.CompletedScreenshots++;
                PersistState();
            };

            _stopAction = Mod.Setting.GetAction(Setting.StopSessionActionName);
            _pauseResumeAction = Mod.Setting.GetAction(Setting.PauseResumeActionName);
            _stopAction.shouldBeEnabled = true;
            _pauseResumeAction.shouldBeEnabled = true;

            LoadStateFromDisk();
            LogsTools.Info("SessionSystem ready");
        }

        public static ProgressSnapshot GetProgressSnapshot()
        {
            if (_instance == null) return null;
            return _instance.BuildSnapshot();
        }

        private ProgressSnapshot BuildSnapshot()
        {
            if (_state.Phase == SessionPhase.Idle)
                return new ProgressSnapshot { IsActive = false };

            return new ProgressSnapshot
            {
                IsActive = true,
                IsPaused = _state.IsPaused,
                SaveIdx = _state.IsAllSavesMode
                    ? (_state.CurrentSaveProcessed ? _state.SaveIdx + 2 : 1)
                    : 1,
                SaveTotal = _state.TotalExpectedSaves,
                ViewIdx = _state.ViewIdx + 1,
                ViewTotal = PresetsSystem.Presets.Count,
                TimeIdx = _state.ModeIdx + 1,
                TimeTotal = _timeQueue.Count,
                CurrentTime = CurrentCaptureTime(),
                CurrentSave = _state.CurrentSaveName ?? "",
                Phase = _state.Phase.ToString(),
                EtaSeconds = ComputeEta(),
                CompletedScreenshots = _state.CompletedScreenshots
            };
        }

        private int ComputeEta()
        {
            if (_state.CompletedScreenshots <= 0 || _state.ElapsedSeconds <= 0f)
                return -1; 

            float avgPerScreenshot = _state.ElapsedSeconds / _state.CompletedScreenshots;
            int remaining = TotalScreenshotsRemaining();
            return Mathf.RoundToInt(remaining * avgPerScreenshot);
        }

        private int TotalScreenshotsRemaining()
        {
            int views = PresetsSystem.Presets.Count;
            int times = _timeQueue.Count;
            if (views <= 0 || times <= 0) return 0;

            int perSave = views * times;
            int totalForAllSaves = perSave * _state.TotalExpectedSaves;
            int alreadyDone = _state.CompletedScreenshots;
            return Mathf.Max(0, totalForAllSaves - alreadyDone);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
        protected override void OnGameLoadingComplete(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            if (mode == GameMode.Game && _state.Phase == SessionPhase.LoadingSave)
            {
                _settleFrames = 60;
                Transition(SessionPhase.WaitingForWorldSettle);
                return;
            }

            if (mode == GameMode.MainMenu && _pendingResume)
            {
                _pendingResume = false;
                LogsTools.Info("Main menu loaded, scheduling resume in 2s...");
                CoroutineSystem.Instance.StartCoroutine(TriggerResumeAfterDelay(5f));
            }

            if (mode == GameMode.MainMenu && !string.IsNullOrEmpty(_pendingAbortMessage))
            {
                UITools.ShowError(_pendingAbortMessage);
                _pendingAbortMessage = null;
            }
        }


        private System.Collections.IEnumerator TriggerResumeAfterDelay(float seconds)
        {
            yield return new UnityEngine.WaitForSeconds(seconds);
            _reqResume = true;
            LogsTools.Info("Resume request issued");
        }


        protected override void OnUpdate()
        {
            HandleHotkeys();
            HandleRequests();
            HandleReminder();

            if (_state.Phase != SessionPhase.Idle && !_state.IsPaused)
            {
                _state.ElapsedSeconds += UnityEngine.Time.unscaledDeltaTime;
            }

            if (_state.Phase == SessionPhase.Idle) return;
            if (_captureInFlight) return;
            if (_state.IsPaused) return;

            // Ne pas avancer dans les phases pendant les transitions de save
            // (Mod.IsInGame est false pendant que le World se reconfigure)
            if (!Mod.IsInGame &&
                _state.Phase != SessionPhase.LoadingSave &&
                _state.Phase != SessionPhase.WaitingForWorldSettle)
            {
                return;
            }


            switch (_state.Phase)
            {

                case SessionPhase.WaitingForWorldSettle:
                    if (!_settleStarted)
                    {
                        _settleStarted = true;
                        CoroutineSystem.Instance.StartCoroutine(SettleAndContinue());
                    }
                    break;

                case SessionPhase.PreviewConstructionAndWait:
                    HandlePreviewConstructionAndWait();
                    break;

                case SessionPhase.SetupTimeWeather:
                    {
                        float captureTime = CurrentCaptureTime();
                        bool presetHasPhotoMode = false;
                        if (_state.ViewIdx < PresetsSystem.Presets.Count)
                        {
                            var p = PresetsSystem.Presets[_state.ViewIdx];
                            presetHasPhotoMode = p.PhotoModeProperties != null && p.PhotoModeProperties.Count > 0;
                        }

                        bool forceClear = SettingsTools.ShouldForceWeather(presetHasPhotoMode);

                        if (!float.IsNaN(captureTime))
                        {
                            GameTools.ApplyTimeAndWeather(captureTime, forceClear);
                        }
                        else if (forceClear)
                        {
                            GameTools.ApplyClearWeather();
                        }

                        _settleFrames = 30;
                        Transition(SessionPhase.ApplyView);
                        break;
                    }

                case SessionPhase.CaptureFrame:
                    if (_settleFrames-- > 0) break;

                    if ((Mod.Setting?.VideoRecordingEnabled ?? false)
                        && (Mod.Setting?.VideoRecordSeconds ?? 0) > 0
                        && Systems.ObsClientSystem.IsConnected
                        && !_videoRecordingDone)
                    {
                        if (!_videoRecordingTriggered)
                        {
                            _videoRecordingTriggered = true;
                            CoroutineSystem.Instance.StartCoroutine(RecordVideoBeforeCapture());
                        }
                        break; 
                    }

                    DoCapture();
                    _videoRecordingTriggered = false;
                    _videoRecordingDone = false;
                    Transition(SessionPhase.AdvanceView);
                    break;

                case SessionPhase.ApplyView:
                    if (_settleFrames-- > 0) break;
                    if (_state.ViewIdx >= PresetsSystem.Presets.Count)
                    { Transition(SessionPhase.AdvanceMode); break; }

                    var preset = PresetsSystem.Presets[_state.ViewIdx];
                    bool useCaptureTimes = !float.IsNaN(CurrentCaptureTime());

                    if (useCaptureTimes)
                        PresetsSystem.ApplyIgnoreTimeOfDay(preset);
                    else
                        PresetsSystem.Apply(preset);

                    // Photo Mode presets need more time to settle (DoF, exposure, etc.)
                    bool presetHasPhotoMode2 = preset.PhotoModeProperties != null
                                           && preset.PhotoModeProperties.Count > 0;
                    _settleFrames = presetHasPhotoMode2 ? 75 : 10;
                    Transition(SessionPhase.CaptureFrame);
                    break;

                case SessionPhase.AdvanceView:
                    _state.ViewIdx++;
                    Transition(SessionPhase.ApplyView);
                    break;

                case SessionPhase.AdvanceMode:
                    if (_state.ModeIdx + 1 >= _timeQueue.Count)
                    {
                        Transition(SessionPhase.PlayCinematicsForHour);
                    }
                    else
                    {
                        AdvanceModeAfterCinematics();
                    }
                    break;

                case SessionPhase.PlayCinematicsForHour:
                    if (string.IsNullOrEmpty(Mod.Setting?.CinematicsToRecord))
                    {
                        AdvanceModeAfterCinematics();
                        break; 
                    }

                    if (!Systems.ObsClientSystem.IsConnected)
                    {
                        UITools.ShowError(
                            "OBS not connected or video recording not enabled.\n\n" +
                            "Cinematics are configured but cannot be recorded — aborting session.");
                        LogsTools.Warn("Session aborted mid-run: OBS not connected, cinematics configured");
                        _reqStop = true;
                        break;
                    }

                    if (!_cinematicsDone)
                    {
                        if (!_cinematicsTriggered)
                        {
                            _cinematicsTriggered = true;
                            CoroutineSystem.Instance.StartCoroutine(PlayCinematicsForCurrentHour());
                        }
                        break; 
                    }

                    _cinematicsTriggered = false;
                    _cinematicsDone = false;
                    AdvanceModeAfterCinematics();
                    break;

                case SessionPhase.AdvanceSave:
                    AdvanceSave();
                    break;

                case SessionPhase.Done:
                    FinishSession();
                    break;
            }
        }

        private System.Collections.IEnumerator SettleAndContinue()
        {
            yield return new UnityEngine.WaitForSeconds(5f);
            _settleStarted = false;
            Transition(SessionPhase.PreviewConstructionAndWait);
        }

        private void AdvanceModeAfterCinematics()
        {
            _state.ModeIdx++;
            _state.ViewIdx = 0;
            if (_state.ModeIdx >= _timeQueue.Count)
            {
                SettingsTools.cartoExportIfRequired();

                if (!_state.IsAllSavesMode)
                {
                    _state.SuccessfulSaves++;
                    Transition(SessionPhase.Done);
                }
                else
                {
                    Transition(SessionPhase.AdvanceSave);
                }
            }
            else
            {
                Transition(SessionPhase.SetupTimeWeather);
            }
        }

        private bool _previewApplied = false;
        private bool _waitInPlayStarted = false;
        private float _waitInPlayUntil = 0f;
        private bool _wasPausedBeforeWait = true;
        private System.Collections.IEnumerator RecordVideoBeforeCapture()
        {
            int durationSec = Mod.Setting?.VideoRecordSeconds ?? 5;
            if (durationSec <= 0) { _videoRecordingDone = true; yield break; }

            bool wasPaused = false;
            bool speedChanged = false;
            bool started = false;
            bool uiHidden = false;
            bool hideUI = Mod.Setting?.HideUIInScreenshots ?? true;

            // Variables d'overlay déclarées AU NIVEAU EXTERNE pour être visibles dans le finally
            Game.Rendering.RenderingSystem renderingSys = null;
            bool prevHideOverlay = false;
            bool prevMarkersVisible = true;
            bool overlaysOverridden = false;

            try
            {
                var simSpeedSetting = Mod.Setting?.VideoSimulationSpeed ?? Setting.SimulationSpeed.Normal_x2;

                string videoFolder = BuildOutputFolder("Videos");
                Directory.CreateDirectory(videoFolder);

                var dirTask = Systems.ObsClientSystem.SetRecordDirectory(videoFolder);
                yield return new UnityEngine.WaitUntil(() => dirTask.IsCompleted);
                if (!dirTask.Result)
                    LogsTools.Warn($"OBS SetRecordDirectory failed for: {videoFolder}");

                yield return new UnityEngine.WaitForSeconds(0.3f);

                wasPaused = GameTools.IsSimulationPaused();
                GameTools.SetSimulationSpeed((int)simSpeedSetting);
                speedChanged = true;

                if (hideUI)
                {
                    uiHidden = UITools.SetUIVisible(false);

                    renderingSys = Unity.Entities.World.DefaultGameObjectInjectionWorld
                        ?.GetExistingSystemManaged<Game.Rendering.RenderingSystem>();
                    if (renderingSys != null)
                    {
                        prevHideOverlay = renderingSys.hideOverlay;
                        prevMarkersVisible = renderingSys.markersVisible;
                        renderingSys.hideOverlay = true;
                        renderingSys.markersVisible = false;
                        overlaysOverridden = true;
                    }
                }

                var startTask = Systems.ObsClientSystem.StartRecording();
                yield return new UnityEngine.WaitUntil(() => startTask.IsCompleted);
                started = startTask.Result;

                if (started)
                {
                    yield return new UnityEngine.WaitForSeconds(durationSec);

                    var stopTask = Systems.ObsClientSystem.StopRecording();
                    yield return new UnityEngine.WaitUntil(() => stopTask.IsCompleted);
                }
            }
            finally
            {
                // Restauration des overlays AVEC LES VRAIES VALEURS d'origine
                if (overlaysOverridden && renderingSys != null)
                {
                    try
                    {
                        renderingSys.hideOverlay = prevHideOverlay;
                        renderingSys.markersVisible = prevMarkersVisible;
                    }
                    catch { }
                }

                // Restauration de l'UI
                if (uiHidden)
                {
                    try { UITools.SetUIVisible(true); } catch { }
                }

                // Restauration de la pause de simulation
                if (speedChanged) GameTools.SetSimulationPaused(wasPaused);

                _videoRecordingDone = true;
            }
        }

        private System.Collections.IEnumerator PlayCinematicsForCurrentHour()
        {
            try
            {
                var assets = SettingsTools.GetConfiguredCinematics();
                if (assets.Count == 0) yield break;

                string baseDir = Path.Combine(
                    Tools.getVideosFolder(),
                    Tools.Sanitize(GameTools.GetCurrentCityName()),
                    SessionFolderName);

                yield return Systems.CinematicSystem.PlayConfiguredCinematicsToBaseDir(baseDir);
            }
            finally
            {
                _cinematicsDone = true;
            }
        }

        private void HandlePreviewConstructionAndWait()
        {
            bool wantPreview = Mod.Setting?.SavesModPreviewConstruction ?? false;
            int wantWaitSec = Mod.Setting?.SavesModPlayWaitSeconds ?? 0;

            if (wantPreview && !_previewApplied)
            {
                int n = Mod.Setting?.SavesModRoadsToDeletePerClickInt ?? 1;
                GameTools.PreviewRecentAsConstruction(n);
                _previewApplied = true;
                LogsTools.Info($"AllSaves: marked {n} recent edges' areas as construction");
            }

            if (wantWaitSec > 0)
            {
                if (!_waitInPlayStarted)
                {
                    _wasPausedBeforeWait = GameTools.IsSimulationPaused();
                    GameTools.SetSimulationPaused(false);
                    _waitInPlayUntil = UnityEngine.Time.unscaledTime + wantWaitSec;
                    _waitInPlayStarted = true;
                    LogsTools.Info($"AllSaves: running simulation for {wantWaitSec}s in play mode");
                    return;  
                }

                if (UnityEngine.Time.unscaledTime < _waitInPlayUntil) return;

                GameTools.SetSimulationPaused(_wasPausedBeforeWait);
                LogsTools.Info("AllSaves: wait finished, restored pause state");
            }

            _previewApplied = false;
            _waitInPlayStarted = false;

            Transition(SessionPhase.SetupTimeWeather);
        }

        private void HandleHotkeys()
        {
            if (_stopAction != null && _stopAction.WasPressedThisFrame())
            {
                if (Systems.CinematicSystem.IsCinematicPlaying)
                {
                    Systems.CinematicSystem.RequestCancel();
                    LogsTools.Info("Stop hotkey: cinematic cancel requested");
                }

                if (_state.Phase != SessionPhase.Idle)
                {
                    _reqStop = true;
                    LogsTools.Info("Stop hotkey: session stop requested");
                }

                if (AutoTimelapseSessionSystem.IsRunning)
                {
                    AutoTimelapseSessionSystem.CancelSession();
                    LogsTools.Info("Stop hotkey: auto timelapse cancel requested");
                }
            }

            if (_pauseResumeAction != null && _pauseResumeAction.WasPressedThisFrame())
            {
                if (_state.Phase != SessionPhase.Idle)
                {
                    if (_state.IsPaused)
                    {
                        _reqResumeFromPause = true;
                        LogsTools.Info("Pause/Resume hotkey: session resume requested");
                    }
                    else
                    {
                        _reqPause = true;
                        LogsTools.Info("Pause/Resume hotkey: session pause requested");
                    }
                }

                if (AutoTimelapseSessionSystem.IsRunning)
                {
                    if (AutoTimelapseSessionSystem.IsPaused)
                    {
                        AutoTimelapseSessionSystem.RequestResume();
                        LogsTools.Info("Pause/Resume hotkey: auto timelapse resume requested");
                    }
                    else
                    {
                        AutoTimelapseSessionSystem.RequestPause();
                        LogsTools.Info("Pause/Resume hotkey: auto timelapse pause requested");
                    }
                }
            }
        }

        private static bool _reqPause, _reqResumeFromPause;
        public static void RequestPause() => _reqPause = true;
        public static void RequestResume() => _reqResumeFromPause = true;

        

        private void HandleRequests()
        {
            if (_reqStop)
            {
                LogsTools.Info($"_reqStop processing: phase was {_state.Phase}, calling AbortSession");
                _reqStop = false;
                AbortSession();
            }
            if (_reqStartCurrent) { _reqStartCurrent = false; StartSessionCurrentSave(); }
            if (_reqStartAll) { _reqStartAll = false; StartSessionAllSaves(); }
            if (_reqResume) { _reqResume = false; ResumeSession(); }
            if (_reqPause)
            {
                LogsTools.Info($"_reqPause processing: setting IsPaused=true on phase {_state.Phase}");
                _reqPause = false;
                _state.IsPaused = true;
                PersistState();
            }
            if (_reqResumeFromPause) { _reqResumeFromPause = false; _state.IsPaused = false; PersistState(); LogsTools.Info("Session resumed"); }
        }

        private void HandleReminder()
        {
            if (Mod.Setting == null || Mod.Setting.ReminderMinutes <= 0) return;
            if (_state.Phase != SessionPhase.Idle) return;

            _reminderTimer += UnityEngine.Time.deltaTime;
            if (_reminderTimer >= Mod.Setting.ReminderMinutes * 60f)
            {
                _reminderTimer = 0f;
                UITools.ShowMessage($"TimeLapse Reminder, {Mod.Setting.ReminderMinutes} min elapsed");
                LogsTools.Info($"Reminder: {Mod.Setting.ReminderMinutes} min elapsed");
            }
        }

        private void StartSessionCurrentSave()
        {
            if (!Tools.isInGameAndHasPresetOrCinematic()) return;

            _saveQueue = new List<SaveTools.SaveEntry>();
            _timeQueue = BuildTimeQueue();
            _state = new SessionState
            {
                Phase = SessionPhase.SetupTimeWeather,
                CurrentSaveName = "current",
                IsAllSavesMode = false,
                TotalExpectedSaves = 1,
                SuccessfulSaves = 0,
                CaptureTimesQueue = Mod.Setting.CaptureTimes ?? ""   
            };

            BackupPresetsToSessionFolder();
            _settleFrames = 30;
            PersistState();
            UITools.CloseGameMenu();
            Systems.UISystem.RequestClosePanel();
            if (Mod.Setting?.VideoRecordingEnabled ?? false)
            {
                CoroutineSystem.Instance.StartCoroutine(ConnectObsThenCheck());
            }
            LogsTools.Info("Session started (current save)");
        }


        private void StartSessionAllSaves()
        {


            string warning =
                "If you activated the marking of recent buildings as under-construction, this will modify your saves (without resaving them)" +
                "Screenshots will be taken from all your camera presets for each saves.\n\n" +
                "WARNING:\n" +
                "- Some mods or asset may corrupt old saves when they are open, if a save is broken after you run this mod it's because of such problem.\n" +
                "- Thus, be sure to COPY your ENTIRE saves folder before using this mod, so you do not loose anything in case of problem.\n" +
                "- If you have an auto save function, please disable it to avoid problems.\n" +
                "- DO NOT save the game after this runs, just reload your save to continue playing.\n\n";

            UITools.ShowConfirm("SAVES SCREENSHOTS TIMELAPSE", warning, StartSessionAllSavesBis);

        }


        private void StartSessionAllSavesBis()
        {
            if (!Tools.isInGameAndHasPresetOrCinematic()) return;

            SettingsTools.StartCrashWatchdogIfRequired();

            var filterResult = SaveTools.BuildQueueFromSettings();
            _saveQueue = filterResult.Queue;

            LogsTools.Info(
                $"Save filtering: {filterResult.TotalBeforeFilter} → " +
                $"prefix:{filterResult.AfterPrefix} → city:{filterResult.AfterCity} → " +
                $"skip:{filterResult.AfterSkip} → max:{filterResult.AfterMax}");

            if (!string.IsNullOrEmpty(filterResult.ResumeError))
            {
                LogsTools.Warn(filterResult.ResumeError);
                ShowError(filterResult.ResumeError);
                return;
            }

            if (_saveQueue.Count == 0)
            {
                LogsTools.Warn("No saves matched filter");
                ShowError("No saves matched your filters. Adjust them and retry.");
                return;
            }

            int startIdx = filterResult.StartIdx;
            bool currentSaveProcessed = filterResult.HasResume;

            if (currentSaveProcessed)
            {
                LogsTools.Info($"Resuming from save '{_saveQueue[startIdx].Name}' at index {startIdx}");
            }

            _timeQueue = BuildTimeQueue();

            SessionPhase initialPhase = currentSaveProcessed
                ? SessionPhase.LoadingSave
                : SessionPhase.SetupTimeWeather;

            _state = new SessionState
            {
                Phase = initialPhase,
                CurrentSaveName = currentSaveProcessed ? "" : "current",
                IsAllSavesMode = true,
                CurrentSaveProcessed = currentSaveProcessed,
                SaveIdx = startIdx,
                TotalExpectedSaves = currentSaveProcessed
                    ? (_saveQueue.Count - startIdx)
                    : (_saveQueue.Count + 1),
                SuccessfulSaves = 0,
                SessionStartDate = DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                CaptureTimesQueue = Mod.Setting.CaptureTimes ?? ""
            };
            BackupPresetsToSessionFolder();
            _settleFrames = 30;
            PersistState();
            UITools.CloseGameMenu();
            Systems.UISystem.RequestClosePanel();

            if (Mod.Setting?.VideoRecordingEnabled ?? false)
            {
                CoroutineSystem.Instance.StartCoroutine(ConnectObsThenCheck());
            }

            if (currentSaveProcessed)
            {
                LoadCurrentSave();
                LogsTools.Info($"Session resumed from save #{startIdx} '{_saveQueue[startIdx].Name}', " +
                                $"{_saveQueue.Count - startIdx} save(s) to process");
            }
            else
            {
                LogsTools.Info($"Session started: current save first, then {_saveQueue.Count} more save(s)");
            }

            Mod.Setting.ResumeFromSaveName = "";
            Mod.Setting.ApplyAndSave();
        }

        private System.Collections.IEnumerator ConnectObsThenCheck()
        {
            yield return Systems.ObsClientSystem.ConnectIfEnabledCoroutine();

            if ((Mod.Setting?.VideoRecordingEnabled ?? false)
                && Systems.ObsClientSystem.LastConnectFailed)
            {
                LogsTools.Warn("Session aborted: OBS connection failed");
                UITools.ShowMessage(
                    "OBS connection failed",
                    "Could not connect to OBS Studio.\n\n" +
                    "Session aborted.\n\n" +
                    "Checklist:\n" +
                    "- OBS Studio is running\n" +
                    "- Tools → WebSocket Server Settings → Enable WebSocket Server\n" +
                    "- Port and password match your settings");
                _reqStop = true;
                yield break;
            }

            var cinematics = SettingsTools.GetConfiguredCinematics();
            if (cinematics.Count > 0 && !Systems.ObsClientSystem.IsConnected)
            {
                LogsTools.Warn($"Session aborted: {cinematics.Count} cinematic(s) configured but OBS not available");
                UITools.ShowMessage(
                    "Cinematics cannot be recorded",
                    $"You have {cinematics.Count} cinematic(s) configured to record, but OBS is not connected " +
                    "(video recording disabled or OBS unavailable).\n\n" +
                    "Session aborted.\n\n" +
                    "Either remove the cinematics from the 'Cinematics to record' field, " +
                    "or enable video recording and verify OBS is reachable.");
                _reqStop = true;
            }
        }

        private void ResumeSession()
        {
            if (_state.Phase == SessionPhase.Idle) return;
            LogsTools.Info($"Resuming from save #{_state.SaveIdx} '{_state.CurrentSaveName}'");

            var filterResult = SaveTools.BuildQueueFromSettings(ignoreResumeFromSaveName: true);
            _saveQueue = filterResult.Queue;

            _timeQueue = Tools.ParseTimes(_state.CaptureTimesQueue);
            if (_timeQueue.Count == 0) _timeQueue.Add(12f);

            if (!string.IsNullOrEmpty(_state.CurrentSaveName) && _state.CurrentSaveName != "current")
            {
                int byName = _saveQueue.FindIndex(e =>
                    string.Equals(e.Name, _state.CurrentSaveName, StringComparison.OrdinalIgnoreCase));
                if (byName >= 0 && byName != _state.SaveIdx)
                {
                    LogsTools.Info($"SaveIdx adjusted from {_state.SaveIdx} to {byName} (name match)");
                    _state.SaveIdx = byName;
                }
                else if (byName < 0)
                {
                    LogsTools.Warn($"Save '{_state.CurrentSaveName}' not found in filtered queue, using SaveIdx={_state.SaveIdx} blindly");
                }
            }

            if (_state.SaveIdx >= _saveQueue.Count) { FinishSession(); return; }

            _state.Phase = SessionPhase.LoadingSave;
            PersistState();
            if (Mod.Setting?.VideoRecordingEnabled ?? false)
            {
                CoroutineSystem.Instance.StartCoroutine(ConnectObsThenCheck());
            }

            LoadCurrentSave();
        }

        private void ResetTransientFlags()
        {
            _videoRecordingTriggered = false;
            _videoRecordingDone = false;
            _cinematicsTriggered = false;
            _cinematicsDone = false;
            _captureInFlight = false;
            _previewApplied = false;
            _waitInPlayStarted = false;
        }


        private void AbortSession()
        {
            Tools.StopCrashWatchdog();

            if (Systems.ObsClientSystem.IsConnected)
            {
                CoroutineSystem.Instance.StartCoroutine(
                    Systems.ObsClientSystem.DisconnectCoroutine());
            }
            GameTools.RestoreWeather();
            CameraTools.ExitCinematicMode();

            int doneScreenshots = _state.CompletedScreenshots;
            int successful = _state.SuccessfulSaves;
            int total = _state.TotalExpectedSaves;
            bool wasAllSavesMode = _state.IsAllSavesMode; 

            LogsTools.Info($"Session aborted ({doneScreenshots} screenshot(s) saved before stop)");

            _state = new SessionState();
            ClearStateFile();
            ResetTransientFlags();

            UITools.ShowMessage(
                "Session stopped",
                $"Session stopped by user.\n\n" +
                $"Saves processed: {successful}/{total}\n" +
                $"{doneScreenshots} screenshot(s) were saved before stop.\n\n" +
                "You can find them in the screenshots folder." +
                (wasAllSavesMode ? "\n\nDO NOT save the game." : ""));
        }

        private void FinishSession()
        {
            ResetTransientFlags();
            Tools.StopCrashWatchdog();

            if (Systems.ObsClientSystem.IsConnected)
            {
                CoroutineSystem.Instance.StartCoroutine(
                    Systems.ObsClientSystem.DisconnectCoroutine());
            }

            GameTools.RestoreWeather();
            bool allSucceeded = _state.SuccessfulSaves >= _state.TotalExpectedSaves
                                && _state.TotalExpectedSaves > 0;
            bool wasAllSavesMode = _state.IsAllSavesMode;

            int doneScreenshots = _state.CompletedScreenshots;
            int successful = _state.SuccessfulSaves;
            int total = _state.TotalExpectedSaves;

            LogsTools.Info($"Session complete. " +
                            $"Successful: {_state.SuccessfulSaves}/{_state.TotalExpectedSaves}");

            _state = new SessionState();
            ClearStateFile();

            CameraTools.ExitCinematicMode();

            UITools.ShowMessage(
                "Session complete",
                $"Session finished successfully.\n\n" +
                $"Saves processed: {successful}/{total}\n" +
                $"Screenshots saved: {doneScreenshots}\n\n" +
                (wasAllSavesMode ? "DO NOT save the game." : ""));



            if (!wasAllSavesMode)
            {
                LogsTools.Info("Single-save session, no shutdown.");
                return;
            }

            if (!allSucceeded)
            {
                LogsTools.Warn("Some saves did not process successfully. Skipping shutdown.");
                return;
            }

            SettingsTools.ShutDownOrExitIfRequired();
        }

        private void AdvanceSave()
        {
            _state.SuccessfulSaves++;
            _state.ModeIdx = 0;
            _state.ViewIdx = 0;
            _state.SavesProcessedSinceRestart++;
            _state.ResumeAttempts = 0;

            if (_state.IsAllSavesMode && !_state.CurrentSaveProcessed)
            {
                _state.CurrentSaveProcessed = true;

                if (_saveQueue.Count == 0) { Transition(SessionPhase.Done); return; }

                int n = Mod.Setting.RestartGameEveryNSaves;
                if (n > 0 && _state.SavesProcessedSinceRestart >= n)
                {
                    LogsTools.Info($"Reached {n} saves, restarting game");
                    _state.SavesProcessedSinceRestart = 0;
                    _state.Phase = SessionPhase.LoadingSave;
                    PersistState();
                    Tools.RestartGame();
                    return;
                }

                _state.Phase = SessionPhase.LoadingSave;
                PersistState();
                CoroutineSystem.Instance.StartCoroutine(WaitThenLoadCurrentSave());
                return;
            }

            _state.SaveIdx++;
            if (_state.SaveIdx >= _saveQueue.Count) { Transition(SessionPhase.Done); return; }

            int restartN = Mod.Setting.RestartGameEveryNSaves;
            if (restartN > 0 && _state.SavesProcessedSinceRestart >= restartN)
            {
                LogsTools.Info($"Reached {restartN} saves, restarting game");
                _state.SavesProcessedSinceRestart = 0;
                _state.Phase = SessionPhase.LoadingSave;
                PersistState();
                Tools.RestartGame();
                return;
            }

            _state.Phase = SessionPhase.LoadingSave;
            PersistState();
            CoroutineSystem.Instance.StartCoroutine(WaitThenLoadCurrentSave());
        }
        private System.Collections.IEnumerator WaitThenLoadCurrentSave()
        {
            //without cleanup, if we start to delete building it may crash. If we just put construction like now,
            //it may works but didnt try, because in all case better to cleanup.

            if (Mod.Setting?.ReturnToMenuBetweenSaves ?? false)
            {
                // Force un retour au menu principal avec cleanup explicite
                LogsTools.Info("Returning to main menu with cleanup before loading next save");
                var task = Game.SceneFlow.GameManager.instance.MainMenu();
                while (!task.IsCompleted)
                    yield return null;

                if (!task.Result)
                    LogsTools.Warn("MainMenu() returned false, loading save anyway");

                yield return new UnityEngine.WaitForSeconds(1f);
            }
            else
            {
                // Comportement par défaut : juste laisser le pipeline ECS finir
                yield return new UnityEngine.WaitForSeconds(2f);
            }

            LoadCurrentSave();
        }

        private void LoadCurrentSave()
        {
            if (_state.SaveIdx >= _saveQueue.Count) { Transition(SessionPhase.Done); return; }
            var entry = _saveQueue[_state.SaveIdx];
            _state.CurrentSaveName = entry.Name;
            PersistState();

            try
            {
                var load = typeof(GameManager).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "Load")
                    .FirstOrDefault(m =>
                    {
                        var ps = m.GetParameters();
                        return ps.Length == 3
                            && ps[0].ParameterType == typeof(GameMode)
                            && ps[1].ParameterType == typeof(Purpose);
                    });

                if (load == null)
                {
                    LogsTools.Error("GameManager.Load(GameMode, Purpose, asset) not found");
                    AdvanceSave();
                    return;
                }

                load.Invoke(GameManager.instance,
                    new[] { GameMode.Game, Purpose.LoadGame, entry.RawAsset });
                LogsTools.Info($"Loading save: {entry.Name}");
            }
            catch (Exception ex)
            {
                LogsTools.Error($"Load {entry.Name} failed: {ex}");
                AdvanceSave();
            }
        }

        private void DoCapture()
        {
            string folder = BuildOutputFolder("Screenshots");
            Directory.CreateDirectory(folder);
            string fullPath = Path.Combine(folder, BuildFilename() + ".png");
            _captureInFlight = true;
            CaptureSystem.CaptureNow(fullPath);
        }


        private string BuildFilename()
        {
            string date = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string save = string.IsNullOrEmpty(_state.CurrentSaveName) ? "save" : _state.CurrentSaveName;
            return $"{Tools.Sanitize(save)}_{date}";
        }

        private string SessionFolderName => _state.IsAllSavesMode ? _state.SessionStartDate : "current";

        private string BuildOutputFolder(string kind)
        {
            string city = Tools.Sanitize(GameTools.GetCurrentCityName());
            string viewName = _state.ViewIdx < PresetsSystem.Presets.Count
                ? PresetsSystem.Presets[_state.ViewIdx].Name
                : $"view{_state.ViewIdx}";
            string timeLabel = CurrentTimeLabel();

            string baseFolder = kind == "Videos"
                ? Tools.getVideosFolder()
                : Tools.getScreenshotFolder();

            return Path.Combine(
                baseFolder,
                city,
                SessionFolderName,
                Tools.Sanitize(viewName),
                timeLabel);
        }

        private List<float> BuildTimeQueue()
        {
            var times = Tools.ParseTimes(Mod.Setting.CaptureTimes);

            if (times.Count == 0)
            {
                LogsTools.Info("CaptureTimes empty → using each preset's stored Time of Day");
                times.Add(float.NaN);  
            }

            return times;
        }

        private float CurrentCaptureTime()
            => _state.ModeIdx < _timeQueue.Count ? _timeQueue[_state.ModeIdx] : 12f;

        private string CurrentTimeLabel() => Tools.FormatHourLabel(CurrentCaptureTime());

        private void Transition(SessionPhase next)
        {
            _state.Phase = next;
            PersistState();
        }

        private void PersistState()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Mod.SessionStatePath));
                File.WriteAllText(Mod.SessionStatePath, JsonUtility.ToJson(_state, true));
            }
            catch (Exception ex) { LogsTools.Warn($"PersistState: {ex.Message}"); }
        }

        private const int MaxResumeAttempts = 2;

        private void LoadStateFromDisk()
        {
            try
            {
                if (!File.Exists(Mod.SessionStatePath)) return;

                _state = JsonUtility.FromJson<SessionState>(File.ReadAllText(Mod.SessionStatePath))
                         ?? new SessionState();

                if (_state.Phase == SessionPhase.Idle || _state.Phase == SessionPhase.Done)
                    return;


                _state.ResumeAttempts++;

                if (_state.ResumeAttempts > MaxResumeAttempts)
                {
                    LogsTools.Warn($"Session resume aborted after {_state.ResumeAttempts - 1} " +
                                 $"failed attempts. Discarding session state.");
                    ClearStateFile();
                    _state = new SessionState();
                    _pendingAbortMessage = "Session of timelapse aborted, the game kept crashing, see logs please. Thank you.";

                    return;
                }

                if (Mod.Setting?.AutoRestartOnCrash ?? false)
                {
                    Tools.StartCrashWatchdog();
                }


                LogsTools.Info($"Resuming unfinished session " +
                             $"(attempt {_state.ResumeAttempts}/{MaxResumeAttempts}, " +
                             $"phase={_state.Phase})");
                PersistState();      
                _pendingResume = true;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"LoadState: {ex.Message}");
            }
        }

        private void ClearStateFile()
        {
            try { if (File.Exists(Mod.SessionStatePath)) File.Delete(Mod.SessionStatePath); }
            catch { }
        }
        private void BackupPresetsToSessionFolder()
        {
            try
            {
                string presetsSource = PresetsSystem.PresetsPath;
                if (!File.Exists(presetsSource))
                {
                    LogsTools.Warn($"No presets file found to backup at {presetsSource}");
                    return;
                }


                string sessionDir = Path.Combine(
                    Tools.getScreenshotFolder(),
                    Tools.Sanitize(GameTools.GetCurrentCityName()),
                    SessionFolderName);

                Directory.CreateDirectory(sessionDir);

                string backupPath = Path.Combine(sessionDir, "_presets_backup.json");
                File.Copy(presetsSource, backupPath, overwrite: true);

                LogsTools.Info($"Presets backed up to: {backupPath}");
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"BackupPresetsToSessionFolder failed: {ex.Message}");
            }
        }

    }
}