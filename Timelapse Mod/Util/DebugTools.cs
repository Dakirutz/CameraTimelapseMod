using CameraTimelapseMod.Data;
using CameraTimelapseMod.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using static Game.Rendering.Debug.RenderPrefabRenderer;

namespace CameraTimelapseMod.Systems
{
    public static class DebugTools
    {
        public static void Run(Setting.DebugAction action)
        {
            LogsTools.Info($"[DEBUG] Running action: {action}");
            try
            {
                switch (action)
                {
                    case Setting.DebugAction.None:
                        UITools.ShowMessage("Debug", "No action selected.");
                        return;

                    // OBS
                    case Setting.DebugAction.Obs_RecordTest5s:
                        CoroutineSystem.Instance.StartCoroutine(DebugObsRecord5s());
                        break;
                    case Setting.DebugAction.Obs_SetTestRecordDir:
                        CoroutineSystem.Instance.StartCoroutine(DebugObsSetDir());
                        break;

                    // Cinematics
                    case Setting.DebugAction.Cin_ListAvailable:
                        CameraTools.ShowAvailableCinematics();
                        break;

                    case Setting.DebugAction.Cin_PlayFirstConfigured:
                        var assets = SettingsTools.GetConfiguredCinematics();
                        if (assets.Count == 0) { UITools.ShowError("No configured cinematics."); break; }
                        CoroutineSystem.Instance.StartCoroutine(
                            CinematicSystem.PlayAndRecordCoroutine(assets[0]));
                        break;

                    // Camera
                    case Setting.DebugAction.Cam_ApplyFirstPreset:
                        if (PresetsSystem.Presets.Count == 0) { UITools.ShowError("No presets."); break; }
                        PresetsSystem.Apply(PresetsSystem.Presets[0]);
                        break;

                    case Setting.DebugAction.Cam_DumpPhotoProperties:
                        DumpPhotoProperties();
                        break;

                    // Time / weather
                    case Setting.DebugAction.Tw_SetTime12:
                        GameTools.ApplyTimeAndWeather(12f, false);
                        break;
                    case Setting.DebugAction.Tw_SetTime22:
                        GameTools.ApplyTimeAndWeather(22f, false);
                        break;
                    case Setting.DebugAction.Tw_ClearWeatherOnly:
                        GameTools.ApplyClearWeather();
                        break;
                    case Setting.DebugAction.Tw_Restore:
                        GameTools.RestoreWeather();
                        break;

                    case Setting.DebugAction.Auto_ListDistricts:
                        DebugListDistricts();
                        break;

                    case Setting.DebugAction.Auto_CountEdgesPerDistrict:
                        GameTools.DebugCountEdgesPerDistrict();
                        break;

                    case Setting.DebugAction.Auto_CountEdges:
                        {
                            var em = GameTools.GetEntityManager();
                            if (em == null) { UITools.ShowError("No world loaded."); break; }

                            var query = em.Value.CreateEntityQuery(GameTools.GetVisibleEdgesQueryDesc());
                            try
                            {
                                int count = query.CalculateEntityCount();
                                UITools.ShowMessage("Edge count", $"Visible edges: {count}");
                            }
                            finally
                            {
                                query.Dispose();
                            }
                            break;
                        }
                    case Setting.DebugAction.Auto_Destroy1Road:
                        AutoTimelapseSessionSystem.StepTimelapse(1, 0);
                        break;
                    case Setting.DebugAction.Auto_Destroy5Roads:
                        AutoTimelapseSessionSystem.StepTimelapse(5, 0);
                        break;
                    case Setting.DebugAction.Auto_MarkConstruction10:
                        GameTools.PreviewRecentAsConstruction(10);
                        break;

                    // Saves
                    case Setting.DebugAction.Saves_ListFiltered:
                        DebugListSaves();
                        break;

                    // Carto
                    case Setting.DebugAction.Carto_CheckAvailable:
                        UITools.ShowMessage("Carto",
                            $"Available: {CartoTools.IsAvailable}");
                        break;
                    case Setting.DebugAction.Carto_TriggerExport:
                        bool ok = CartoTools.TriggerExport();
                        UITools.ShowMessage("Carto",
                            ok ? "Export triggered." : "Failed (Carto not installed?)");
                        break;




                    case Setting.DebugAction.Life_RestartGame:
                        UITools.ShowConfirm(
                            "Debug: restart game",
                            "This will save the current session state and restart Cities Skylines II " +
                            "via a .bat script. The session should resume automatically once the game " +
                            "is back. Continue?",
                            () => Tools.RestartGame());
                        break;

                    case Setting.DebugAction.Life_StartWatchdog:
                        Tools.StartCrashWatchdog();
                        UITools.ShowMessage(
                            "Debug",
                            "Crash watchdog started. A window should appear showing the watchdog status.\n\n" +
                            "If you crash the game now, it will be relaunched automatically.\n\n" +
                            "Run 'Lifecycle: stop crash watchdog' or finish a session normally to stop it. " +
                            "(For now you can also manually delete the marker file in the mod folder.)");
                        break;

                    case Setting.DebugAction.Life_StopWatchdog:
                        Tools.StopCrashWatchdog();
                        UITools.ShowMessage(
                            "Debug",
                            "Crash watchdog stopped. Marker file removed.\n\n" +
                            "The watchdog window should close itself within a few seconds.");
                        break;

                    case Setting.DebugAction.Life_QuitGame:
                        UITools.ShowConfirm(
                            "Debug: quit game",
                            "This will close Cities Skylines II immediately, without saving anything. " +
                            "Use this to test that quit-after-session works.\n\nContinue?",
                            () => UnityEngine.Application.Quit());
                        break;

                    case Setting.DebugAction.Cap_ScreenshotNow:
                        DebugScreenshot();
                        break;

                    case Setting.DebugAction.Auto_MoveCameraToRecent:
                        DebugMoveCameraToRecent();
                        break;

                    default:
                        UITools.ShowError($"Debug action {action} not implemented.");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogsTools.Error($"[DEBUG] Action {action} failed: {ex}");
                UITools.ShowError($"Debug action failed: {ex.Message}");
            }
        }


        private static void DebugListDistricts()
        {
            var em = World.DefaultGameObjectInjectionWorld?.EntityManager;
            if (em == null) { UITools.ShowError("No world loaded."); return; }

            var districts = GameTools.GetAllDistricts(em.Value);
            if (districts.Count == 0)
            {
                UITools.ShowMessage(
                    "Districts",
                    "No districts found in this city.\n\n" +
                    "Create some via the in-game District tool to use this filter.");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Found {districts.Count} district(s):\n");
            foreach (var (entity, name) in districts)
            {
                string display = string.IsNullOrEmpty(name) ? "(unnamed)" : name;
                sb.AppendLine($"  - {display}  (entity #{entity.Index})");
            }
            sb.AppendLine();
            sb.AppendLine("Type one of these names (or part of it) into the " +
                          "'Restrict to district (Auto mode)' setting to filter the auto timelapse.");

            LogsTools.Info($"[DEBUG] Districts: {string.Join(", ", districts.ConvertAll(d => d.Name))}");
            UITools.ShowMessage("Districts", sb.ToString());
        }

        private static void DebugMoveCameraToRecent()
        {
            var em = World.DefaultGameObjectInjectionWorld?.EntityManager;
            if (em == null)
            {
                UITools.ShowError("No world loaded.");
                return;
            }

            var recent = GameTools.GetMostRecentEdges(1);

            // priorité aux comptables, fallback sur les bonus si aucune route visible
            Entity target = recent.Countable.Count > 0
                ? recent.Countable[0]
                : (recent.Bonus.Count > 0 ? recent.Bonus[0] : Entity.Null);

            if (target == Entity.Null)
            {
                UITools.ShowError("No edges found in city.");
                return;
            }

            bool ok = CameraTools.MoveCameraToEdge(target);
            if (ok)
            {
                LogsTools.Info($"[DEBUG] Camera moved to edge {target.Index}");
                UITools.ShowMessage(
                    "Debug",
                    $"Camera moved to most recent edge (entity #{target.Index}).");
            }
            else
            {
                UITools.ShowError("Failed to move camera. See log.");
            }
        }

        private static void DebugScreenshot()
        {
            try
            {
                string folder = System.IO.Path.Combine(Mod.DataDir, "DebugScreenshots");
                System.IO.Directory.CreateDirectory(folder);
                string fileName = $"debug_{DateTime.Now:yyyyMMdd-HHmmss}.png";
                string fullPath = System.IO.Path.Combine(folder, fileName);

                CaptureSystem.CaptureNow(fullPath);
                LogsTools.Info($"[DEBUG] Screenshot saved: {fullPath}");
                UITools.ShowMessage(
                    "Debug",
                    $"Screenshot capture triggered.\n\nFile: {fileName}\n\n" +
                    $"Folder: {folder}");
            }
            catch (Exception ex)
            {
                LogsTools.Error($"[DEBUG] Screenshot failed: {ex}");
                UITools.ShowError($"Screenshot failed: {ex.Message}");
            }
        }

        private static System.Collections.IEnumerator WrapTask(System.Threading.Tasks.Task task)
        {
            yield return new UnityEngine.WaitUntil(() => task.IsCompleted);
        }

        private static System.Collections.IEnumerator DebugObsRecord5s()
        {
            yield return ObsClientSystem.ConnectIfEnabledCoroutine();
            if (!ObsClientSystem.IsConnected) { UITools.ShowError("OBS not connected or video recording option not enabled in settings."); yield break; }
            var s = ObsClientSystem.StartRecording();
            yield return new UnityEngine.WaitUntil(() => s.IsCompleted);
            yield return new UnityEngine.WaitForSeconds(5f);
            var stop = ObsClientSystem.StopRecording();
            yield return new UnityEngine.WaitUntil(() => stop.IsCompleted);
            UITools.ShowMessage("Debug",
                $"Record test: start={s.Result}, stop={stop.Result}");
        }

        private static System.Collections.IEnumerator DebugObsSetDir()
        {
            yield return ObsClientSystem.ConnectIfEnabledCoroutine();
            if (!ObsClientSystem.IsConnected) { UITools.ShowError("OBS not connected."); yield break; }
            string testDir = System.IO.Path.Combine(Mod.DataDir, "ObsTest");
            System.IO.Directory.CreateDirectory(testDir);
            var t = ObsClientSystem.SetRecordDirectory(testDir);
            yield return new UnityEngine.WaitUntil(() => t.IsCompleted);
            UITools.ShowMessage("Debug",
                t.Result ? $"OK: {testDir}" : "FAILED — see log for OBS error code.");
        }

        private static void DumpPhotoProperties()
        {
            var photoMode = World.DefaultGameObjectInjectionWorld
                ?.GetExistingSystemManaged<Game.Rendering.PhotoModeRenderSystem>();
            if (photoMode?.photoModeProperties == null)
            {
                UITools.ShowError("Photo mode not initialized.");
                return;
            }

            int n = 0;
            foreach (var kv in photoMode.photoModeProperties)
            {
                LogsTools.Info($"[DEBUG] Photo property: id='{kv.Value?.id}', group='{kv.Value?.group}'");
                n++;
            }
            UITools.ShowMessage("Debug", $"Dumped {n} photo properties to log.");
        }

        private static void DebugListSaves()
        {
            try
            {
                var r = SaveTools.BuildQueueFromSettings();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Total saves found: {r.TotalBeforeFilter}");
                sb.AppendLine($"After Prefix: {r.AfterPrefix}");
                sb.AppendLine($"After CityNameFilter: {r.AfterCity}");
                sb.AppendLine($"After SkipBetweenSaves: {r.AfterSkip}");
                sb.AppendLine($"After MaxSaves: {r.AfterMax}");

                if (r.HasResume)
                    sb.AppendLine($"\nResume: starting at index {r.StartIdx} ('{r.Queue[r.StartIdx].Name}')");
                else if (!string.IsNullOrEmpty(r.ResumeError))
                    sb.AppendLine($"\nResume error: {r.ResumeError}");

                sb.AppendLine();
                sb.AppendLine("Saves that will be processed:");
                sb.AppendLine();

                if (r.Queue.Count == 0)
                {
                    sb.AppendLine("(none — adjust your filters)");
                }
                else
                {
                    int i = 1;
                    foreach (var s in r.Queue.Take(50))
                    {
                        string marker = (r.HasResume && i - 1 == r.StartIdx) ? " ← RESUME HERE" : "";
                        sb.AppendLine($"  {i}. {s.Name}{marker}");
                        sb.AppendLine($"     city='{s.CityName}', modified {s.LastModified:yyyy-MM-dd HH:mm}");
                        i++;
                    }
                    if (r.Queue.Count > 50)
                        sb.AppendLine($"  ... and {r.Queue.Count - 50} more (see log for full list)");
                }

                LogsTools.Info($"[DEBUG] Filtered saves: {r.Queue.Count}");
                int idx = 1;
                foreach (var s in r.Queue)
                {
                    LogsTools.Info($"[DEBUG]   #{idx++} {s.Name} (city='{s.CityName}', modified={s.LastModified:yyyy-MM-dd HH:mm})");
                }

                UITools.ShowMessage($"Filtered saves ({r.Queue.Count})", sb.ToString());
            }
            catch (Exception ex)
            {
                LogsTools.Error($"[DEBUG] DebugListSaves failed: {ex}");
                UITools.ShowError($"Failed: {ex.Message}");
            }
        }


        private static void OpenFolder(string path)
        {
            try
            {
                System.IO.Directory.CreateDirectory(path);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { LogsTools.Warn($"OpenFolder failed: {ex.Message}"); }
        }
    }
}