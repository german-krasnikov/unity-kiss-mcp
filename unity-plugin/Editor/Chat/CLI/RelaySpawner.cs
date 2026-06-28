// Launches chat_relay.py sidecar, reads port from stdout, persists to SessionState.
// Relay survives domain reload — static field _process is null post-reload but
// IsProcessAlive() reattaches via PID stored in SessionState.
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal static class RelaySpawner
    {
        internal const string PortKey = "MCPChat_Relay_Port";
        internal const string PidKey  = "MCPChat_Relay_PID";

        internal static int  RelayPort => SessionState.GetInt(PortKey, 0);
        internal static bool IsRunning  => _process != null && !_process.HasExited;

        internal static event Action OnAfterReloadResume;

        // Test seams — replace to inject mocks
        internal static Func<ProcessStartInfo, Process> ProcessFactory = psi => Process.Start(psi);
        internal static Func<string>                    PythonResolver = DefaultPythonResolver;
        internal static TimeSpan                        ReadTimeout    = TimeSpan.FromSeconds(5);
#if UNITY_INCLUDE_TESTS
        // Override EnsureRunning entirely in unit tests — prevents GetProcessById(selfPid)
        // from attaching _process to the Unity editor, which would cause Stop() to kill it.
        internal static Func<int>      EnsureRunningOverride;
        // Override TCP alive check — avoids real connection in unit tests.
        internal static Func<int, bool> TcpAliveOverride;
#endif

        private static Process  _process;
        private static DateTime _tcpAliveExpiry;
        private static bool     _tcpAliveResult;

        static RelaySpawner()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            AssemblyReloadEvents.afterAssemblyReload  += OnAfterReload;
            EditorApplication.quitting                += Stop;
        }

        /// <summary>Ensure relay is running. Returns TCP port. Throws on failure.</summary>
        internal static int EnsureRunning()
        {
#if UNITY_INCLUDE_TESTS
            if (EnsureRunningOverride != null) return EnsureRunningOverride();
#endif
            var port = SessionState.GetInt(PortKey, 0);
            var pid  = SessionState.GetInt(PidKey,  0);
            if (port > 0 && IsProcessAlive(pid) && IsTcpAlive(port))
            {
                // Reattach handle after domain reload (_process is null post-reload)
                if (_process == null || _process.HasExited)
                    try { _process = Process.GetProcessById(pid); } catch { /* process died */ }
                return port;
            }
            return Spawn();
        }

        /// <summary>Kill relay process. Called on Unity quit or Stop().</summary>
        internal static void Stop()
        {
            try { _process?.Kill(); } catch { /* already gone */ }
            _process = null;
            try
            {
                SessionState.EraseInt(PortKey);
                SessionState.EraseInt(PidKey);
            }
            catch { /* editor shutting down */ }
        }

        // Relay survives domain reload — just a no-op; socket close happens in RelayChatProcess
        internal static void OnBeforeReload() { }

        internal static void OnAfterReload()
        {
            // Re-attach _process handle lost during domain reload so IsRunning stays true.
            var port = SessionState.GetInt(PortKey, 0);
            var pid  = SessionState.GetInt(PidKey,  0);
            if (port > 0 && pid > 0 && IsProcessAlive(pid))
                try { _process = Process.GetProcessById(pid); } catch { }
            OnAfterReloadResume?.Invoke();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static int Spawn()
        {
            var python = PythonResolver();
            if (string.IsNullOrEmpty(python))
                throw new InvalidOperationException(
                    "[MCP Relay] Python not found. Run: python install.py setup");

            var psi = new ProcessStartInfo(python)
            {
                Arguments              = "-m unity_mcp.chat_relay",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };

            _process = ProcessFactory(psi);
            if (_process == null)
                throw new InvalidOperationException("[MCP Relay] Process.Start returned null");

            var port = ReadRelayPortWithTimeout(_process.StandardOutput, ReadTimeout);
            SessionState.SetInt(PortKey, port);
            SessionState.SetInt(PidKey,  _process.Id);
            return port;
        }

        internal static int ParseRelayPort(string line)
        {
            const string prefix = "relay_port:";
            if (line == null || !line.StartsWith(prefix, StringComparison.Ordinal))
                throw new FormatException(
                    $"[MCP Relay] Expected 'relay_port:PORT', got: {line ?? "null"}");
            if (!int.TryParse(line.Substring(prefix.Length).Trim(), out var port))
                throw new FormatException($"[MCP Relay] Non-integer port in: {line}");
            return port;
        }

        internal static bool IsProcessAlive(int pid)
        {
            if (pid <= 0) return false;
            if (pid == System.Diagnostics.Process.GetCurrentProcess().Id) return false;
            try { return !Process.GetProcessById(pid).HasExited; }
            catch { return false; }
        }

        // Reads lines from reader until one starts with "relay_port:", skipping noise.
        // Throws TimeoutException if deadline passes without finding the port line.
        private static int ReadRelayPortWithTimeout(StreamReader reader, TimeSpan timeout)
        {
            const string prefix  = "relay_port:";
            var          deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) break;
                var task = Task.Run(() => reader.ReadLine());
                if (!task.Wait(remaining)) break;
                var line = task.Result;
                if (line == null) throw new InvalidOperationException("[MCP Relay] Process exited without reporting port");
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                    return ParseRelayPort(line);
                // else: noise line — continue waiting
            }
            throw new TimeoutException("[MCP Relay] Timed out waiting for relay to report port");
        }

        private static bool IsTcpAlive(int port)
        {
#if UNITY_INCLUDE_TESTS
            if (TcpAliveOverride != null) return TcpAliveOverride(port);
#endif
            // Cache result for 3s — avoids blocking main thread on every EnsureRunning call.
            if (DateTime.UtcNow < _tcpAliveExpiry) return _tcpAliveResult;
            try
            {
                using var tcp = new TcpClient();
                _tcpAliveResult = tcp.ConnectAsync("127.0.0.1", port).Wait(200);
            }
            catch { _tcpAliveResult = false; }
            _tcpAliveExpiry = DateTime.UtcNow.AddSeconds(3);
            return _tcpAliveResult;
        }

        private static string DefaultPythonResolver()
        {
            const string packageId = "com.unity-mcp.editor";
            var packageRoot = Path.GetFullPath($"Packages/{packageId}");
            var serverDir   = ChatMcpConfigWriter.ResolveServerDir(packageRoot);
            if (serverDir == null) return null;
            var (command, _) = ChatMcpConfigWriter.ResolvePythonCommand(serverDir, null);
            return command;
        }
    }
}
