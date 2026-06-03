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

        static ChatProcess()
        {
            AssemblyReloadEvents.beforeAssemblyReload += KillOrphan;
        }

        private static void KillOrphan()
        {
            var pidStr = SessionState.GetString(PidKey, "");
            if (!int.TryParse(pidStr, out var pid)) return;
            try { Process.GetProcessById(pid)?.Kill(); } catch { /* already gone */ }
            SessionState.EraseString(PidKey);
        }

        // ── Instance ──────────────────────────────────────────────────────────

        private Process _process;
        private StreamWriter _stdin;
        private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();
        private Thread _readerThread;
        private volatile bool _running;

        internal bool IsRunning => _running && _process != null && !_process.HasExited;

        internal void Spawn(string binaryPath, string[] args, string[] stripEnvKeys)
        {
            if (IsRunning) return;

            var psi = new ProcessStartInfo(binaryPath)
            {
                UseShellExecute        = false,
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
                Arguments              = string.Join(" ", QuoteArgs(args)),
            };

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

            // Drain stderr to prevent 64KB pipe fill / deadlock
            var stderrReader = new StreamReader(_process.StandardError.BaseStream, utf8);
            var errThread = new System.Threading.Thread(() =>
            {
                try { while (stderrReader.ReadLine() != null) { } }
                catch { }
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

        public void Dispose()
        {
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
