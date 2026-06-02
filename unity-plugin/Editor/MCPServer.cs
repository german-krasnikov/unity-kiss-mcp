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
        private static TcpListener _listener;
        private static CancellationTokenSource _cts;
        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        private static volatile bool _clientConnected;
        private static TcpClient _currentClient;
        private static CancellationTokenSource _clientCts;
        private static long _clientGeneration;
        private static volatile bool _shuttingDown;
        private static volatile bool _starting;
        private static readonly int Port = GetPort();
        private static DateTime _compileStartTime;
        private static int GetPort()
        {
            var env = System.Environment.GetEnvironmentVariable("UNITY_MCP_PORT");
            return env != null && int.TryParse(env, out var p) ? p : 9500;
        }
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
        public static bool IsClientConnected => _clientConnected;
        public static int ServerPort => Port;

        private static double _lastWatchdogCheck;

        static MCPServer()
        {
            EditorApplication.update += ProcessMainThreadQueue;
            EditorApplication.update += WatchdogTick;
            EditorApplication.quitting += OnQuit;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            CompilationPipeline.compilationStarted += _ => { _compileStartTime = DateTime.UtcNow; WriteStateFile("compiling"); };
            CompilationPipeline.compilationFinished += _ =>
            {
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
                        await Task.Delay(500 * (attempt + 1));
                    }
                }

                Debug.Log($"[MCP] Server started on port {Port}");
                WritePortFile(Port);
                WriteStateFile("ready");

                while (!token.IsCancellationRequested)
                {
                    var client = await AcceptClientAsync(token);
                    if (client == null)
                    {
                        // Listener disposed/superseded or a transient accept error.
                        // Break if shut down or this loop was orphaned by a newer
                        // StartAsync (_cts replaced); otherwise back off so we never
                        // tight-spin on a dead listener → silent 100% CPU, no console.
                        if (token.IsCancellationRequested || _cts != cts || !IsRunning) break;
                        await Task.Delay(100, token);
                        continue;
                    }
                    if (client != null)
                    {
                        // Cancel old handler BEFORE closing socket — triggers clean
                        // OperationCanceledException instead of ObjectDisposedException
                        if (_clientConnected && _currentClient != null)
                        {
                            Debug.Log("[MCP] New client — disconnecting previous");
                            try { _clientCts?.Cancel(); } catch { }
                            try { _currentClient.Close(); } catch { }
                        }
                        try { client.NoDelay = true; } catch { }
                        ApplyKeepAlive(client.Client);
                        _currentClient = client;
                        var gen = Interlocked.Increment(ref _clientGeneration);
                        _clientCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                        _ = HandleClientAsync(client, gen, _clientCts.Token);
                    }
                }
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

        private static async Task<TcpClient> AcceptClientAsync(CancellationToken token)
        {
            try
            {
                return await _listener.AcceptTcpClientAsync();
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                {
                    Debug.LogError($"[MCP] Accept error: {e.Message}");
                }
                return null;
            }
        }

        private static async Task HandleClientAsync(TcpClient client, long generation, CancellationToken clientToken)
        {
            Debug.Log("[MCP] Client connected");
            _clientConnected = true;
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
                            var isCompiling = EditorApplication.isCompiling;
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
                // Only clear shared state if we are still the active handler.
                // If a newer client already took over, _clientGeneration > our generation.
                if (Interlocked.Read(ref _clientGeneration) == generation)
                {
                    _clientConnected = false;
                    _currentClient = null;
                }
                Debug.Log($"[MCP] Client disconnected (gen={generation})");
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

        public static void Stop()
        {
            Debug.Log("[MCP] Server stopping");
            _shuttingDown = true;
            EditorApplication.update -= ProcessMainThreadQueue;
            // 1. Cancel tokens first — unblocks async loops
            try { _clientCts?.Cancel(); } catch { }
            try { _cts?.Cancel(); } catch { }
            // 2. Close sockets — unblocks blocked reads
            try { _currentClient?.Close(); } catch { }
            _currentClient = null;
            _clientConnected = false;
            // 3. Dispose CTS (after cancel, allow callbacks to complete)
            try { _clientCts?.Dispose(); } catch { }
            _clientCts = null;
            try { _cts?.Dispose(); } catch { }
            _cts = null;
            // 4. Stop listener (wrapped — can throw SocketException)
            try { _listener?.Server?.Shutdown(SocketShutdown.Both); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;
            // 5. Drain queue
            while (_mainThreadQueue.TryDequeue(out _)) { }
            EditorApplication.update -= WatchdogTick;
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
            // 1. Send going_away FIRST — stream still alive, handler still running
            if (_currentClient != null && _clientConnected)
            {
                try { SendGoingAwaySync(_currentClient.GetStream()); } catch { }
            }
            // 2. Cancel client CTS — handler catches OperationCanceledException cleanly
            try { _clientCts?.Cancel(); } catch { }
            // 3. Cancel server CTS — stops accept loop
            // Wrapped: linked _clientCts registers a callback on _cts; if _clientCts was
            // already cancelled above, Mono may throw ObjectDisposedException from the callback.
            try { _cts?.Cancel(); } catch { }
            try { _currentClient?.Close(); } catch { }
            _currentClient = null;
            _clientConnected = false;
            try { _clientCts?.Dispose(); } catch { }
            _clientCts = null;
            try { _cts?.Dispose(); } catch { }
            _cts = null;
            try { _listener?.Server?.Shutdown(SocketShutdown.Both); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;
            EditorApplication.update -= ProcessMainThreadQueue;
            EditorApplication.update -= WatchdogTick;
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
