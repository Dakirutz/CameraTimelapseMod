using System;
using System.IO;

namespace CameraTimelapseMod.Util
{
    public static class LogsTools
    {
        private static readonly object _lock = new object();
        private static string _filePath;
        private static StreamWriter _writer;


        private static bool _includeDebug = false;  // not used yet

        public static void Init()
        {
            try
            {
                Directory.CreateDirectory(Mod.DataDir);
                _filePath = Path.Combine(Mod.DataDir, "CameraTimelapseMod.log");

                _writer = new StreamWriter(_filePath, append: true)
                {
                    AutoFlush = true
                };

                WriteLine("INFO", $"=== CameraTimelapseMod log opened at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            }
            catch (Exception ex)
            {
                Mod.log.Warn($"CameraTimelapseMod log init failed: {ex.Message}");
            }
        }

        public static void Debug(string msg)
        {
            if (_includeDebug) Mod.log.Debug(msg);
            if (_includeDebug) WriteLine("DEBUG", msg);
        }

        public static void Shutdown()
        {
            try
            {
                lock (_lock)
                {
                    WriteLine("INFO", "=== CameraTimelapseMod log closing ===");
                    _writer?.Dispose();
                    _writer = null;
                }
            }
            catch { }
        }

        public static void Info(string msg)
        {
            Mod.log.Info(msg);
            WriteLine("INFO", msg);
        }

        public static void Warn(string msg)
        {
            Mod.log.Warn(msg);
            WriteLine("WARN", msg);
        }

        public static void Error(string msg)
        {
            Mod.log.Error(msg);
            WriteLine("ERROR", msg);
        }

        public static void Error(Exception ex, string msg = null)
        {
            string full = msg == null ? ex.ToString() : $"{msg} :: {ex}";
            Mod.log.Error(full);
            WriteLine("ERROR", full);
        }

        private static void WriteLine(string level, string msg)
        {
            try
            {
                lock (_lock)
                {
                    if (_writer == null) return;
                    _writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {msg}");
                }
            }
            catch { 
            
            }
        }
    }
}