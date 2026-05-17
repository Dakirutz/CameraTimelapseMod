using Colossal.UI.Binding;
using Game.SceneFlow;
using Game.UI;
using Game.UI.Localization;
using CameraTimelapseMod.Data;
using CameraTimelapseMod.Util;
using System;
using System.Linq;
using CameraTimelapseMod.Util;
using Unity.Entities;
using UnityEngine;

namespace CameraTimelapseMod.Systems
{
    public partial class UISystem : UISystemBase
    {
        private const string ModId = "CameraTimelapseMod";

        private static ValueBinding<bool> s_OpenPanelBinding;
        private static ValueBinding<int> s_CloseMenuBinding;


        protected override void OnCreate()
        {
            base.OnCreate();


            AddUpdateBinding(new GetterValueBinding<string>(ModId, "presetsJson",
            () =>
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("{\"Items\":[");
                for (int i = 0; i < PresetsSystem.Presets.Count; i++)
                {
                    if (i > 0) sb.Append(",");

                    var p = PresetsSystem.Presets[i];
                    bool hasPhotoMode = p.PhotoModeProperties != null && p.PhotoModeProperties.Count > 0;

                    sb.Append("{");
                    sb.Append("\"Name\":").Append(Tools.JsonEscape(p.Name ?? "")); 
                    sb.Append(",\"PivotX\":").Append(p.PivotX.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(",\"PivotY\":").Append(p.PivotY.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(",\"PivotZ\":").Append(p.PivotZ.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(",\"Zoom\":").Append(p.Zoom.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(",\"Rotation\":{");
                    sb.Append("\"x\":").Append(p.RotationX.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(",\"y\":").Append(p.RotationY.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(",\"z\":").Append(p.RotationZ.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append("}");
                    sb.Append(",\"HasPhotoMode\":").Append(hasPhotoMode ? "true" : "false");
                    sb.Append("}");
                }
                sb.Append("]}");
                return sb.ToString();
            }));

            AddBinding(new TriggerBinding<int>(ModId, "gotoPreset", OnGotoPreset));
            AddBinding(new TriggerBinding<int>(ModId, "deletePreset", OnDeletePreset));
            AddBinding(new TriggerBinding(ModId, "deleteAllPresets", OnDeleteAllPresets));
            AddBinding(new TriggerBinding<int, string>(ModId, "renamePreset", OnRenamePreset));
            AddBinding(new TriggerBinding(ModId, "captureCurrentAsPreset", OnCaptureCurrent));

            AddBinding(new TriggerBinding(ModId, "exportPresets", () => PresetsSystem.ExportPresets()));
            AddBinding(new TriggerBinding(ModId, "importPresets", () => PresetsSystem.ImportPresets()));

            AddUpdateBinding(new GetterValueBinding<string>(ModId, "sessionProgressJson", () => GetSessionProgressJson()));

            AddBinding(new TriggerBinding(ModId, "sessionPause", () =>
            {
                SessionSystem.RequestPause();
            }));

            AddBinding(new TriggerBinding(ModId, "sessionResume", () =>
            {
                SessionSystem.RequestResume();
            }));

            AddBinding(new TriggerBinding(ModId, "sessionStop", () =>
            {
                SessionSystem.RequestStop();
            }));

            s_OpenPanelBinding = new ValueBinding<bool>(ModId, "openPanel", false);
            AddBinding(s_OpenPanelBinding);

            AddBinding(new TriggerBinding(ModId, "panelClosed", () => s_OpenPanelBinding.Update(false)));

            s_CloseMenuBinding = new ValueBinding<int>(ModId, "closeMenuTick", 0);
            AddBinding(s_CloseMenuBinding);

            AddBinding(new TriggerBinding(ModId, "openScreenshotFolder", () =>
            {
                Tools.OpenScreenshotFolder();
            }));

            AddBinding(new TriggerBinding(ModId, "openForumTopic", () =>
            {
                Tools.OpenUrl(Mod.forumLink);
            }));

            AddBinding(new TriggerBinding(ModId, "writeEmail", () =>
            {
                Tools.OpenUrl("mailto:"+Mod.getEmail()+ "?subject=CameraTimelapseMod%20Feedback");
            }));

            AddUpdateBinding(new GetterValueBinding<string>(ModId, "autoTimelapseJson",
           () => GetAutoTimelapseJson()));

            AddBinding(new TriggerBinding(ModId, "autoTimelapseStop", () =>
            {
                AutoTimelapseSessionSystem.CancelSession();
            }));

            AddBinding(new TriggerBinding(ModId, "autoTimelapsePause", () =>
            {
                AutoTimelapseSessionSystem.RequestPause();
            }));

            AddBinding(new TriggerBinding(ModId, "autoTimelapseResume", () =>
            {
                AutoTimelapseSessionSystem.RequestResume();
            }));

            AddUpdateBinding(new GetterValueBinding<bool>(ModId, "isPhotoModeActive",
            () =>
            {
                try
                {
                    var photoMode = World.DefaultGameObjectInjectionWorld
                        ?.GetExistingSystemManaged<Game.Rendering.PhotoModeRenderSystem>();
                    if (photoMode == null) return false;

                    var field = typeof(Game.Rendering.PhotoModeRenderSystem)
                        .GetField("m_Active",
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance);
                    if (field == null) return photoMode.Enabled;
                    return (bool)field.GetValue(photoMode);
                }
                catch { return false; }
            }));

            AddBinding(new TriggerBinding(ModId, "exitPhotoMode", () =>
            {
                CameraTools.ExitCinematicMode();
                LogsTools.Info("Photo mode disabled by user");
            }));


            Mod.log.Info("PresetUISystem bindings registered");
        }

        private static string GetAutoTimelapseJson()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{");
            sb.Append($"\"active\":{(AutoTimelapseSessionSystem.IsRunning ? "true" : "false")},");
            sb.Append($"\"paused\":{(AutoTimelapseSessionSystem.IsPaused ? "true" : "false")},");
            sb.Append($"\"currentStep\":{AutoTimelapseSessionSystem.CurrentStep},");
            sb.Append($"\"totalSteps\":{AutoTimelapseSessionSystem.EstimatedTotalSteps},");
            sb.Append($"\"totalEdgesProcessed\":{AutoTimelapseSessionSystem.TotalEdgesProcessed},");
            sb.Append($"\"edgesLeft\":{AutoTimelapseSessionSystem.LastKnownEdgeCount},");
            sb.Append($"\"phase\":\"{Tools.Escape(AutoTimelapseSessionSystem.CurrentPhase ?? "")}\",");
            sb.Append($"\"folder\":\"{Tools.Escape(AutoTimelapseSessionSystem.SessionFolder ?? "")}\",");  
            sb.Append($"\"comment\":\"{Tools.Escape(Mod.sorryForThisCommentAhah)}\"");
            sb.Append("}");
            return sb.ToString();
        }

        private void OnGotoPreset(int index)
        {
            if (index < 0 || index >= PresetsSystem.Presets.Count)
            {
                Mod.log.Warn($"gotoPreset: invalid index {index}");
                return;
            }
            PresetsSystem.Apply(PresetsSystem.Presets[index]);
            Mod.log.Info($"UI requested goto preset #{index}");
        }

        private static string GetSessionProgressJson()
        {
            var s = SessionSystem.GetProgressSnapshot();
            if (s == null || !s.IsActive)
                return "{\"active\":false}";

            var sb = new System.Text.StringBuilder(); 
            string currentTimeStr = float.IsNaN(s.CurrentTime) ? "null": s.CurrentTime.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            sb.Append("{");
            sb.Append("\"active\":true,");
            sb.Append($"\"paused\":{(s.IsPaused ? "true" : "false")},");
            sb.Append($"\"saveIdx\":{s.SaveIdx},");
            sb.Append($"\"saveTotal\":{s.SaveTotal},");
            sb.Append($"\"viewIdx\":{s.ViewIdx},");
            sb.Append($"\"viewTotal\":{s.ViewTotal},");
            sb.Append($"\"timeIdx\":{s.TimeIdx},");
            sb.Append($"\"timeTotal\":{s.TimeTotal},");
            sb.Append($"\"currentTime\":{currentTimeStr},");
            sb.Append($"\"currentSave\":\"{Tools.Escape(s.CurrentSave)}\",");
            sb.Append($"\"phase\":\"{s.Phase}\",");
            sb.Append($"\"etaSeconds\":{s.EtaSeconds},");
            sb.Append($"\"completedScreenshots\":{s.CompletedScreenshots},");
            sb.Append($"\"comment\":\"{Tools.Escape(Mod.sorryForThisCommentAhah)}\"");
            sb.Append("}");
            return sb.ToString();
        }

        private void OnDeletePreset(int index)
        {
            if (index < 0 || index >= PresetsSystem.Presets.Count)
            {
                Mod.log.Warn($"deletePreset: invalid index {index}");
                return;
            }
            string name = PresetsSystem.Presets[index].Name;
            PresetsSystem.Presets.RemoveAt(index);
            PresetsSystem.Save();
            Mod.log.Info($"UI deleted preset '{name}' (#{index})");
        }

        private void OnDeleteAllPresets()
        {
            PresetsSystem.ClearAll();
            Mod.log.Info("UI cleared all presets");
        }

        private void OnRenamePreset(int index, string newName)
        {
            if (index < 0 || index >= PresetsSystem.Presets.Count)
            {
                Mod.log.Warn($"renamePreset: invalid index {index}");
                return;
            }
            if (string.IsNullOrWhiteSpace(newName)) return;

            PresetsSystem.Presets[index].Name = newName.Trim();
            PresetsSystem.Save();
            Mod.log.Info($"UI renamed preset #{index} to '{newName}'");
        }

        private void OnCaptureCurrent()
        {
            if (!Mod.IsInGame)
            {
                UITools.ShowError("You must be in a city to capture a camera view.");
                return;
            }
            PresetsSystem.AddFromCurrentCamera();
        }
        public static void RequestOpenPanel()
        {
            if (!Mod.IsInGame)
            {
                UITools.ShowError("Open a city first to manage presets.");
                return;
            }
            if (s_OpenPanelBinding == null) return;

            s_OpenPanelBinding.Update(true);
            Mod.log.Info("Panel open requested");

            UITools.CloseGameMenu();
        }

        public static void RequestClosePanel()
        {
            if (s_OpenPanelBinding != null)
                s_OpenPanelBinding.Update(false);
        }
    }
}