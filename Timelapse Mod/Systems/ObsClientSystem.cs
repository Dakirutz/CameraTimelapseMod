using CameraTimelapseMod.Util;
using System;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CameraTimelapseMod.Systems
{
    public static class ObsClientSystem
    {
        private static ClientWebSocket _ws;
        private static bool _connected = false;
        private static int _requestCounter = 0;
        private static CancellationTokenSource _receiveLoopCts;

        public static bool IsConnected => _connected;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource<bool>>
            _pendingRequests = new System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource<bool>>();

        public static async Task TestConnection()
        {
            string host = Mod.Setting?.ObsHost ?? "localhost";
            int port = Mod.Setting?.ObsPortInt ?? 4455;
            string password = Mod.Setting?.ObsPassword ?? "";

            LogsTools.Info($"OBS test: connecting to {host}:{port}");

            bool ok = await Connect(host, port, password);
            if (ok)
            {
                LogsTools.Info("OBS test: connection successful");
                UITools.ShowMessage(
                    "OBS Test",
                    $"Successfully connected to OBS at {host}:{port}.\n\n" +
                    "Recording will work during timelapse sessions.");
                await Disconnect();
            }
            else
            {
                LogsTools.Warn("OBS test: connection failed");
                UITools.ShowMessage(
                    "OBS Test",
                    $"Could not connect to OBS at {host}:{port}.\n\n" +
                    "Checklist:\n" +
                    "- OBS Studio is running\n" +
                    "- Tools → WebSocket Server Settings → Enable WebSocket Server\n" +
                    "- Port matches your settings (default 4455)\n" +
                    "- Password matches (or empty in both)\n" +
                    "- No firewall blocking localhost connections");
            }
        }

        public static async Task<bool> Connect(string host, int port, string password)
        {
            if (_connected) return true;

            try
            {
                _ws = new ClientWebSocket();
                var uri = new Uri($"ws://{host}:{port}");

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    await _ws.ConnectAsync(uri, cts.Token);
                }

                string hello = await ReceiveText(TimeSpan.FromSeconds(5));
                if (string.IsNullOrEmpty(hello))
                {
                    LogsTools.Warn("OBS Connect: no Hello received");
                    Cleanup();
                    return false;
                }

                string authToken = null;
                if (TryExtractAuth(hello, out string challenge, out string salt))
                {
                    if (string.IsNullOrEmpty(password))
                    {
                        LogsTools.Warn("OBS Connect: server requires password but none provided");
                        Cleanup();
                        return false;
                    }
                    authToken = ComputeAuthString(password, salt, challenge);
                }

                string identify = BuildIdentify(authToken);
                await SendText(identify);

                string response = await ReceiveText(TimeSpan.FromSeconds(5));
                if (string.IsNullOrEmpty(response) || !response.Contains("\"op\":2"))
                {
                    LogsTools.Warn($"OBS Connect: identify failed, response = {response}");
                    Cleanup();
                    return false;
                }

                _connected = true;
                LogsTools.Info($"OBS Connect: identified at {host}:{port}");

                _receiveLoopCts = new CancellationTokenSource();
                _ = Task.Run(() => ReceiveLoop(_receiveLoopCts.Token));

                return true;
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"OBS Connect failed: {ex.Message}");
                Cleanup();
                return false;
            }
        }

        public static async Task Disconnect()
        {
            if (!_connected || _ws == null) return;
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token);
                }
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"OBS Disconnect: {ex.Message}");
            }
            finally
            {
                Cleanup();
            }
        }

        public static async Task<bool> StartRecording()
        {
            return await SendRequest("StartRecord");
        }

        public static async Task<bool> StopRecording()
        {
            return await SendRequest("StopRecord");
        }

        public static async Task<bool> SetSceneByName(string sceneName)
        {
            string id = NextRequestId();
            string body = $"{{\"sceneName\":\"{Tools.Escape(sceneName)}\"}}";
            return await SendRequestWithBody("SetCurrentProgramScene", body, id);
        }

        private static async Task<bool> SendRequest(string requestType)
        {
            if (!_connected) return false;

            string id = NextRequestId();
            string msg =
                $"{{\"op\":6,\"d\":{{\"requestType\":\"{requestType}\",\"requestId\":\"{id}\"}}}}";

            var tcs = new TaskCompletionSource<bool>();
            _pendingRequests[id] = tcs;

            try
            {
                await SendText(msg);
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"OBS {requestType} send failed: {ex.Message}");
                _pendingRequests.TryRemove(id, out _);
                return false;
            }

            var timeoutTask = Task.Delay(5000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            _pendingRequests.TryRemove(id, out _);

            if (completedTask == timeoutTask)
            {
                LogsTools.Warn($"OBS {requestType} timeout (no response in 5s)");
                return false;
            }

            return await tcs.Task;
        }

        private static async Task<bool> SendRequestWithBody(string requestType, string bodyJson, string id)
        {
            if (!_connected) return false;

            string msg =
                $"{{\"op\":6,\"d\":{{\"requestType\":\"{requestType}\"," +
                $"\"requestId\":\"{id}\",\"requestData\":{bodyJson}}}}}";

            var tcs = new TaskCompletionSource<bool>();
            _pendingRequests[id] = tcs;

            try
            {
                await SendText(msg);
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"OBS {requestType} send failed: {ex.Message}");
                _pendingRequests.TryRemove(id, out _);
                return false;
            }

            var timeoutTask = Task.Delay(5000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            _pendingRequests.TryRemove(id, out _);

            if (completedTask == timeoutTask)
            {
                LogsTools.Warn($"OBS {requestType} timeout (no response in 5s)");
                return false;
            }

            bool result = await tcs.Task;
            LogsTools.Info($"OBS {requestType} (id={id}) → {result}");
            return result;
        }

        private static string NextRequestId()
        {
            int id = System.Threading.Interlocked.Increment(ref _requestCounter);
            return $"hm2-{id}";
        }

        private static string BuildIdentify(string authToken)
        {
            if (string.IsNullOrEmpty(authToken))
                return "{\"op\":1,\"d\":{\"rpcVersion\":1}}";
            return $"{{\"op\":1,\"d\":{{\"rpcVersion\":1,\"authentication\":\"{authToken}\"}}}}";
        }

        private static bool TryExtractAuth(string helloJson, out string challenge, out string salt)
        {
            challenge = null;
            salt = null;
            if (string.IsNullOrEmpty(helloJson)) return false;

            int authIdx = helloJson.IndexOf("\"authentication\"", StringComparison.Ordinal);
            if (authIdx < 0) return false;

            challenge = ExtractJsonString(helloJson, "challenge", authIdx);
            salt = ExtractJsonString(helloJson, "salt", authIdx);

            return !string.IsNullOrEmpty(challenge) && !string.IsNullOrEmpty(salt);
        }

        private static string ExtractJsonString(string json, string key, int startFrom)
        {
            string needle = $"\"{key}\":\"";
            int i = json.IndexOf(needle, startFrom, StringComparison.Ordinal);
            if (i < 0) return null;
            i += needle.Length;
            int end = json.IndexOf('"', i);
            if (end < 0) return null;
            return json.Substring(i, end - i);
        }
        private static string ComputeAuthString(string password, string salt, string challenge)
        {
            using (var sha = SHA256.Create())
            {
                byte[] step1 = sha.ComputeHash(Encoding.UTF8.GetBytes(password + salt));
                string secret = Convert.ToBase64String(step1);
                byte[] step2 = sha.ComputeHash(Encoding.UTF8.GetBytes(secret + challenge));
                return Convert.ToBase64String(step2);
            }
        }
        private static async Task SendText(string text)
        {
            if (_ws == null) return;
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                await _ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    cts.Token);
            }
        }

        private static async Task<string> ReceiveText(TimeSpan timeout)
        {
            if (_ws == null) return null;
            var buffer = new byte[8192];
            var sb = new StringBuilder();

            using (var cts = new CancellationTokenSource(timeout))
            {
                while (true)
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) return null;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (result.EndOfMessage) break;
                }
            }
            return sb.ToString();
        }

        private static void Cleanup()
        {
            try { _receiveLoopCts?.Cancel(); } catch { }
            _receiveLoopCts = null;

            try { _ws?.Dispose(); } catch { }
            _ws = null;
            _connected = false;

            foreach (var kv in _pendingRequests)
            {
                try { kv.Value.TrySetResult(false); } catch { }
            }
            _pendingRequests.Clear();
        }

        private static async Task ReceiveLoop(CancellationToken ct)
        {
            var buffer = new byte[16384];
            var sb = new StringBuilder();

            try
            {
                while (!ct.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
                {
                    sb.Clear();

                    while (!ct.IsCancellationRequested)
                    {
                        WebSocketReceiveResult result;
                        try
                        {
                            result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        }
                        catch (OperationCanceledException) { return; }
                        catch (Exception ex)
                        {
                            LogsTools.Warn($"OBS receive loop error: {ex.Message}");
                            return;
                        }

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            LogsTools.Info("OBS closed the connection");
                            return;
                        }

                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                        if (result.EndOfMessage) break;
                    }

                    string msg = sb.ToString();
                    HandleIncomingMessage(msg);
                }
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"OBS receive loop crashed: {ex.Message}");
            }
        }

        private static void HandleIncomingMessage(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                if (!json.Contains("\"op\":7")) return;

                string requestId = ExtractJsonString(json, "requestId", 0);
                if (string.IsNullOrEmpty(requestId)) return;

                bool result = ExtractResultBool(json);
                string comment = ExtractJsonString(json, "comment", 0);
                int code = ExtractCode(json);

                if (!result)
                {
                    LogsTools.Warn(
                        $"[OBS] Request {requestId} failed: code={code}" +
                        (string.IsNullOrEmpty(comment) ? "" : $", comment='{comment}'"));
                }

                if (_pendingRequests.TryGetValue(requestId, out var tcs))
                {
                    tcs.TrySetResult(result);
                }
            }
            catch (Exception ex)
            {
                LogsTools.Warn($"OBS HandleIncomingMessage failed: {ex.Message}");
            }
        }

        private static bool ExtractResultBool(string json)
        {
            int idx = json.IndexOf("\"result\":", StringComparison.Ordinal);
            if (idx < 0) return false;
            idx += 9;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
            return idx + 4 <= json.Length
                && json.Substring(idx, 4).Equals("true", StringComparison.Ordinal);
        }

        private static int ExtractCode(string json)
        {
            int idx = json.IndexOf("\"code\":", StringComparison.Ordinal);
            if (idx < 0) return -1;
            idx += 7;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
            int end = idx;
            while (end < json.Length && char.IsDigit(json[end])) end++;
            if (end == idx) return -1;
            int.TryParse(json.Substring(idx, end - idx), out int code);
            return code;
        }

        public static bool LastConnectFailed { get; private set; } = false;

        public static System.Collections.IEnumerator ConnectIfEnabledCoroutine()
        {
            LastConnectFailed = false;

            if (!(Mod.Setting?.VideoRecordingEnabled ?? false)) yield break;
            if (_connected) yield break;

            var task = Connect(
                Mod.Setting.ObsHost ?? "localhost",
                Mod.Setting.ObsPortInt,
                Mod.Setting.ObsPassword ?? "");
            yield return new UnityEngine.WaitUntil(() => task.IsCompleted);

            if (!task.Result)
            {
                LastConnectFailed = true;
                LogsTools.Warn("OBS connect failed at session start");
            }
        }
        public static System.Collections.IEnumerator DisconnectCoroutine()
        {
            if (!_connected) yield break;
            var task = Disconnect();
            yield return new UnityEngine.WaitUntil(() => task.IsCompleted);
        }

        public static System.Collections.IEnumerator RecordWithSimulationCoroutine(string recordDirectory = null)
        {
            bool videoEnabled = (Mod.Setting?.VideoRecordingEnabled ?? false) && _connected;
            if (!videoEnabled) yield break;

            int durationSec = Mod.Setting?.VideoRecordSeconds ?? 5;
            if (durationSec <= 0) yield break;  

            var simSpeedSetting = Mod.Setting?.VideoSimulationSpeed ?? Setting.SimulationSpeed.Normal_x2;



            GameTools.SetSimulationSpeed((int)simSpeedSetting);

            if (!string.IsNullOrEmpty(recordDirectory))
            {
                try { System.IO.Directory.CreateDirectory(recordDirectory); } catch { }
                var dirTask = SetRecordDirectory(recordDirectory);
                yield return new UnityEngine.WaitUntil(() => dirTask.IsCompleted);
                if (!dirTask.Result)
                    LogsTools.Warn("OBS SetRecordDirectory failed (need OBS 30.0+ with websocket 5.3+)");
            }

            var startTask = StartRecording();
            yield return new UnityEngine.WaitUntil(() => startTask.IsCompleted);
            if (!startTask.Result)
            {
                LogsTools.Warn("OBS StartRecord failed, continuing without video");
                yield break;
            }

            float waited = 0f;
            while (waited < durationSec)
            {
                yield return new UnityEngine.WaitForSeconds(1f);
                waited += 1f;
            }

            var stopTask = StopRecording();
            yield return new UnityEngine.WaitUntil(() => stopTask.IsCompleted);
        }

        public static async Task<bool> SetRecordDirectory(string path)
        {
            string id = NextRequestId();
            string body = $"{{\"recordDirectory\":\"{Tools.Escape(path)}\"}}";
            LogsTools.Info($"[OBS] SetRecordDirectory request: path='{path}', body='{body}'");

            bool result = await SendRequestWithBody("SetRecordDirectory", body, id);
            LogsTools.Info($"[OBS] SetRecordDirectory result: {result} for path='{path}'");

            return result;
        }

    }
}