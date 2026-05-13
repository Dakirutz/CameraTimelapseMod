using CameraTimelapseMod.Data;
using CameraTimelapseMod.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace CameraTimelapseMod.Systems
{
    public static class PresetsSystem
    {
        public static List<CameraPreset> Presets { get; private set; } = new List<CameraPreset>();

        public static string PresetsPath => Path.Combine(Mod.DataDir, "presets.json");
        public static string ExportsDir => Path.Combine(Mod.DataDir, "Exports");

        public static void Load()
        {
            try
            {
                if (!File.Exists(PresetsPath))
                {
                    LogsTools.Info("No presets file yet");
                    return;
                }
                var json = File.ReadAllText(PresetsPath);
                LogsTools.Info($"Reading presets file ({json.Length} chars)");


                // === DEBUG ===
                LogsTools.Info($"DEBUG Load: raw JSON = {json}");
                var rawWrapper = JsonUtility.FromJson<CameraPresetList>(json);
                LogsTools.Info($"DEBUG Load: rawWrapper is null? {rawWrapper == null}");
                if (rawWrapper != null)
                {
                    LogsTools.Info($"DEBUG Load: rawWrapper.Items is null? {rawWrapper.Items == null}");
                    LogsTools.Info($"DEBUG Load: rawWrapper.Items.Count = {rawWrapper.Items?.Count ?? -1}");
                    if (rawWrapper.Items != null && rawWrapper.Items.Count > 0)
                    {
                        var first = rawWrapper.Items[0];
                        LogsTools.Info($"DEBUG Load: first preset Name = '{first?.Name}'");
                    }
                }
                // === FIN DEBUG ===

                Presets = ParsePresetsJson(json);
                LogsTools.Info($"Loaded {Presets.Count} camera presets");
            }
            catch (Exception ex) { LogsTools.Error($"Load presets failed: {ex}"); }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PresetsPath));
                string json = SerializePresetsToJson(Presets);
                File.WriteAllText(PresetsPath, json);
                LogsTools.Info($"Saved {Presets.Count} presets ({json.Length} chars)");
            }
            catch (Exception ex) { LogsTools.Error($"Save presets failed: {ex}"); }
        }

        public static void AddFromCurrentCamera()
        {
            if (!Mod.IsInGame)
            {
                LogsTools.Warn("Cannot capture preset: not in a loaded game.");
                UITools.ShowError("You must load a city first before adding a preset.");
                return;
            }
            try
            {
                var preset = CameraTools.CaptureCurrentAsPreset();
                preset.Name = $"View {Presets.Count + 1}";
                Presets.Add(preset);

                LogsTools.Info($"DEBUG: preset.PhotoModeProperties is null? {preset.PhotoModeProperties == null}");
                LogsTools.Info($"DEBUG: preset.PhotoModeProperties.Count = {preset.PhotoModeProperties?.Count ?? -1}");
                string testJson = JsonUtility.ToJson(preset);
                LogsTools.Info($"DEBUG: ToJson output = {testJson}");

                Save();
                LogsTools.Info($"Added preset '{preset.Name}'");
            }
            catch (Exception ex)
            {
                LogsTools.Error($"AddFromCurrentCamera failed: {ex.GetType().Name}: {ex.Message}");
                LogsTools.Error($"Stack: {ex.StackTrace}");
            }
        }

        public static void ClearAll()
        {
            Presets.Clear();
            Save();
            LogsTools.Info("Cleared all presets");
        }

        public static void Apply(CameraPreset p) => ApplyInternal(p, overrideTimeOfDay: false);
        public static void ApplyIgnoreTimeOfDay(CameraPreset p) => ApplyInternal(p, overrideTimeOfDay: true);

        private static void ApplyInternal(CameraPreset p, bool overrideTimeOfDay)
        {
            if (p == null) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                LogsTools.Warn("ApplyInternal: no World available");
                return;
            }

            var sys = world.GetExistingSystemManaged<Game.Rendering.CameraUpdateSystem>();
            if (sys == null)
            {
                LogsTools.Warn("ApplyInternal: CameraUpdateSystem not ready yet");
                return;
            }
            bool hasPhotoProps = p.PhotoModeProperties != null && p.PhotoModeProperties.Count > 0;

            if (hasPhotoProps)
            {
                var photoUI = world.GetExistingSystemManaged<Game.UI.InGame.PhotoModeUISystem>();
                if (photoUI != null)
                {
                    photoUI.Activate(true);
                }
                else
                {
                    var photoMode = world.GetExistingSystemManaged<Game.Rendering.PhotoModeRenderSystem>();
                    photoMode?.Enable(true);
                }

                var cine = sys.cinematicCameraController;
                if (cine != null)
                {
                    CameraTools.TryWritePivotZoomRotation(cine, p.GetPivot(), p.Zoom, p.GetRotation());
                    sys.activeCameraController = cine;
                }
            }
            else
            {
                if (CameraTools.IsPhotoModeActive())
                {
                    CameraTools.ExitCinematicMode();
                }

                var c = sys.orbitCameraController;
                c.pivot = p.GetPivot();
                c.zoom = p.Zoom;
                c.rotation = p.GetRotation();
                sys.activeCameraController = c;
            }

            CameraTools.ApplyPhotoModeProperties(p.PhotoModeProperties, overrideTimeOfDay);
        }

        public static string ExportPresets()
        {
            try
            {
                if (Presets.Count == 0)
                {
                    UITools.ShowError("No presets to export.");
                    return null;
                }

                Directory.CreateDirectory(ExportsDir);
                string fileName = $"presets_{DateTime.Now:yyyyMMdd-HHmmss}.json";
                string fullPath = Path.Combine(ExportsDir, fileName);

                File.WriteAllText(fullPath, SerializePresetsToJson(Presets));
                LogsTools.Info($"Exported {Presets.Count} presets to {fullPath}");

                Tools.OpenFolder(ExportsDir);

                UITools.ShowMessage(
                    "Presets exported",
                    $"Exported {Presets.Count} preset(s) to:\n\n{fullPath}\n\n" +
                    "The Exports folder has been opened. You can copy this file " +
                    "and share it, or import it later.");

                return fullPath;
            }
            catch (Exception ex)
            {
                LogsTools.Error($"ExportPresets failed: {ex}");
                UITools.ShowError($"Export failed: {ex.Message}");
                return null;
            }
        }

        public static void ImportPresets()
        {
            try
            {
                Directory.CreateDirectory(ExportsDir);

                var files = Directory.GetFiles(ExportsDir, "*.json")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .ToList();

                if (files.Count == 0)
                {
                    ShowEmptyExportsMessage();
                    return;
                }

                string latestFile = files[0];
                UITools.ShowConfirm(
                    "Import presets",
                    $"Import the most recent export?\n\n" +
                    $"  - {Path.GetFileName(latestFile)}\n\n" +
                    (files.Count > 1
                        ? $"({files.Count - 1} other older file(s) available, use file explorer to manage)\n\n"
                        : "") +
                    "WARNING: this will REPLACE all your current presets.\n\n" +
                    "Continue?",
                    () => DoImport(latestFile));
            }
            catch (Exception ex)
            {
                LogsTools.Error($"ImportPresets failed: {ex}");
                UITools.ShowError($"Import failed: {ex.Message}");
            }
        }

        private static void DoImport(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    UITools.ShowError($"File not found: {filePath}");
                    return;
                }

                string json = File.ReadAllText(filePath);
                LogsTools.Info($"Import: read {json.Length} chars from {filePath}");

                var items = ParsePresetsJson(json);
                LogsTools.Info($"Import: parsed {items.Count} preset(s)");

                if (items.Count == 0)
                {
                    UITools.ShowError(
                        $"No presets found in file (or malformed).\n\n" +
                        $"File: {Path.GetFileName(filePath)}\n" +
                        $"Size: {json.Length} chars");
                    return;
                }

                Presets = items;
                Save();

                LogsTools.Info($"Imported {Presets.Count} presets from {filePath}");
                UITools.ShowMessage(
                    "Presets imported",
                    $"Imported {Presets.Count} preset(s) from:\n{Path.GetFileName(filePath)}\n\n" +
                    "Reopen the preset panel to see them.");
            }
            catch (Exception ex)
            {
                LogsTools.Error($"DoImport failed: {ex}");
                UITools.ShowError($"Import failed: {ex.Message}");
            }
        }

        private static void ShowEmptyExportsMessage()
        {
            UITools.ShowMessage(
                "Import presets",
                $"No JSON files found in:\n\n{ExportsDir}\n\n" +
                "Place a presets export file there and try again. " +
                "The Exports folder has been opened.");
            Tools.OpenFolder(ExportsDir);
        }

        private static List<CameraPreset> ParsePresetsJson(string json)
        {
            var result = new List<CameraPreset>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            int bracketStart = json.IndexOf('[');
            int bracketEnd = json.LastIndexOf(']');
            if (bracketStart < 0 || bracketEnd <= bracketStart) return result;

            string arr = json.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);

            int depth = 0;
            int objStart = -1;
            for (int i = 0; i < arr.Length; i++)
            {
                char c = arr[i];
                if (c == '{')
                {
                    if (depth == 0) objStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objStart >= 0)
                    {
                        string objJson = arr.Substring(objStart, i - objStart + 1);
                        try
                        {
                            var p = ParseSinglePreset(objJson);
                            if (p != null) result.Add(p);
                        }
                        catch (Exception ex)
                        {
                            LogsTools.Warn($"Skipping malformed preset: {ex.Message}");
                        }
                        objStart = -1;
                    }
                }
            }
            return result;
        }

        private static CameraPreset ParseSinglePreset(string objJson)
        {
            // On extrait d'abord PhotoModeProperties s'il existe (parce que JsonUtility le drop)
            var photoEntries = ExtractPhotoModeEntriesArray(objJson);

            // On parse le reste avec JsonUtility (qui marche pour les champs simples)
            var p = JsonUtility.FromJson<CameraPreset>(objJson);
            if (p == null) return null;

            // On force la liste (au cas où JsonUtility l'aurait droppée)
            p.PhotoModeProperties = photoEntries;
            return p;
        }

        private static List<PhotoModeEntry> ExtractPhotoModeEntriesArray(string objJson)
        {
            var result = new List<PhotoModeEntry>();
            const string key = "\"PhotoModeProperties\"";
            int keyIdx = objJson.IndexOf(key);
            if (keyIdx < 0) return result;

            int bracketStart = objJson.IndexOf('[', keyIdx);
            if (bracketStart < 0) return result;

            int depth = 0;
            int bracketEnd = -1;
            for (int i = bracketStart; i < objJson.Length; i++)
            {
                if (objJson[i] == '[') depth++;
                else if (objJson[i] == ']')
                {
                    depth--;
                    if (depth == 0) { bracketEnd = i; break; }
                }
            }
            if (bracketEnd < 0) return result;

            string arr = objJson.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);

            depth = 0;
            int entryStart = -1;
            for (int i = 0; i < arr.Length; i++)
            {
                char c = arr[i];
                if (c == '{')
                {
                    if (depth == 0) entryStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && entryStart >= 0)
                    {
                        string entryJson = arr.Substring(entryStart, i - entryStart + 1);
                        try
                        {
                            var e = JsonUtility.FromJson<PhotoModeEntry>(entryJson);
                            if (e != null) result.Add(e);
                        }
                        catch { }
                        entryStart = -1;
                    }
                }
            }
            return result;
        }

        private static string SerializePresetsToJson(List<CameraPreset> presets)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"Items\":[");
            for (int i = 0; i < presets.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(SerializeSinglePreset(presets[i]));
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string SerializeSinglePreset(CameraPreset p)
        {
            // JsonUtility pour les champs simples — il drop PhotoModeProperties, on l'ajoutera nous-mêmes
            string baseJson = JsonUtility.ToJson(p);

            // On retire l'accolade de fin pour pouvoir ajouter notre champ
            if (baseJson.EndsWith("}"))
                baseJson = baseJson.Substring(0, baseJson.Length - 1);

            var sb = new System.Text.StringBuilder(baseJson);

            // Ajout de PhotoModeProperties manuellement
            sb.Append(",\"PhotoModeProperties\":[");
            if (p.PhotoModeProperties != null)
            {
                for (int i = 0; i < p.PhotoModeProperties.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(JsonUtility.ToJson(p.PhotoModeProperties[i]));
                }
            }
            sb.Append("]}");
            return sb.ToString();
        }
    }
}