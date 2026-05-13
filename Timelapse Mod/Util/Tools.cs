using CameraTimelapseMod;
using CameraTimelapseMod.Systems;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace CameraTimelapseMod.Util
{
    internal class Tools
    {

        public static bool isInGameAndHasPresetOrCinematic()
        {
            if (!Mod.IsInGame)
            {
                LogsTools.Warn("Cannot start session: not in a loaded game.");
                UITools.ShowError("You must load a city. ");
                return false;
            }

            bool hasPresets = PresetsSystem.Presets.Count > 0;
            bool hasCinematics = !string.IsNullOrEmpty(Mod.Setting?.CinematicsToRecord);
            if (!hasPresets && !hasCinematics)
            {
                UITools.ShowError("You need at least one camera preset OR one cinematic configured (Video tab) before starting.");
                return false;
            }
            return true;
        }


        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }


        public static string JsonEscape(string s)
        {
            if (s == null) return "\"\"";
            var sb = new System.Text.StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }

        public static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "unnamed";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        public static void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                Mod.log.Info($"Opened URL: {url}");
            }
            catch (System.Exception ex)
            {
                Mod.log.Error($"OpenUrl failed for '{url}': {ex.Message}");
            }
        }



        public static void ShutdownComputer()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/s /t 30 /c \"CameraTimelapseMod: screenshot session complete. Shutting down. Run 'shutdown /a' to cancel.\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (System.Exception ex)
            {
                LogsTools.Error($"Shutdown failed: {ex.Message}");
            }
        }


        public static void RestartGame()
        {
            try
            {
                StopCrashWatchdog();
                LogsTools.Info("Restart requested");

                UITools.ShowMessage("Game will restart in 5 seconds to free memory and continue the screenshot session... Do not touch anything until the game restarted and loaded the next save, inbetween it can feel stuck, but it's probably not.");

                CoroutineSystem.Instance.StartCoroutine(RestartGameDelayed(5f));
            }
            catch (Exception ex)
            {
                LogsTools.Error($"RestartGame: {ex}");
            }
        }



        private static string BuildRestartHta(int pid, string launchCmd, bool isSteam)
        {
            string launchEsc = launchCmd.Replace("\\", "\\\\").Replace("'", "\\'");
            string isSteamJs = isSteam ? "true" : "false";

            return @"<!DOCTYPE html>
<html>
<head>
    <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"">
    <title>Auto Timelapse Mod - Restarting Game</title>
    <HTA:APPLICATION
        ID='restart'
        APPLICATIONNAME='CameraTimelapseMod Restart'
        BORDER='thin'
        CAPTION='yes'
        SHOWINTASKBAR='yes'
        SINGLEINSTANCE='yes'
        SYSMENU='yes'
        WINDOWSTATE='normal'
        SCROLL='no' />
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, sans-serif;
            background: #1e2440;
            color: #e8ecf5;
            margin: 0;
            padding: 30px;
        }
        h1 {
            margin: 0 0 10px 0;
            font-size: 18px;
            color: #ffd166;
        }
        .status {
            background: #14172a;
            border-left: 3px solid #4ecdc4;
            padding: 12px 15px;
            margin: 15px 0;
            font-size: 13px;
            line-height: 1.5;
        }
    </style>
