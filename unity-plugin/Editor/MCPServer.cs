using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    public static class MCPServer
    {
        // ── Per-port client state ─────────────────────────────────────────────
        private sealed class ClientSlot
        {
            internal volatile bool Connected;
            internal volatile TcpClient Client;
            internal volatile CancellationTokenSource Cts;
            internal long Generation;  // OK — Interlocked
        }

        private static TcpListener _listener;
        private static TcpListener _chatListener;
        private static CancellationTokenSource _cts;
        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        private static readonly ClientSlot _mainSlot = new ClientSlot();
        private static readonly ClientSlot _chatSlot = new ClientSlot();
        private static volatile bool _shuttingDown;
        private static volatile bool _starting;
        private static volatile bool _isCompiling;
        private static readonly string PortFilePath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "MCP_Port.json"));
        private static readonly int Port = GetPort();
        private static readonly int ChatPort = GetChatPort();
        private static DateTime _compileStartTime;

        private static string ReadPortFileOrNull()
        {
            try { return File.Exists(PortFilePath) ? File.ReadAllText(PortFilePath) : null; }
            catch { return null; }
        }

        private static int GetPort()
        {
            var env = System.Environment.GetEnvironmentVariable("UNITY_MCP_PORT");
            return PortResolver.ResolvePort(env, ReadPortFileOrNull(), 9500);
        }

        private static int GetChatPort()
        {
            var env = System.Environment.GetEnvironmentVariable("UNITY_MCP_CHAT_PORT");
            return PortResolver.ResolveChatPort(env, ReadPortFileOrNull(), Port, Port + 1);
        }

        public static void SavePorts(int port, int chatPort)
            => PortResolver.SavePorts(PortFilePath, port, chatPort);

        private const int MaxMessageSize = 10_000_000;

        // Per-command timeout overrides (seconds). Default: 25s.
        private static readonly System.Collections.Generic.Dictionary<string, int> CommandTimeouts =
            new System.Collections.Generic.Dictionary<string, int>
            {
                { "run_tests", 130 },
                { "run_playtest", 130 },
                { "batch", 65 },
                { "wait_until", 30 },
                { "move_to", 30 },
                { "test_step", 30 },
            };

        private static int GetCommandTimeout(string cmd)
        {
            return CommandTimeouts.TryGetValue(cmd, out var t) ? t : 25;
        }

        public static bool IsRunning
        {
            get
            {
                try
                {
                    var l = _listener;
                    return l != null && l.Server != null && l.Server.IsBound;
                }
                catch { return false; }  // ObjectDisposedException during teardown
            }
        }

        public static bool IsChatListenerRunning
        {
            get
            {
                try
                {
                    var l = _chatListener;
                    return l != null && l.Server != null && l.Server.IsBound;
                }
                catch { return false; }
            }
        }
        public static bool IsClientConnected => _mainSlot.Connected || _chatSlot.Connected;
        public static int ServerPort => Port;
        public static int ServerChatPort => ChatPort;

        private static double _lastWatchdogCheck;

        static MCPServer()
        {
            // Persist auto-assigned ports so they survive domain reload
            SavePorts(Port, ChatPort);
            EditorApplication.update += ProcessMainThreadQueue;
            EditorApplication.update += WatchdogTick;
            EditorApplication.quitting += OnQuit;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            CompilationPipeline.compilationStarted += _ => { _isCompiling = true; _compileStartTime = DateTime.UtcNow; WriteStateFile("compiling"); };
            CompilationPipeline.compilationFinished += _ =>
            {
                _isCompiling = false;
                EditorApplication.delayCall += () =>
                {
                    if (!EditorApplication.isCompiling && !_shuttingDown)
                        WriteStateFile(IsRunning ? "ready" : "restarting");
                };
            };
            EditorSceneManager.sceneOpened += (_, _) => RefManager.Invalidate();
            EditorSceneManager.newSceneCreated += (_, _, _) => RefManager.Invalidate();
            // Delay async start until first update — SynchronizationContext dead in static ctor
            EditorApplication.delayCall += () => StartAsync();
        }

        private static void WatchdogTick()
        {
            if (_shuttingDown) return;
            if (EditorApplication.timeSinceStartup - _lastWatchdogCheck < 5.0) return;
            _lastWatchdogCheck = EditorApplication.timeSinceStartup;
            if (!IsRunning && !EditorApplication.isCompiling)
                StartAsync();
        }

        public static async void StartAsync()
        {
            // _starting closes the re-entrancy gap: during the bind-retry await window
            // IsRunning is false, so WatchdogTick could otherwise launch a 2nd StartAsync
            // → duplicate _cts/accept-loop churn.
            if (IsRunning || _starting) return;
            _starting = true;
            _shuttingDown = false;
            CommandRouter.EnsureEnabledToolsCacheWarm();  // #29: warm tool cache on main thread before serving the read-thread fast-path
            // Tier 0: re-register idempotently so restart after Stop() works
            EditorApplication.update -= ProcessMainThreadQueue;
            EditorApplication.update += ProcessMainThreadQueue;
            EditorApplication.update -= WatchdogTick;
            EditorApplication.update += WatchdogTick;
            CancellationTokenSource cts = null;
            try
            {
                cts = new CancellationTokenSource();
                _cts = cts;
                var token = cts.Token; // local copy — safe from null after Stop()/OnBeforeReload()

                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        _listener = new TcpListener(IPAddress.Loopback, Port);
                        _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                        _listener.Start();
                        break;
                    }
                    catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        try { _listener?.Stop(); } catch { }
                        _listener = null;
                        if (attempt == 4) throw;
                        Debug.LogWarning($"[MCP] Port {Port} busy, retry {attempt + 1}/5...");
                        await Task.Delay(500 * (attempt + 1), token);
                    }
                }

                // Chat listener — best-effort (non-fatal if chat port is unavailable)
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        _chatListener = new TcpListener(IPAddress.Loopback, ChatPort);
                        _chatListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                        _chatListener.Start();
                        break;
                    }
                    catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        try { _chatListener?.Stop(); } catch { }
                        _chatListener = null;
                        if (attempt == 2)
                        {
                            Debug.LogWarning($"[MCP] Chat port {ChatPort} unavailable after 3 attempts: {se.Message}");
                            break;
                        }
                        Debug.LogWarning($"[MCP] Chat port {ChatPort} busy, retry {attempt + 1}/3...");
                        await Task.Delay(300 * (attempt + 1), token);
                    }
                    catch (SocketException se)
                    {
                        Debug.LogWarning($"[MCP] Chat port {ChatPort} unavailable: {se.Message}");
                        _chatListener = null;
                        break;
                    }
                }

                Debug.Log($"[MCP] Server started on port {Port} (chat: {ChatPort})");
                WritePortFile(Port);
                WriteStateFile("ready");

                // Run both accept loops concurrently — chat loop is optional
                var mainLoop = RunAcceptLoop(_listener, _mainSlot, "CLI", cts, token);
                var chatLoop = _chatListener != null
                    ? RunAcceptLoop(_chatListener, _chatSlot, "Chat", cts, token)
                    : Task.CompletedTask;
                await Task.WhenAll(mainLoop, chatLoop);
            }
            catch (Exception e)
            {
                if (!_shuttingDown)
                {
                    Debug.LogError($"[MCP] Server error: {e.Message}");
                }
                // Clean up on bind failure — prevent CTS/listener leak
                if (!IsRunning)
                {
                    try { cts?.Dispose(); } catch { }
                    if (_cts == cts) _cts = null;
                    try { _listener?.Stop(); } catch { }
                    _listener = null;
                }
            }
            finally { _starting = false; }
        }

        private static async Task RunAcceptLoop(TcpListener listener, ClientSlot slot, string label,
            CancellationTokenSource masterCts, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(); }
                catch (ObjectDisposedException) { break; }
                catch (Exception e)
                {
                    if (token.IsCancellationRequested) break;
                    Debug.LogError($"[MCP] {label} accept error: {e.Message}");
                    if (_cts != masterCts || !IsRunning) break;
                    await Task.Delay(100, token);
                    continue;
                }

                // Cancel old handler for THIS slot only — leaves the other slot untouched
                if (slot.Connected && slot.Client != null)
                {
                    Debug.Log($"[MCP] {label}: new client — disconnecting previous");
                    try { slot.Cts?.Cancel(); } catch { }
                    try { slot.Client.Close(); } catch { }
                }

                try { client.NoDelay = true; } catch { }
                ApplyKeepAlive(client.Client);
                slot.Client = client;
                var gen = Interlocked.Increment(ref slot.Generation);
                slot.Cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                _ = HandleClientAsync(client, slot, gen, label, slot.Cts.Token);
            }
        }

        private static void ApplyKeepAlive(Socket socket)
        {
            try { socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true); } catch { }
            // Platform-specific keepalive tuning (idle=60s, interval=10s, count=3)
            // Detects dead peers within ~90s. Relaxed from 10s/5s/3 to survive
            // macOS App Nap timer coalescing when Unity is in background.
            // App-level heartbeat (15s) handles faster detection for normal cases.
