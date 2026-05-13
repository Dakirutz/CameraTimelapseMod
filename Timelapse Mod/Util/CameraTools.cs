using CameraTimelapseMod.Data;
using CameraTimelapseMod.Systems;
using Colossal.IO.AssetDatabase;
using Game.Assets;
using Game.Rendering;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using System.IO;

namespace CameraTimelapseMod.Util
{
    internal class CameraTools
    {


        public static void ApplyPhotoModeProperties(List<PhotoModeEntry> entries, bool skipTimeOfDay = false)
        {
            if (entries == null || entries.Count == 0) return;

            try
            {
                var photoMode = World.DefaultGameObjectInjectionWorld
                    ?.GetExistingSystemManaged<Game.Rendering.PhotoModeRenderSystem>();
                if (photoMode == null || photoMode.photoModeProperties == null) return;

                int applied = 0;
                int skipped = 0;
                foreach (var entry in entries)
                {
                    if (string.IsNullOrEmpty(entry.Id)) continue;

                    if (skipTimeOfDay && entry.Id == "Time of Day")
                    {
                        skipped++;
                        continue;
                    }

                    if (!photoMode.photoModeProperties.TryGetValue(entry.Id, out var prop))
                    {
                        skipped++;
                        continue;
                    }
                    if (prop == null) continue;

                    try
                    {
                        prop.setEnabled?.Invoke(entry.IsEnabled);
                        if (entry.IsEnabled)
                            prop.setValue?.Invoke(entry.Value);
                        applied++;
                    }
                    catch (Exception ex)
                    {
                        LogsTools.Warn($"Apply property '{entry.Id}' failed: {ex.Message}");
                    }
                }
                LogsTools.Info($"Applied {applied} photo mode properties (skipped {skipped})");
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"ApplyPhotoModeProperties failed: {ex.Message}");
            }
        }