</head>
<body>
    <h1>Auto Timelapse Mod - Restarting Game</h1>

    <div class='status'>
        <b id='state'>Waiting for Cities Skylines II to close...</b><br>
        <span id='detail'>This is normal - the mod is freeing memory between saves to keep your session stable.</span>
    </div>

    <script language='JScript'>
        var pid = " + pid + @";
        var launchCmd = '" + launchEsc + @"';
        var isSteam = " + isSteamJs + @";
        var shell = new ActiveXObject('WScript.Shell');

        function isProcessRunning(targetPid) {
            try {
                var wmi = GetObject('winmgmts:\\\\.\\root\\cimv2');
                var procs = wmi.ExecQuery('SELECT * FROM Win32_Process WHERE ProcessId = ' + targetPid);
                var e = new Enumerator(procs);
                return !e.atEnd();
            } catch (err) {
                return false;
            }
        }

        function waitClose() {
            if (!isProcessRunning(pid)) {
                document.getElementById('state').innerText = 'Game closed.';
                document.getElementById('detail').innerText = 'Launching Cities Skylines II in 3 seconds...';
                setTimeout(relaunch, 3000);
                return;
            }
            setTimeout(waitClose, 1000);
        }

        function relaunch() {
            try {
                var q = String.fromCharCode(34);
                if (isSteam) {
                    shell.Run(launchCmd, 1, false);
                } else {
                    shell.Run(q + launchCmd + q, 1, false);
                }
                document.getElementById('detail').innerText = 'Game launched. Closing in 1 second.';
                // Ferme tout de suite pour ne pas voler le focus
                setTimeout(function() { window.close(); }, 500);
            } catch (err) {
                document.getElementById('detail').innerText = 'Failed to launch: ' + err.message;
            }
        }

        window.resizeTo(500, 280);
        window.moveTo(
            (screen.width - 500) / 2,
            (screen.height - 280) / 2);

        setTimeout(waitClose, 1000);
           </script>
        </body>
        </html>";
        }


        private static System.Collections.IEnumerator RestartGameDelayed(float warningSeconds)
        {
            yield return new UnityEngine.WaitForSeconds(warningSeconds);

            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                string exeDir = System.IO.Path.GetDirectoryName(exePath);
                bool isSteam = System.IO.File.Exists(System.IO.Path.Combine(exeDir, "steam_api64.dll"));
                int currentPid = Process.GetCurrentProcess().Id;

                string launchCmd = isSteam
                    ? "steam://rungameid/949230"
                    : exePath;

                string htaPath = Path.Combine(Path.GetTempPath(), "CameraTimelapseMod_restart.hta");
                string htaContent = BuildRestartHta(currentPid, launchCmd, isSteam);
                File.WriteAllText(htaPath, htaContent);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "mshta.exe",
                    Arguments = $"\"{htaPath}\"",
                    UseShellExecute = true
                });

                LogsTools.Info($"Restart HTA launched: {htaPath}");
                CoroutineSystem.Instance.StartCoroutine(QuitAfterDelay(1f));
            }
            catch (Exception ex)
            {
                LogsTools.Error($"RestartGameDelayed: {ex}");
            }
        }

        private static System.Collections.IEnumerator QuitAfterDelay(float seconds)
        {
            yield return new UnityEngine.WaitForSeconds(seconds);
            UnityEngine.Application.Quit();
        }

        public static void StartCrashWatchdog()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                string exeDir = Path.GetDirectoryName(exePath);
                bool isSteam = File.Exists(Path.Combine(exeDir, "steam_api64.dll"));
                int currentPid = Process.GetCurrentProcess().Id;

                string markerPath = Path.Combine(Mod.DataDir, "_session_alive.marker");
                File.WriteAllText(markerPath, DateTime.Now.ToString());

                string launchCmd = isSteam
                    ? "steam://rungameid/949230"
                    : exePath;

                string htaPath = Path.Combine(Path.GetTempPath(), "CameraTimelapseMod_watchdog.hta");
                string htaContent = BuildWatchdogHta(currentPid, markerPath, launchCmd, isSteam);
                File.WriteAllText(htaPath, htaContent);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "mshta.exe",
                    Arguments = $"\"{htaPath}\"",
                    UseShellExecute = true
                });

                LogsTools.Info($"Crash watchdog launched (HTA: {htaPath})");
            }
            catch (Exception ex)
            {
                LogsTools.Error($"StartCrashWatchdog failed: {ex.Message}");
            }
        }

        private static string BuildWatchdogHta(int pid, string markerPath, string launchCmd, bool isSteam)
        {
            string markerEsc = markerPath.Replace("\\", "\\\\").Replace("'", "\\'");
            string launchEsc = launchCmd.Replace("\\", "\\\\").Replace("'", "\\'");
            string isSteamJs = isSteam ? "true" : "false";

            return @"<!DOCTYPE html>
                <html>
                <head>
                    <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"">
                    <title>Auto Timelapse Mod - Crash Watchdog</title>
                    <HTA:APPLICATION
                        ID='watchdog'
                        APPLICATIONNAME='CameraTimelapseMod Watchdog'
                        BORDER='thin'
                        CAPTION='yes'
                        SHOWINTASKBAR='yes'
                        SINGLEINSTANCE='yes'
                        SYSMENU='yes'
                        WINDOWSTATE='minimize'
                        SCROLL='no'
                        INNERBORDER='no'
                        CONTEXTMENU='no'
                        SELECTION='no' />
                    <style>
                        body {
                            font-family: 'Segoe UI', Tahoma, sans-serif;
                            background: #1e2440;
                            color: #e8ecf5;
                            margin: 0;
                            padding: 30px;
                        }
                        h1 {
                            margin: 0 0 10px 0;
                            font-size: 18px;
                            color: #ffd166;
                        }
                        h2 {
                            margin: 0 0 10px 0;
                            font-size: 14px;
                            color: #ff8c66;
                            line-height: 1.4;
                            word-wrap: break-word;
                        }
                        body {
                            overflow-y: auto;
                        }
                        .subtitle {
                            font-size: 12px;
                            color: #8b95b8;
                            margin-bottom: 20px;
                        }
                        .status {
                            background: #14172a;
                            border-left: 3px solid #4ecdc4;
                            padding: 12px 15px;
                            margin: 15px 0;
                            font-size: 13px;
                            line-height: 1.5;
                        }
                        .info {
                            font-size: 11px;
                            color: #8b95b8;
                            margin-top: 20px;
                        }
                    </style>
                </head>
                <body>
                    <h1>Auto Timelapse Mod - Crash Watchdog</h1>
                    <div class='subtitle'>Monitoring Cities Skylines II</div>
                    <h2>Please go back/stay in game to record screenshots/videos correctly with fullscreen/OBS, but do not close this window. Do not forget to turn off radio, music and any sound of your computer to record videos.</h2>

                    <div class='status'>
                        <b id='state'>Watching...</b><br>
                        <span id='detail'>Game is running normally. The mod will restart the game if it crashes during your screenshot session.</span>
                    </div>

                    <div class='info'>
                        You can leave this window open or minimize it.<br>
                        It will close automatically when your screenshot session finishes.
                    </div>

                    <script language='JScript'>
                        var pid = " + pid + @";
                        var markerPath = '" + markerEsc + @"';
                        var launchCmd = '" + launchEsc + @"';
                        var isSteam = " + isSteamJs + @";
                        var fso = new ActiveXObject('Scripting.FileSystemObject');
                        var shell = new ActiveXObject('WScript.Shell');

                        function isProcessRunning(targetPid) {
                            try {
                                var wmi = GetObject('winmgmts:\\\\.\\root\\cimv2');
                                var procs = wmi.ExecQuery('SELECT * FROM Win32_Process WHERE ProcessId = ' + targetPid);
                                var e = new Enumerator(procs);
                                return !e.atEnd();
                            } catch (err) {
                                return false;
                            }
                        }

                        function tick() {
                            if (!fso.FileExists(markerPath)) {
                                document.getElementById('state').innerText = 'Session finished cleanly.';
                                document.getElementById('detail').innerText = 'This window will close in 3 seconds.';
                                setTimeout(function() { window.close(); }, 3000);
                                return;
                            }

                            if (!isProcessRunning(pid)) {
                                document.getElementById('state').innerText = 'Game crashed!';
                                document.getElementById('detail').innerText = 'Restarting Cities Skylines II in 5 seconds...';
                                setTimeout(relaunch, 5000);
                                return;
                            }

                            setTimeout(tick, 2000);
                        }

                       function relaunch() {
                            try {
                                var q = String.fromCharCode(34);
                                if (isSteam) {
                                    shell.Run(launchCmd, 1, false);
                                } else {
                                    shell.Run(q + launchCmd + q, 1, false);
                                }
                                document.getElementById('detail').innerText = 'Game launched. Closing in 1 second.';
                                // Ferme tout de suite pour ne pas voler le focus
                                setTimeout(function() { window.close(); }, 500);
                            } catch (err) {
                                document.getElementById('detail').innerText = 'Failed to launch: ' + err.message;
                            }
                        }

                        window.resizeTo(500, 320);
                window.moveTo(
                    (screen.width - 500) / 2,
                    (screen.height - 320) / 2);

                setTimeout(tick, 1000);
                    </script>
        </body>
        </html>";
        }

        public static void StopCrashWatchdog()
        {
            try
            {
                string markerPath = Path.Combine(Mod.DataDir, "_session_alive.marker");
                if (File.Exists(markerPath))
                {
                    File.Delete(markerPath);
                    LogsTools.Info("Crash watchdog marker removed (clean shutdown)");
                }
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"StopCrashWatchdog failed: {ex.Message}");
            }
        }



        public static void OpenFolder(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch { }
        }



        public static string FormatHourLabel(float t)
        {
            if (float.IsNaN(t)) return "presetTime";
            int hh = Mathf.FloorToInt(t);
            int mm = Mathf.FloorToInt((t - hh) * 60f);
            return $"{hh:D2}h{(mm > 0 ? mm.ToString("D2") : "")}";
        }

        public static List<float> ParseTimes(string raw)
        {
            var times = new List<float>();
            if (string.IsNullOrEmpty(raw)) return times;

            foreach (var part in raw.Split(','))
            {
                var s = part.Trim().Replace(',', '.');
                if (string.IsNullOrEmpty(s)) continue;
                if (float.TryParse(s, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float h))
                {
                    h = Mathf.Clamp(h, 0f, 24f);
                    if (h >= 24f) h = 0f;
                    times.Add(h);
                }
            }
            return times;
        }

        public static string getScreenshotFolder()
        {
            string custom = Mod.Setting?.ScreenshotFolderOverride ?? "";
            if (!string.IsNullOrWhiteSpace(custom))
                return custom;
            return Path.Combine(Mod.DataDir, "Screenshots");
        }

        public static string getVideosFolder()
        {
            string custom = Mod.Setting?.VideoFolderOverride ?? "";
            if (!string.IsNullOrWhiteSpace(custom))
                return custom;
            return Path.Combine(Mod.DataDir, "Videos");
        }
        public static void OpenVideosFolder()
        {
            try
            {
                string folder = Tools.getVideosFolder();
                Directory.CreateDirectory(folder);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true,
                    Verb = "open"
                });

                LogsTools.Info($"Opened screenshot folder: {folder}");
            }
            catch (Exception ex)
            {
                LogsTools.Error($"OpenScreenshotFolder failed: {ex.Message}");
            }
        }

        public static void OpenScreenshotFolder()
        {
            try
            {
                string folder = Tools.getScreenshotFolder();
                Directory.CreateDirectory(folder);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true,
                    Verb = "open"
                });

                LogsTools.Info($"Opened screenshot folder: {folder}");
            }
            catch (Exception ex)
            {
                LogsTools.Error($"OpenScreenshotFolder failed: {ex.Message}");
            }
        }

    }
}