#if UNITY_EDITOR_OSX
            try { socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)0x10, 60); } catch { }   // TCP_KEEPALIVE (idle)
            try { socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)0x101, 10); } catch { }  // TCP_KEEPINTVL
            try { socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)0x102, 3); } catch { }   // TCP_KEEPCNT
#elif UNITY_EDITOR_WIN
            // Windows: use SIO_KEEPALIVE_VALS via IOControl
            try
            {
                var vals = new byte[12];
                BitConverter.GetBytes(1).CopyTo(vals, 0);       // on
                BitConverter.GetBytes(60000).CopyTo(vals, 4);   // idle ms
                BitConverter.GetBytes(10000).CopyTo(vals, 8);   // interval ms
                socket.IOControl(IOControlCode.KeepAliveValues, vals, null);
            }
            catch { }
#elif UNITY_EDITOR_LINUX
            try { socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)4, 60); } catch { }    // TCP_KEEPIDLE
            try { socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)5, 10); } catch { }    // TCP_KEEPINTVL
            try { socket.SetSocketOption(SocketOptionLevel.Tcp, (SocketOptionName)6, 3); } catch { }     // TCP_KEEPCNT
#endif
        }

        private static async Task HandleClientAsync(TcpClient client, ClientSlot slot, long generation,
            string label, CancellationToken clientToken)
        {
            Debug.Log($"[MCP] {label} client connected");
            slot.Connected = true;
            RefManager.Invalidate();
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var header = new byte[4];

                    while (client.Connected && !clientToken.IsCancellationRequested)
                    {
                        if (!await ReadExactAsync(stream, header, clientToken))
                            break;

                        var length = BinaryPrimitives.ReadUInt32BigEndian(header);
                        if (length > MaxMessageSize)
                        {
                            Debug.LogWarning($"[MCP] Protocol desync: length prefix {length} bytes (0x{length:X8}) exceeds {MaxMessageSize} — reconnecting");
                            break;
                        }

                        var payload = new byte[length];
                        if (!await ReadExactAsync(stream, payload, clientToken))
                            break;

                        var json = Encoding.UTF8.GetString(payload);

                        // Fast-path: ping/get_version/status bypass main thread (works even when Editor is busy)
                        var cmdName = JsonHelper.ExtractString(json, "cmd");
                        var msgId = JsonHelper.ExtractString(json, "id");
                        if (cmdName == "ping")
                        {
                            await SendAsync(stream, JsonHelper.FormatResponse(msgId, true, "pong", null), clientToken);
                            continue;
                        }
                        if (cmdName == "get_version")
                        {
                            await SendAsync(stream, JsonHelper.FormatResponse(msgId, true, "1.0", null), clientToken);
                            continue;
                        }
                        if (cmdName == "status")
                        {
                            var isCompiling = _isCompiling;
                            var elapsed = isCompiling ? (DateTime.UtcNow - _compileStartTime).TotalSeconds : 0.0;
                            await SendAsync(stream, FormatStatusResponse(msgId, isCompiling, elapsed), clientToken);
                            continue;
                        }
                        if (cmdName == "get_enabled_tools")
                        {
                            var tools = CommandRouter.ExecGetEnabledToolsCached();
                            await SendAsync(stream, JsonHelper.FormatResponse(msgId, true, tools, null), clientToken);
                            continue;
                        }

                        // Dispatch to main thread (supports async commands like run_tests)
                        var tcs = new TaskCompletionSource<string>();
                        var timeoutSec = GetCommandTimeout(cmdName);
                        using var cmdTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                            clientToken, cmdTimeout.Token);
                        _mainThreadQueue.Enqueue(() =>
                        {
                            // Skip if Python already gave up (per-command timeout fired and
                            // sent retry:2000) — prevents the queued action running a 2nd time
                            // after Python re-sent it → duplicate mutations.
                            if (_shuttingDown || tcs.Task.IsCompleted) { tcs.TrySetCanceled(); return; }
                            try
                            {
                                CommandRouter.ProcessAsync(json, tcs);
                            }
                            catch (Exception e)
                            {
                                Debug.LogException(e);
                                tcs.TrySetException(e);
                            }
                        });
                        EditorApplication.QueuePlayerLoopUpdate();

                        // Stop() / client replacement / per-command timeout unblocks this await
                        using var reg = linkedCts.Token.Register(() =>
                        {
                            if (_shuttingDown || clientToken.IsCancellationRequested)
                                tcs.TrySetCanceled();
                            else
                                tcs.TrySetResult(
                                    $"{{\"id\":\"{msgId}\",\"ok\":false,\"err\":\"Command '{cmdName}' timed out after {timeoutSec}s (Unity main thread blocked). Retry.\",\"retry\":2000}}");
                        });
                        var result = await tcs.Task;
                        await SendAsync(stream, result, clientToken);
                    }
                }
            }
            catch (OperationCanceledException) { /* clean shutdown or client replaced */ }
            catch (Exception e)
            {
                if (!_shuttingDown && !clientToken.IsCancellationRequested)
                {
                    Debug.LogError($"[MCP] Client error: {e.Message}");
                }
            }
            finally
            {
                // Only clear slot state if we are still the active handler for this slot.
                if (Interlocked.Read(ref slot.Generation) == generation)
                {
                    slot.Connected = false;
                    slot.Client = null;
                }
                Debug.Log($"[MCP] {label} client disconnected (gen={generation})");
            }
        }

        private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken token)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead, token);
                if (read == 0)
                    return false;
                totalRead += read;
            }
            return true;
        }

        private static async Task SendAsync(NetworkStream stream, string json, CancellationToken token)
        {
            var payload = Encoding.UTF8.GetBytes(json);
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)payload.Length);
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
            await stream.WriteAsync(frame, 0, frame.Length, token);
            await stream.FlushAsync(token);
        }

        private static void ProcessMainThreadQueue()
        {
            if (_shuttingDown) return;
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private static void WritePortFile(int port)
        {
            try
            {
                var cachePath = Path.Combine(Application.temporaryCachePath, "mcp_port.txt");
                File.WriteAllText(cachePath, port.ToString());
            }
            catch { }
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".unity-mcp", "ports");
                Directory.CreateDirectory(dir);
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                var project = Path.GetFileName(Path.GetDirectoryName(Application.dataPath));
                var info = $"{port}\n{Path.GetDirectoryName(Application.dataPath)}\n{project}";
                File.WriteAllText(Path.Combine(dir, $"{pid}.port"), info);
            }
            catch (Exception e) { Debug.LogWarning($"[MCP] Could not write discovery file: {e.Message}"); }
        }

        private static void DeletePortFile()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".unity-mcp", "ports");
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                var path = Path.Combine(dir, $"{pid}.port");
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        private static void TeardownSlot(ClientSlot slot)
        {
            try { slot.Cts?.Cancel(); } catch { }
            try { slot.Client?.Close(); } catch { }
            slot.Client = null;
            slot.Connected = false;
            try { slot.Cts?.Dispose(); } catch { }
            slot.Cts = null;
        }

        private static void TeardownCore()
        {
            try { _cts?.Cancel(); } catch { }      // fires linked tokens safely FIRST
            TeardownSlot(_mainSlot);
            TeardownSlot(_chatSlot);
            try { _cts?.Dispose(); } catch { }
            _cts = null;
            try { _listener?.Server?.Shutdown(SocketShutdown.Both); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;
            try { _chatListener?.Server?.Shutdown(SocketShutdown.Both); } catch { }
            try { _chatListener?.Stop(); } catch { }
            _chatListener = null;
            EditorApplication.update -= ProcessMainThreadQueue;
            EditorApplication.update -= WatchdogTick;
        }

        public static void Stop()
        {
            Debug.Log("[MCP] Server stopping");
            _shuttingDown = true;
            TeardownCore();
            // Drain queue after handlers are unregistered
            while (_mainThreadQueue.TryDequeue(out _)) { }
            DeletePortFile();
        }

        private static void OnQuit()
        {
            _shuttingDown = true;
            EditorApplication.update -= WatchdogTick;
            DeleteStateFile();
            Stop();
        }

        private static void OnBeforeReload()
        {
            _shuttingDown = true;
            WriteStateFile("reloading");
            // Send going_away FIRST — stream still alive, handler still running
            if (_mainSlot.Client != null && _mainSlot.Connected)
                try { SendGoingAwaySync(_mainSlot.Client.GetStream()); } catch { }
            if (_chatSlot.Client != null && _chatSlot.Connected)
                try { SendGoingAwaySync(_chatSlot.Client.GetStream()); } catch { }
            TeardownCore();
            // Do NOT delete port file — port stays the same after reload, Python needs it
        }

        // ── Tier 2: State file ────────────────────────────────────────────────

        internal static void WriteStateFile(string state)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".unity-mcp", "state");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"port-{Port}.state");
                var tmp = path + ".tmp";
                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                // Invariant culture so decimal separator is always '.'
                File.WriteAllText(tmp, $"{state}\n{ts.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n{pid}");
                // rename(2) on POSIX atomically overwrites
                if (File.Exists(path)) File.Delete(path); // .NET Framework compat
                File.Move(tmp, path);
            }
            catch { }
        }

        internal static void DeleteStateFile()
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".unity-mcp", "state", $"port-{Port}.state");
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        // ── Tier 4a: going_away frame ─────────────────────────────────────────

        internal static void SendGoingAwaySync(Stream stream)
        {
            if (stream == null) return;
            try
            {
                var payload = Encoding.UTF8.GetBytes("{\"ev\":\"going_away\",\"reason\":\"domain_reload\"}");
                var header = new byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(header, (uint)payload.Length);
                stream.Write(header, 0, 4);
                stream.Write(payload, 0, payload.Length);
                stream.Flush();
            }
            catch { }
        }

        // ── Tier 4b: status response format ──────────────────────────────────

        internal static string FormatStatusResponse(string msgId, bool isCompiling, double elapsed)
        {
            var state = isCompiling ? $"compiling|{elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}" : "idle|0";
            var compile = isCompiling ? "true" : "false";
            return $"{{\"id\":\"{msgId}\",\"ok\":true,\"data\":\"{state}\",\"compile\":{compile}}}";
        }
    }
}