        private static List<PhotoModeEntry> CapturePhotoModeProperties()
        {
            var result = new List<PhotoModeEntry>();
            try
            {
                var photoMode = World.DefaultGameObjectInjectionWorld
                    ?.GetExistingSystemManaged<Game.Rendering.PhotoModeRenderSystem>();
                if (photoMode == null || photoMode.photoModeProperties == null)
                    return result;

                foreach (var kv in photoMode.photoModeProperties)
                {
                    var p = kv.Value;
                    if (p == null) continue;
                    if (p.getValue == null) continue;

                    try
                    {
                        bool enabled = p.isEnabled?.Invoke() ?? true;
                        float value = p.getValue();
                        result.Add(new PhotoModeEntry
                        {
                            Id = p.id,
                            Value = value,
                            IsEnabled = enabled
                        });
                    }
                    catch (Exception ex)
                    {
                        LogsTools.Warn($"Capture property '{p.id}' failed: {ex.Message}");
                    }
                }
                LogsTools.Info($"Captured {result.Count} photo mode properties");
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"CapturePhotoModeProperties failed: {ex.Message}");
            }
            return result;
        }
        public static bool TryWritePivotZoomRotation(object controller, Vector3 pivot, float zoom, Vector3 rotation)
        {
            try
            {
                var t = controller.GetType();
                t.GetProperty("pivot")?.SetValue(controller, pivot);
                t.GetProperty("zoom")?.SetValue(controller, zoom);
                t.GetProperty("rotation")?.SetValue(controller, rotation);
                return true;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"TryWritePivotZoomRotation failed: {ex.Message}");
                return false;
            }
        }

        private static void FallbackCapture(Game.Rendering.CameraUpdateSystem sys, CameraPreset preset)
        {
            if (sys.gamePlayController != null)
            {
                var c = sys.gamePlayController;
                preset.SetPivot(c.pivot);
                preset.Zoom = c.zoom;
                preset.SetRotation(c.rotation);

            }
            else
            {
                var c = sys.orbitCameraController;
                preset.SetPivot(c.pivot);
                preset.Zoom = c.zoom;
                preset.SetRotation(c.rotation);

            }
        }

        private static bool TryGetPivotZoomRotation(object controller, out Vector3 pivot, out float zoom, out Vector3 rotation)
        {
            pivot = default;
            zoom = 0f;
            rotation = default;
            try
            {
                var t = controller.GetType();
                var pivotProp = t.GetProperty("pivot");
                var zoomProp = t.GetProperty("zoom");
                var rotProp = t.GetProperty("rotation");
                if (pivotProp == null || zoomProp == null || rotProp == null) return false;

                pivot = (Vector3)pivotProp.GetValue(controller);
                zoom = (float)zoomProp.GetValue(controller);
                rotation = (Vector3)rotProp.GetValue(controller);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsPhotoModeActive()
        {


            var photoMode = World.DefaultGameObjectInjectionWorld
                ?.GetExistingSystemManaged<Game.Rendering.PhotoModeRenderSystem>();
            if (photoMode == null) return false;
            try
            {
                var field = typeof(Game.Rendering.PhotoModeRenderSystem)
                    .GetField("m_Active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field == null) return photoMode.Enabled;   
                return (bool)field.GetValue(photoMode);
            }
            catch
            {
                return photoMode.Enabled;
            }
        }

        public static CameraPreset CaptureCurrentAsPreset()
        {
            var sys = World.DefaultGameObjectInjectionWorld
                          .GetExistingSystemManaged<Game.Rendering.CameraUpdateSystem>();

            CameraPreset preset = new CameraPreset();

            var active = sys.activeCameraController;
            if (active != null)
            {
                if (TryGetPivotZoomRotation(active, out var pivot, out var zoom, out var rotation))
                {
                    preset.SetPivot(pivot);
                    preset.Zoom = zoom;
                    preset.SetRotation(rotation);
                    LogsTools.Info($"Captured camera from active controller: {active.GetType().Name}");
                }
                else
                {
                    LogsTools.Warn($"Active controller {active.GetType().Name} doesn't expose pivot/zoom/rotation, falling back");
                    FallbackCapture(sys, preset);
                }
            }
            else
            {
                FallbackCapture(sys, preset);
            }

            if (IsPhotoModeActive())
            {
                LogsTools.Info("Photo mode is active, capturing photo properties");
                preset.PhotoModeProperties = CapturePhotoModeProperties();
            }
            else
            {
                preset.PhotoModeProperties = new List<PhotoModeEntry>();
            }

            return preset;
        }

        public static IEnumerator CaptureToRenderTexture(string path, int width, int height, int msaa)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                foreach (var c in Camera.allCameras)
                    if (c.cameraType == CameraType.Game) { cam = c; break; }
            }
            if (cam == null)
            {
                LogsTools.Error("No game camera found for HD capture");
                yield break;
            }

            RenderTexture rt = null;
            Texture2D readback = null;
            bool renderOk = false;

            {
                RenderTexture prevTarget = cam.targetTexture;
                try
                {
                    rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                    {
                        antiAliasing = msaa,
                        name = "CameraTimelapseMod_CaptureRT"
                    };
                    rt.Create();

                    cam.targetTexture = rt;
                    cam.Render();
                    renderOk = true;
                }
                catch (Exception ex)
                {
                    LogsTools.Error($"RenderTexture setup/render failed: {ex.Message}");
                    if (rt != null) { try { rt.Release(); } catch { } UnityEngine.Object.Destroy(rt); rt = null; }
                }
                finally
                {
                    cam.targetTexture = prevTarget;  
                }
            }

            if (!renderOk) yield break;

            yield return new WaitForEndOfFrame();

            try
            {
                RenderTexture.active = rt;
                readback = new Texture2D(width, height, TextureFormat.RGB24, false);
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply();
                RenderTexture.active = null;

                byte[] png = readback.EncodeToPNG();
                File.WriteAllBytes(path, png);
                LogsTools.Info($"Captured ({width}x{height}, MSAA x{msaa}): {path}");
            }
            catch (Exception ex)
            {
                LogsTools.Error($"Capture readback/encode failed: {ex}");
            }
            finally
            {
                if (readback != null) UnityEngine.Object.Destroy(readback);
                if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
            }
        }

        public static CinematicCameraAsset[] GetAllCinematics()
        {
            try
            {
                return AssetDatabase.global
                    .GetAssets(default(SearchFilter<CinematicCameraAsset>))
                    .ToArray();
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"GetAllCinematics failed: {ex.Message}");
                return new CinematicCameraAsset[0];
            }
        }

        public static CinematicCameraAsset FindCinematicByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var all = GetAllCinematics();

            var exact = all.FirstOrDefault(a =>
                string.Equals(a.name, name, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            return all.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.name) &&
                a.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static void EnterCinematicMode()
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;

                var photoUISystem = world?.GetExistingSystemManaged<Game.UI.InGame.PhotoModeUISystem>();
                if (photoUISystem != null)
                {
                    photoUISystem.Activate(true);
                }
                else
                {
                    var photoMode = world?.GetExistingSystemManaged<PhotoModeRenderSystem>();
                    photoMode?.Enable(true);
                }
                UnityEngine.Cursor.visible = false;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"EnterCinematicMode failed: {ex.Message}");
            }
        }

        public static void ExitCinematicMode()
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;

                var gamePanelSystem = world?.GetExistingSystemManaged<Game.UI.InGame.GamePanelUISystem>();
                var photoUISystem = world?.GetExistingSystemManaged<Game.UI.InGame.PhotoModeUISystem>();

                bool panelWasClosed = false;
                if (gamePanelSystem != null && gamePanelSystem.activePanel is Game.UI.InGame.PhotoModePanel)
                {
                    gamePanelSystem.ClosePanel(typeof(Game.UI.InGame.PhotoModePanel).FullName);
                    panelWasClosed = true;
                }

                if (!panelWasClosed && photoUISystem != null)
                {
                    photoUISystem.Activate(false);
                }

                UnityEngine.Cursor.visible = true;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"ExitCinematicMode failed: {ex.Message}");
            }
        }

        public static void ShowAvailableCinematics()
        {
            var all = GetAllCinematics();
            if (all.Length == 0)
            {
                UITools.ShowMessage(
                    "Cinematics",
                    "No cinematics found.\n\n" +
                    "Create some in CS2 Photo Mode → Cinematic Camera, save them, " +
                    "then come back here.");
                return;
            }

            string list = string.Join("\n  - ", all.Select(a => a.name));
            UITools.ShowMessage(
                $"Available cinematics ({all.Length})",
                $"  - {list}\n\nCopy the names you want, separated by commas, " +
                "into the 'Cinematics to record' field.");
        }

        public static bool MoveCameraToEdge(
            Entity edge,
            float zoom = 1000f,
            float pitchDegrees = 45f,
            float yawDegrees = 0f)
        {
            try
            {

                if (!GameTools.TryGetEdgePosition(edge, out var pos))
                {
                    LogsTools.Warn($"MoveCameraToEdge: cannot resolve position of edge {edge.Index}");
                    return false;
                }

                var preset = new CameraTimelapseMod.Data.CameraPreset
                {
                    Name = "_auto_edge_focus",
                    Zoom = zoom,
                };
                preset.SetPivot(pos);
                preset.SetRotation(new Vector3(pitchDegrees, yawDegrees, 0f));

                PresetsSystem.Apply(preset);
                LogsTools.Info(
                    $"MoveCameraToEdge: focused on edge {edge.Index} at " +
                    $"({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
                return true;
            }
            catch (Exception ex)
            {
                LogsTools.Error($"MoveCameraToEdge failed: {ex}");
                return false;
            }
        }
    }
}
