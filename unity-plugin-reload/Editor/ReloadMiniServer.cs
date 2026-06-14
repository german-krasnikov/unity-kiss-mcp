// ReloadMiniServer — lightweight TCP server for the reload package.
// Background Thread (not async). Single-client. 4-byte BE length prefix + JSON.
// Read-only commands execute inline; main-thread commands enqueue via ConcurrentQueue.
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Reload
{
    public static class ReloadMiniServer
    {
        private static TcpListener _listener;
        private static Thread _thread;
        private static volatile bool _running;
        private static volatile bool _starting;
        private static volatile bool _shuttingDown;

        // Main-thread work queue — drained by EditorApplication.update via ReloadPlugin.
        public static readonly ConcurrentQueue<Action> UpdateQueue = new ConcurrentQueue<Action>();

        public static int ActualPort { get; private set; }

        // Idempotent start. Retries ports [port..port+50] — does not trust stale config port.
        public static void Start(int port)
        {
            if (_running || _starting) return;
            _starting = true;
            _shuttingDown = false;
            try
            {
                var (l, actualPort) = ReloadBinder.BindListener(port, port + 50);
                _listener = l;
                ActualPort = actualPort;
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[Reload] Failed to start mini-server (range {port}-{port + 50}): {e.Message}");
                _starting = false;
                ActualPort = 0;
                return;
            }

            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "ReloadMiniServer" };
            _running = true;
            _starting = false;
            _thread.Start();
            Debug.Log($"[Reload] Mini-server started on port {ActualPort}");
        }

        public static void Stop()
        {
            _shuttingDown = true;
            _running = false;
            try { _listener?.Stop(); } catch { }
            _listener = null;
            _thread = null; // IsBackground=true — CLR won't wait; _listener.Stop() unblocks AcceptLoop
        }

        private static void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
                }
                catch (SocketException) when (!_running) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception e)
                {
                    if (_running) Debug.LogWarning($"[Reload] Accept error: {e.Message}");
                }
            }
        }

        private static void HandleClient(TcpClient client)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var header = new byte[4];
                    while (_running)
                    {
                        if (!ReadExact(stream, header)) break;
                        var length = (uint)((header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3]);
                        if (length > 1_000_000) break;

                        var payload = new byte[length];
                        if (!ReadExact(stream, payload)) break;

                        var json = Encoding.UTF8.GetString(payload);
                        var cmd = ExtractString(json, "cmd");
                        var id  = ExtractString(json, "id") ?? "";
                        var args = ExtractArgs(json);

                        var response = DispatchCommand(cmd, args, id, stream);
                        if (response != null)
                            SendFrame(stream, response);
                    }
                }
            }
            catch (Exception e)
            {
                if (_running) Debug.LogWarning($"[Reload] Client error: {e.Message}");
            }
        }

        // Returns null when response is sent asynchronously (main-thread commands handle it).
        public static string DispatchCommand(string cmd, string args, string id, NetworkStream stream = null)
        {
            switch (cmd)
            {
                case "ping":         return OkResponse(id, "pong");
                case "get_version":  return OkResponse(id, ReloadDomainStamp.ComputeStamp());
                case "diagnose":     return OkResponse(id, ReloadDiagnoseCommand.Execute(args));
                case "sync_status":  return OkResponse(id, ReloadCommands.Dispatch("sync_status"));
                case "force_refresh":
                case "recompile":
                    return EnqueueMainThread(cmd, id, stream);
                default:
                    return ErrResponse(id, $"unknown command: {cmd}");
            }
        }

        private static string EnqueueMainThread(string cmd, string id, NetworkStream stream)
        {
            if (stream == null)
                return ErrResponse(id, "main thread not available in test context");

            var mre = new ManualResetEventSlim(false);
            string result = null;
            // M1: abandonment flag — prevents phantom mutations after 5s timeout.
            // Ported from master:MCPServer.cs:383 guard pattern.
            int abandoned = 0;
            // M1: sent flag — exactly one SendFrame wins between lambda and timeout path.
            int sent = 0;

            UpdateQueue.Enqueue(() =>
            {
                // Guard: if shutting down OR timed-out caller already gave up, don't dispatch.
                if (_shuttingDown || Volatile.Read(ref abandoned) != 0)
                {
                    mre.Set();
                    return;
                }
                try
                {
                    var data = ReloadCommands.Dispatch(cmd);
                    result = OkResponse(id, data);
                }
                catch (Exception e)
                {
                    result = ErrResponse(id, e.Message);
                }
                finally
                {
                    mre.Set();
                    if (result != null && Interlocked.Exchange(ref sent, 1) == 0)
                        SendFrame(stream, result);
                }
            });

            if (!mre.Wait(5000))
            {
                // M1: signal abandonment BEFORE the lambda drains — prevents side-effect after timeout.
                Interlocked.Exchange(ref abandoned, 1);
                result = ErrResponse(id, "main thread timeout (5s)");
                if (Interlocked.Exchange(ref sent, 1) == 0)
                    SendFrame(stream, result);
            }
            return null; // caller must not send again
        }

        private static void SendFrame(NetworkStream stream, string json)
        {
            try
            {
                var payload = Encoding.UTF8.GetBytes(json);
                var frame = new byte[4 + payload.Length];
                var len = (uint)payload.Length;
                frame[0] = (byte)(len >> 24);
                frame[1] = (byte)(len >> 16);
                frame[2] = (byte)(len >> 8);
                frame[3] = (byte)(len);
                Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
                stream.Write(frame, 0, frame.Length);
                stream.Flush();
            }
            catch { }
        }

        private static bool ReadExact(NetworkStream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read == 0) return false;
                total += read;
            }
            return true;
        }

        // Minimal JSON helpers — no dep on UnityMCP.Editor.JsonHelper.
        public static string OkResponse(string id, string data)
            => $"{{\"id\":\"{Esc(id)}\",\"ok\":true,\"data\":\"{Esc(data)}\"}}";

        public static string ErrResponse(string id, string error)
            => $"{{\"id\":\"{Esc(id)}\",\"ok\":false,\"err\":\"{Esc(error)}\"}}";

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private static string ExtractString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var needle = $"\"{key}\"";
            var idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += needle.Length;
            while (idx < json.Length && json[idx] != ':') idx++;
            idx++; // skip ':'
            while (idx < json.Length && json[idx] == ' ') idx++;
            if (idx >= json.Length) return null;
            if (json[idx] == '"')
            {
                idx++;
                var sb = new StringBuilder();
                while (idx < json.Length && json[idx] != '"')
                {
                    if (json[idx] == '\\') { idx++; if (idx < json.Length) sb.Append(json[idx]); }
                    else sb.Append(json[idx]);
                    idx++;
                }
                return sb.ToString();
            }
            return null;
        }

        private static string ExtractArgs(string json)
        {
            var needle = "\"args\"";
            var idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) return "{}";
            idx += needle.Length;
            while (idx < json.Length && json[idx] != ':') idx++;
            idx++;
            while (idx < json.Length && json[idx] == ' ') idx++;
            if (idx >= json.Length || json[idx] != '{') return "{}";
            int depth = 0, start = idx;
            for (; idx < json.Length; idx++)
            {
                if (json[idx] == '{') depth++;
                else if (json[idx] == '}') { if (--depth == 0) return json.Substring(start, idx - start + 1); }
            }
            return "{}";
        }
    }
}
