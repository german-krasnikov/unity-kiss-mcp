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
        // ── Per-port client state (ring of up to 4 simultaneous clients) ────────
        private sealed class ClientSlot
        {
            internal const int MaxClients = 4;

            private sealed class ClientEntry
            {
                internal volatile TcpClient Client;
                internal volatile CancellationTokenSource Cts;
                internal long Generation;  // Interlocked
            }

            private readonly ClientEntry[] _entries;
            private readonly object _lock = new object();

            internal ClientSlot()
            {
                _entries = new ClientEntry[MaxClients];
                for (int i = 0; i < MaxClients; i++)
                    _entries[i] = new ClientEntry();
            }

            private static bool IsSocketAlive(TcpClient client)
            {
                try
                {
                    var s = client.Client;
                    if (s == null || !s.Connected) return false;
                    if (s.Poll(0, SelectMode.SelectRead))
                        return s.Available > 0;
                    return true;
                }
                catch { return false; }
            }

            // Returns (index, generation, clientCts) for the new entry
            internal (int index, long generation, CancellationTokenSource clientCts) Add(
                TcpClient client, CancellationToken parentToken)
            {
                lock (_lock)
                {
                    // Evict dead connections before looking for a slot
                    for (int i = 0; i < MaxClients; i++)
                    {
                        var c = _entries[i].Client;
                        if (c != null && !IsSocketAlive(c))
                        {
                            try { _entries[i].Cts?.Cancel(); } catch { }
                            try { c.Close(); } catch { }
                            _entries[i].Client = null;
                            _entries[i].Cts = null;
                        }
                    }
                    // Find empty slot first
                    for (int i = 0; i < MaxClients; i++)
                    {
                        if (_entries[i].Client == null)
                        {
                            var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
                            var gen = Interlocked.Increment(ref _entries[i].Generation);
                            _entries[i].Client = client;
                            _entries[i].Cts = cts;
                            return (i, gen, cts);
                        }
                    }
                    // All full — evict entry 0 (oldest), shift down, add at end
                    try { _entries[0].Cts?.Cancel(); } catch { }
                    try { _entries[0].Client?.Close(); } catch { }
                    for (int i = 0; i < MaxClients - 1; i++)
                    {
                        _entries[i].Client = _entries[i + 1].Client;
                        _entries[i].Cts = _entries[i + 1].Cts;
                        _entries[i].Generation = _entries[i + 1].Generation;
                    }
                    var last = MaxClients - 1;
                    var newCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
                    var newGen = Interlocked.Increment(ref _entries[last].Generation);
                    _entries[last].Client = client;
                    _entries[last].Cts = newCts;
                    return (last, newGen, newCts);
                }
            }

            // Called from handler's finally block — only clears if generation matches
            internal void Clear(int index, long generation)
            {
                lock (_lock)
                {
                    if (index >= 0 && index < MaxClients &&
                        Interlocked.Read(ref _entries[index].Generation) == generation)
                    {
                        _entries[index].Client = null;
                        _entries[index].Cts = null;
                    }
                }
            }

            // Safe iteration over all connected clients (e.g. going_away broadcast)
            internal void ForEach(Action<TcpClient> action)
            {
                lock (_lock)
                {
                    for (int i = 0; i < MaxClients; i++)
                    {
                        var c = _entries[i].Client;
                        if (c != null)
                            try { action(c); } catch { }
                    }
                }
            }

            // Cancel + close all entries (teardown)
            internal void DisconnectAll()
            {
                lock (_lock)
                {
                    for (int i = 0; i < MaxClients; i++)
                    {
                        try { _entries[i].Cts?.Cancel(); } catch { }
                        try { _entries[i].Client?.Close(); } catch { }
                        _entries[i].Client = null;
                        _entries[i].Cts = null;
                    }
                }
            }

            internal bool AnyConnected
            {
                get
                {
                    lock (_lock)
                    {
                        for (int i = 0; i < MaxClients; i++)
                            if (_entries[i].Client != null && IsSocketAlive(_entries[i].Client)) return true;
                        return false;
                    }
                }
            }

            internal int CountPhantoms()
            {
                int count = 0;
                lock (_lock)
                {
                    for (int i = 0; i < MaxClients; i++)
                    {
                        var c = _entries[i].Client;
                        if (c != null && !IsSocketAlive(c)) count++;
                    }
                }
                return count;
            }

            internal int KillPhantoms()
            {
                int killed = 0;
                lock (_lock)
                {
                    for (int i = 0; i < MaxClients; i++)
                    {
                        var c = _entries[i].Client;
                        if (c != null && !IsSocketAlive(c))
                        {
                            try { _entries[i].Cts?.Cancel(); } catch { }
                            try { c.Close(); } catch { }
                            _entries[i].Client = null;
                            _entries[i].Cts = null;
                            killed++;
                        }
                    }
                }
                return killed;
            }
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
        // Cached domain stamp — read from main thread in StartAsync, used in get_version fast-path
        // (which runs on ThreadPool after ConfigureAwait(false); SessionState not thread-safe).
        private static volatile string _domainStamp = "";
        // Set on first compilationStarted; never cleared. Distinguishes real compile-in-progress
        // from post-domain-reload stale EditorApplication.isCompiling on Windows.
        private static volatile bool _compileStartedThisDomain;
        private static readonly string PortFilePath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "MCP_Port.json"));
        private static readonly string _projectSettingsPath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ProjectSettings", "MCPSettings.json"));
        private static int _port;
        private static int _chatPort;
        private static bool _portsResolved;
        private static DateTime _compileStartTime;

        private static string ReadPortFileOrNull()
        {
            try { return File.Exists(PortFilePath) ? File.ReadAllText(PortFilePath) : null; }
            catch { return null; }
        }

        private static string ReadProjectSettingsOrNull()
        {
            try { return File.Exists(_projectSettingsPath) ? File.ReadAllText(_projectSettingsPath) : null; }
            catch { return null; }
        }

        private static void EnsurePorts()
        {
            if (_portsResolved) return;
            var env = System.Environment.GetEnvironmentVariable("UNITY_MCP_PORT");
            var projectJson = ReadProjectSettingsOrNull();
            var cacheJson = ReadPortFileOrNull();
            _port = PortResolver.ResolvePort(env, projectJson, cacheJson, 9500);
            var chatEnv = System.Environment.GetEnvironmentVariable("UNITY_MCP_CHAT_PORT");
            _chatPort = PortResolver.ResolveChatPort(chatEnv, projectJson, cacheJson, _port, _port + 1);
            _portsResolved = true;
        }

        private static int Port { get { EnsurePorts(); return _port; } }
        private static int ChatPort { get { EnsurePorts(); return _chatPort; } }

        public static void SavePorts(int port, int chatPort)
        {
            PortResolver.SavePorts(PortFilePath, port, chatPort);
            PortResolver.SaveProjectSettings(_projectSettingsPath, port, chatPort);
            _port = port;
            _chatPort = chatPort;
            _portsResolved = true;
            WritePortFile(port);
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
                { "ask_user", 300 },
            };

        internal static int GetCommandTimeout(string cmd)
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
        public static bool IsClientConnected => _mainSlot.AnyConnected || _chatSlot.AnyConnected;
        public static int PhantomCount => _mainSlot.CountPhantoms() + _chatSlot.CountPhantoms();
        public static int KillPhantoms()
        {
            var killed = _mainSlot.KillPhantoms() + _chatSlot.KillPhantoms();
            if (killed > 0) UnityEngine.Debug.Log($"[MCP] Killed {killed} phantom connection(s)");
            return killed;
        }
        public static int ServerPort => Port;
        public static int ServerChatPort => ChatPort;
        // Reads reloadPort from MCP_Port.json. Returns 0 if reload-package is not installed.
        public static int ServerReloadPort => PortResolver.ReadReloadPort(PortFilePath);
        public static double CompileElapsedSeconds => _isCompiling
            ? (DateTime.UtcNow - _compileStartTime).TotalSeconds
            : 0.0;

        // True only if compilationStarted fired in THIS domain (never resets to false).
        // Used to detect post-reload stale EditorApplication.isCompiling on Windows:
        // if we never saw compilationStarted in this domain, isCompiling=true is a reload artifact.
        public static bool CompileStartedThisDomain => _compileStartedThisDomain;

        // Authoritative compile state: set by compilationStarted, cleared by compilationFinished.
        // Unlike EditorApplication.isCompiling, never stays latched after domain reload.
        internal static bool IsReallyCompiling => _isCompiling;

        private static double _lastWatchdogCheck;

        // Pure helper — no Unity API calls, fully unit-testable.
        // Mirrors ReloadPlugin.ShouldStartReloadServer pattern.
        internal static bool ShouldStartServer(bool isBatchMode) => !isBatchMode;

        static MCPServer()
        {
            if (!ShouldStartServer(Application.isBatchMode)) return;

            // Persist auto-assigned ports so they survive domain reload
            SavePorts(Port, ChatPort);
            EditorApplication.update += ProcessMainThreadQueue;
            EditorApplication.update += WatchdogTick;
            EditorApplication.quitting += OnQuit;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            CompilationPipeline.compilationStarted += _ => { _isCompiling = true; _compileStartedThisDomain = true; _compileStartTime = DateTime.UtcNow; WriteStateFile("compiling"); };
            CompilationPipeline.compilationFinished += _ =>
            {
                // R-4 fix: never write "ready" from compilationFinished.
                // If compile failed → no reload will happen, write compile_failed.
                // If compile succeeded → reload is coming; "ready" written after reload in StartAsync.
                _isCompiling = false;
                if (EditorUtility.scriptCompilationFailed)
                    WriteStateFile("compile_failed");
                // else: state stays "compiling" until reload completes (StartAsync writes "ready")
            };
            EditorSceneManager.sceneOpened += (_, _) => { RefManager.Invalidate(); HierarchySerializer.ResetIncrementalCache(); };
            EditorSceneManager.newSceneCreated += (_, _, _) => { RefManager.Invalidate(); HierarchySerializer.ResetIncrementalCache(); };
            EditorSceneManager.sceneClosed += _ => { RefManager.Invalidate(); HierarchySerializer.ResetIncrementalCache(); };
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
            _domainStamp = SyncHelper.CurrentDomainStamp;  // cache on main thread — safe here, ThreadPool reads below
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

                // Main listener: 3 fast retries on same port, then fall back to a free port.
                // On Windows, ExclusiveAddressUse prevents TIME_WAIT reuse by the same process;
                // falling back to a new port avoids the full TIME_WAIT window.
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    var bindPort = (attempt < 3) ? Port : PortResolver.FindFreePort(Port + 1, skipPort: ChatPort);
                    try
                    {
                        _listener = new TcpListener(IPAddress.Loopback, bindPort);
#if UNITY_EDITOR_WIN
                        // Windows: SO_REUSEADDR = port-hijack; use ExclusiveAddressUse instead (CoplayDev #1173)
                        _listener.Server.ExclusiveAddressUse = true;
#else
                        _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
                        try { _listener.Server.SetSocketOption(SocketOptionLevel.Socket, (SocketOptionName)0x0200, true); } catch { }
#endif
#endif
                        _listener.Start();
                        if (bindPort != Port)
                        {
                            var bp = bindPort; _mainThreadQueue.Enqueue(() => Debug.LogWarning($"[MCP] Port {Port} in TIME_WAIT, switched to {bp}"));
                            SavePorts(bindPort, ChatPort);
                        }
                        break;
                    }
                    catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        try { _listener?.Stop(); } catch { }
                        _listener = null;
                        if (attempt == 3) throw;
                        var bp2 = bindPort; var at = attempt; _mainThreadQueue.Enqueue(() => Debug.LogWarning($"[MCP] Port {bp2} busy, retry {at + 1}/3..."));
                        await Task.Delay(400 * (attempt + 1), token).ConfigureAwait(false);
                    }
                }

                // Chat listener — best-effort (non-fatal if chat port is unavailable)
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var bindPort = (attempt < 2) ? ChatPort : PortResolver.FindFreePort(ChatPort + 1, skipPort: Port);
                    try
                    {
                        _chatListener = new TcpListener(IPAddress.Loopback, bindPort);
#if UNITY_EDITOR_WIN
                        _chatListener.Server.ExclusiveAddressUse = true;
#else
                        _chatListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
#endif
                        _chatListener.Start();
                        if (bindPort != ChatPort)
                        {
                            var bp = bindPort; _mainThreadQueue.Enqueue(() => Debug.LogWarning($"[MCP] Chat port {ChatPort} in TIME_WAIT, switched to {bp}"));
                            SavePorts(Port, bindPort);
                        }
                        break;
                    }
                    catch (SocketException se) when (se.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        try { _chatListener?.Stop(); } catch { }
                        _chatListener = null;
                        if (attempt == 2)
                        {
                            var msg = se.Message; _mainThreadQueue.Enqueue(() => Debug.LogWarning($"[MCP] Chat port {ChatPort} unavailable after fallback: {msg}"));
                            break;
                        }
                        var bp2 = bindPort; var at = attempt; _mainThreadQueue.Enqueue(() => Debug.LogWarning($"[MCP] Chat port {bp2} busy, retry {at + 1}/2..."));
                        await Task.Delay(300 * (attempt + 1), token).ConfigureAwait(false);
                    }
                    catch (SocketException se)
                    {
                        var msg = se.Message; _mainThreadQueue.Enqueue(() => Debug.LogWarning($"[MCP] Chat port {ChatPort} unavailable: {msg}"));
                        _chatListener = null;
                        break;
                    }
                }

                _mainThreadQueue.Enqueue(() => Debug.Log($"[MCP] Server started on port {Port} (chat: {ChatPort})"));
                WritePortFile(Port);
                WriteStateFile("ready");

                // Run both accept loops concurrently — chat loop is optional
                var mainLoop = RunAcceptLoop(_listener, _mainSlot, "CLI", cts, token);
                var chatLoop = _chatListener != null
                    ? RunAcceptLoop(_chatListener, _chatSlot, "Chat", cts, token)
                    : Task.CompletedTask;
                await Task.WhenAll(mainLoop, chatLoop).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (!_shuttingDown)
                {
                    var msg = e.Message; _mainThreadQueue.Enqueue(() => Debug.LogError($"[MCP] Server error: {msg}"));
                }
                // Clean up on bind failure — prevent CTS/listener leak
                if (!IsRunning)
                {
                    try { cts?.Dispose(); } catch { }
                    if (_cts == cts) _cts = null;
                    try { _listener?.Stop(); } catch { }
                    _listener = null;
                    if (!_shuttingDown) WriteStateFile("bind_failed");
                }
            }
            finally { _starting = false; }
        }

        // WARNING: All awaits in RunAcceptLoop use ConfigureAwait(false).
        // Code after any await here runs on ThreadPool, NOT main thread.
        // Do NOT call Unity Editor APIs directly — use _mainThreadQueue.Enqueue().
        private static async Task RunAcceptLoop(TcpListener listener, ClientSlot slot, string label,
            CancellationTokenSource masterCts, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync().ConfigureAwait(false); }
                catch (ObjectDisposedException) { break; }
                catch (Exception e)
                {
                    if (token.IsCancellationRequested) break;
                    var msg = e.Message; _mainThreadQueue.Enqueue(() => Debug.LogError($"[MCP] {label} accept error: {msg}"));
                    if (_cts != masterCts || !IsRunning) break;
                    await Task.Delay(100, token).ConfigureAwait(false);
                    continue;
                }

                try { client.NoDelay = true; } catch { }
                ApplyKeepAlive(client.Client);
                var (idx, gen, clientCts) = slot.Add(client, token);
                _ = HandleClientAsync(client, slot, idx, gen, label, clientCts.Token);
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

        // WARNING: All awaits in HandleClientAsync use ConfigureAwait(false).
        // Code after any await here runs on ThreadPool, NOT main thread.
        // Do NOT call Unity Editor APIs directly — use _mainThreadQueue.Enqueue().
        private static async Task HandleClientAsync(TcpClient client, ClientSlot slot, int index, long generation,
            string label, CancellationToken clientToken)
        {
            var endPoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            _mainThreadQueue.Enqueue(() =>
            {
                Debug.Log($"[MCP] {label} client connected from {endPoint}");
                RefManager.Invalidate();
            });
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var header = new byte[4];

                    while (client.Connected && !clientToken.IsCancellationRequested)
                    {
                        if (!await ReadExactAsync(stream, header, clientToken).ConfigureAwait(false))
                            break;

                        var length = BinaryPrimitives.ReadUInt32BigEndian(header);
                        if (length > MaxMessageSize)
                        {
                            var len = length; _mainThreadQueue.Enqueue(() => Debug.LogWarning($"[MCP] Protocol desync: length prefix {len} bytes (0x{len:X8}) exceeds {MaxMessageSize} — reconnecting"));
                            break;
                        }

                        var payload = new byte[length];
                        if (!await ReadExactAsync(stream, payload, clientToken).ConfigureAwait(false))
                            break;

                        var json = Encoding.UTF8.GetString(payload);

                        // Fast-path: ping/get_version/status bypass main thread (works even when Editor is busy)
                        var cmdName = JsonHelper.ExtractString(json, "cmd");
                        var msgId = JsonHelper.ExtractString(json, "id");
                        if (cmdName == "ping")
                        {
                            await SendAsync(stream, JsonHelper.FormatResponse(msgId, true, "pong", null), clientToken).ConfigureAwait(false);
                            continue;
                        }
                        if (cmdName == "get_version")
                        {
                            // RC-5: include domain stamp so reconnect can detect stale DLL.
                            // Use cached stamp — SyncHelper.CurrentDomainStamp is SessionState (main-thread only).
                            var stamp = _domainStamp;
                            var ver = BuildVersionString(stamp, PluginVersion);
                            await SendAsync(stream, JsonHelper.FormatResponse(msgId, true, ver, null), clientToken).ConfigureAwait(false);
                            continue;
                        }
                        if (cmdName == "status")
                        {
                            var isCompiling = _isCompiling;
                            var elapsed = isCompiling ? (DateTime.UtcNow - _compileStartTime).TotalSeconds : 0.0;
                            await SendAsync(stream, FormatStatusResponse(msgId, isCompiling, elapsed), clientToken).ConfigureAwait(false);
                            continue;
                        }
                        if (cmdName == "get_enabled_tools")
                        {
                            var tools = CommandRouter.ExecGetEnabledToolsCached();
                            await SendAsync(stream, JsonHelper.FormatResponse(msgId, true, tools, null), clientToken).ConfigureAwait(false);
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
                        // QueuePlayerLoopUpdate wakes the main thread to drain _mainThreadQueue.
                        // Wrapped in Enqueue so it runs on main thread — closes the invariant
                        // "zero Unity API on ThreadPool" (even if it were thread-safe, the pattern is consistent).
                        _mainThreadQueue.Enqueue(() => EditorApplication.QueuePlayerLoopUpdate());

                        // Stop() / client replacement / per-command timeout unblocks this await
                        using var reg = linkedCts.Token.Register(() =>
                        {
                            if (_shuttingDown || clientToken.IsCancellationRequested)
                                tcs.TrySetCanceled();
                            else
                                tcs.TrySetResult(
                                    $"{{\"id\":\"{JsonHelper.EscapeJson(msgId)}\",\"ok\":false,\"err\":\"Command '{JsonHelper.EscapeJson(cmdName)}' timed out after {timeoutSec}s (Unity main thread blocked). Retry.\",\"retry\":2000}}");
                        });
                        var result = await tcs.Task.ConfigureAwait(false);
                        await SendAsync(stream, result, clientToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { /* clean shutdown or client replaced */ }
            catch (Exception e)
            {
                if (!_shuttingDown && !clientToken.IsCancellationRequested)
                {
                    var msg = e.Message; _mainThreadQueue.Enqueue(() => Debug.LogError($"[MCP] Client error: {msg}"));
                }
            }
            finally
            {
                slot.Clear(index, generation);
                var lbl = label; var gen = generation;
                _mainThreadQueue.Enqueue(() => Debug.Log($"[MCP] {lbl} client disconnected (gen={gen})"));
            }
        }

        // internal (not private) so UnityMCP.Editor.Tests can call directly for seam tests.
        internal static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken token)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead, token).ConfigureAwait(false);
                if (read == 0)
                    return false;
                totalRead += read;
            }
            return true;
        }

        // internal (not private) so UnityMCP.Editor.Tests can call directly for seam tests.
        // ConfigureAwait(false) on BOTH awaits below is mandatory — GREEN state.
        internal static async Task SendAsync(Stream stream, string json, CancellationToken token)
        {
            var payload = Encoding.UTF8.GetBytes(json);
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)payload.Length);
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
            await stream.WriteAsync(frame, 0, frame.Length, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
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
                if (_chatPort > 0)
                {
                    var chatInfo = $"{_chatPort}\n{Path.GetDirectoryName(Application.dataPath)}\n{project}";
                    File.WriteAllText(Path.Combine(dir, $"{pid}.chat-port"), chatInfo);
                }
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
                var chatPath = Path.Combine(dir, $"{pid}.chat-port");
                if (File.Exists(chatPath)) File.Delete(chatPath);
            }
            catch { }
        }

        private static void TeardownCore()
        {
            try { _cts?.Cancel(); } catch { }      // fires linked tokens safely FIRST
            _mainSlot.DisconnectAll();
            _chatSlot.DisconnectAll();
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
            while (_mainThreadQueue.TryDequeue(out _)) { }  // drain: prevent queued actions after domain tear-down
        }

        public static void Stop()
        {
            Debug.Log("[MCP] Server stopping");
            _shuttingDown = true;
            TeardownCore();  // already drains queue
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
            // Send going_away FIRST — streams still alive, handlers still running
            _mainSlot.ForEach(c => SendGoingAwaySync(c.GetStream()));
            _chatSlot.ForEach(c => SendGoingAwaySync(c.GetStream()));
            TeardownCore();
            // Do NOT delete port file — port stays the same after reload, Python needs it
        }

        // ── Tier 2: State file ────────────────────────────────────────────────

