// Manages the claude child process: spawn, stdin write, stdout reader thread, orphan-kill.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class ChatProcess : IDisposable
    {
        // ── Orphan-kill via SessionState ──────────────────────────────────────
        private const string PidKey = "UnityMCP_Chat_PID";

        // Raised after assembly reload so MCPChatWindow can auto-resume.
        internal static event System.Action OnAfterReloadResume;

        static ChatProcess()
        {
            AssemblyReloadEvents.beforeAssemblyReload += KillOrphan;
            AssemblyReloadEvents.afterAssemblyReload  += TriggerResume;
        }

        private static void KillOrphan()
        {
            var pidStr = SessionState.GetString(PidKey, "");
            if (!int.TryParse(pidStr, out var pid)) return;
            try { Process.GetProcessById(pid)?.Kill(); } catch { /* already gone */ }
            SessionState.EraseString(PidKey);
            // ReloadGuard.OnTurnFinished() — unlock is handled by the guard itself on reload;
            // the watchdog will also fire. No extra call needed here.
        }

        private static void TriggerResume() => OnAfterReloadResume?.Invoke();

        // ── Instance ──────────────────────────────────────────────────────────

        private Process _process;
        private StreamWriter _stdin;
        private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();
        private Thread _readerThread;
        private volatile bool _running;
        private volatile bool _disposing;
        // Bounded ring buffer for last N stderr lines — surfaced on unexpected exit.
        private readonly StderrRingBuffer _stderrRing = new StderrRingBuffer(5);

        internal bool IsRunning => _running && _process != null && !_process.HasExited;

        internal void Spawn(string binaryPath, string[] args, string[] stripEnvKeys)
        {
            if (IsRunning) return;

            // Windows: .cmd/.bat shims (npm installs) cannot be executed by CreateProcess directly
            // under Unity Mono (Win32Exception 193). Route through cmd.exe /c instead.
            // Note on arg quoting: TOML -c args contain embedded " escaped as \" by QuoteWindows.
            // cmd.exe does NOT honour \" natively, but npm shims (codex.cmd, claude.cmd) use %* to
            // forward all args verbatim to node, whose CRT re-parses \" correctly.
            // This works for %*-style npm shims but would break a .bat that uses positional %1/%2.
            // TODO: must validate on real Windows before shipping.
            var isCmdShim = SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows
                         && (binaryPath.EndsWith(".cmd", System.StringComparison.OrdinalIgnoreCase)
                          || binaryPath.EndsWith(".bat", System.StringComparison.OrdinalIgnoreCase));

            ProcessStartInfo psi;
            if (isCmdShim)
            {
                psi = new ProcessStartInfo("cmd.exe")
                {
                    UseShellExecute        = false,
                    RedirectStandardInput  = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    Arguments              = "/c " + ArgQuoting.QuoteWindows(binaryPath)
                                             + " " + string.Join(" ", QuoteArgs(args)),
                };
            }
            else
            {
                psi = new ProcessStartInfo(binaryPath)
                {
                    UseShellExecute        = false,
                    RedirectStandardInput  = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    Arguments              = string.Join(" ", QuoteArgs(args)),
                };
            }

            // Strip env keys that would override subscription auth
            foreach (var key in stripEnvKeys)
                if (psi.EnvironmentVariables.ContainsKey(key))
                    psi.EnvironmentVariables.Remove(key);

            _process = Process.Start(psi);
            if (_process == null) throw new InvalidOperationException("Failed to start claude process");

            SessionState.SetString(PidKey, _process.Id.ToString());

            // Force UTF-8 (no BOM) on all pipes: Unity-Mono defaults to the system code page,
            // which mangles non-ASCII (Cyrillic → '?'). BaseStream wrapping works on every
            // .NET profile (ProcessStartInfo.StandardInputEncoding is .NET Standard 2.1-only).
            var utf8 = new System.Text.UTF8Encoding(false);
            _stdin   = new StreamWriter(_process.StandardInput.BaseStream, utf8) { AutoFlush = true };
            _running = true;

            _readerThread = new Thread(ReadLoop) { IsBackground = true, Name = "ChatProcess.Reader" };
            _readerThread.Start(new StreamReader(_process.StandardOutput.BaseStream, utf8));

            // Buffer stderr (last 5 lines) and emit synthetic error event on unexpected exit.
            var stderrReader  = new StreamReader(_process.StandardError.BaseStream, utf8);
            var stderrRing    = _stderrRing;
            var linesQueue    = _lines;
            var capturedProc  = _process; // capture before thread start to avoid TOCTOU with Dispose
            var errThread = new System.Threading.Thread(() =>
            {
                try
                {
                    string line;
                    while ((line = stderrReader.ReadLine()) != null)
                        stderrRing.Add(line);
                }
                catch { }
                finally
                {
                    var exitCode = -1;
                    try { exitCode = capturedProc.ExitCode; } catch { }
                    // Surface only genuine failures: not an intentional Dispose, and a non-zero/unknown exit.
                    // (Clean exit 0 = normal completion; do NOT depend on the racy _running flag here.)
                    if (!_disposing && exitCode != 0)
                    {
                        var msg = StderrRingBuffer.BuildExitErrorMessage(exitCode, stderrRing.Lines);
                        linesQueue.Enqueue(
                            $"{{\"type\":\"result\",\"is_error\":true,\"error\":\"{JsonHelper.EscapeJson(msg)}\"}}");
                    }
                }
            }) { IsBackground = true, Name = "ChatProcess.StderrDrain" };
            errThread.Start();
        }

        private void ReadLoop(object state)
        {
            var reader = (StreamReader)state;
            try
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                    _lines.Enqueue(line);
            }
            catch (Exception ex)
            {
                // Sentinel: enqueue error line parseable by ChatStreamParser
                _lines.Enqueue($"{{\"type\":\"result\",\"is_error\":true,\"error\":\"{JsonHelper.EscapeJson(ex.Message)}\"}}");
            }
            finally
            {
                _running = false;
            }
        }

        /// <summary>Drain buffered stdout lines into output list.</summary>
        internal void DrainLines(List<string> output)
        {
            while (_lines.TryDequeue(out var line))
                output.Add(line);
        }

        internal void WriteLine(string json)
        {
            if (!IsRunning) return;
            _stdin.WriteLine(json);
            _stdin.Flush();
        }

        /// <summary>Close stdin without killing the process. Required for Codex (spike fact #4).</summary>
        internal void CloseStdin()
        {
            try { _stdin?.Close(); } catch { }
            _stdin = null;
        }

        public void Dispose()
        {
            _disposing = true;
            _running = false;
            try { _process?.Kill(); } catch { }
            try { _stdin?.Dispose(); } catch { }
            SessionState.EraseString(PidKey);
            _process = null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string[] QuoteArgs(string[] args)
        {
            var result = new string[args.Length];
            for (var i = 0; i < args.Length; i++)
                result[i] = ArgQuoting.Quote(args[i]);
            return result;
        }
    }
}