#if UNITY_INCLUDE_TESTS
        internal static void ResetDomainStateForTests()
        {
            _compileStartedThisDomain = false;
            _isCompiling = false;
        }
#endif

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
                var epoch = SyncHelper.CurrentEpoch;
                // Invariant culture so decimal separator is always '.'
                File.WriteAllText(tmp,
                    $"{state}\n{ts.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n{pid}\n{epoch}");
                // Unity editor scripting is still Mono/netstandard2.1 — no
                // File.Move(string,string,bool) overload (CS1739). Delete+Move:
                // tiny non-atomic window, readers retry so it's acceptable.
                try { File.Delete(path); } catch { }
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

        // synced by sync_versions.py — do not edit manually
        internal static string PluginVersion => "0.56.0";

        internal static string BuildVersionString(string stamp, string pluginVersion)
        {
            var result = $"proto:3|plugin:{pluginVersion}";
            if (!string.IsNullOrEmpty(stamp))
                result += $"|stamp:{stamp}";
            return result;
        }

        internal static string FormatStatusResponse(string msgId, bool isCompiling, double elapsed)
        {
            var state = isCompiling ? $"compiling|{elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}" : "idle|0";
            var compile = isCompiling ? "true" : "false";
            return $"{{\"id\":\"{msgId}\",\"ok\":true,\"data\":\"{state}\",\"compile\":{compile}}}";
        }

    }
}
