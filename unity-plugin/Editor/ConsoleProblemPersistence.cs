using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Issue 27 (C1/C2/M9 fix): persists problem-type console entries (Error/Exception/Assert)
    /// to SessionState so they survive a domain reload — ConsoleCapture's in-memory ring
    /// buffers are wiped on reload, SessionState is not.
    ///
    /// Stores structured fields (type, message, timestamp) rather than pre-formatted text so
    /// ConsoleCapture can reconstruct real LogEntry values on the fallback path and run them
    /// through the SAME level/keyword/since filtering as the live path (C1: since-filtering,
    /// C2: level/keyword filtering no longer bypassed after a reload).
    /// </summary>
    internal static class ConsoleProblemPersistence
    {
        internal struct Problem
        {
            public LogType Type;
            public string Message;
            public DateTime Timestamp;
        }

        private const string LinesKey = "MCP_ConsoleProblems";
        private const string TypesKey = "MCP_ConsoleProblemTypes";
        private const string TimesKey = "MCP_ConsoleProblemTimes";
        private const int MAX_PERSISTED_PROBLEMS = 20;

        // Unit Separator — cannot appear in a log message, unlike '\n' which Debug.LogError
        // messages routinely embed (e.g. "Validation failed:\nMissing X"). Using '\n' as the
        // join/split delimiter desyncs the parallel messages/types/timestamps lists on restore.
        private const string Delimiter = "";

        private static readonly List<LogType> _types = new List<LogType>();
        private static readonly List<string> _messages = new List<string>();
        private static readonly List<DateTime> _timestamps = new List<DateTime>();

        /// <summary>Append a problem entry, trimming to MAX_PERSISTED_PROBLEMS (FIFO).
        /// Returns true if the append evicted an older entry — caller (ConsoleCapture)
        /// tracks that as a dropped-problem count so overflow isn't silent (M9).</summary>
        internal static bool Append(LogType type, string message, DateTime timestamp)
        {
            _types.Add(type);
            _messages.Add(message);
            _timestamps.Add(timestamp);

            bool evicted = _types.Count > MAX_PERSISTED_PROBLEMS;
            if (evicted)
            {
                _types.RemoveAt(0);
                _messages.RemoveAt(0);
                _timestamps.RemoveAt(0);
            }
            Persist();
            return evicted;
        }

        /// <summary>Problems newer than `since` — in-memory lists first (fast path, no reload
        /// happened yet), SessionState second (reload wiped the in-memory lists but not the
        /// persisted strings).</summary>
        internal static List<Problem> GetSince(DateTime since)
        {
            List<LogType> types; List<string> messages; List<DateTime> timestamps;
            if (_types.Count > 0)
            {
                types = _types; messages = _messages; timestamps = _timestamps;
            }
            else if (!TryLoadFromSessionState(out types, out messages, out timestamps))
            {
                return new List<Problem>(0);
            }

            var result = new List<Problem>(types.Count);
            for (int i = 0; i < types.Count; i++)
                if (timestamps[i] > since)
                    result.Add(new Problem { Type = types[i], Message = messages[i], Timestamp = timestamps[i] });
            return result;
        }

        internal static void Clear()
        {
            _types.Clear();
            _messages.Clear();
            _timestamps.Clear();
            UnityEditor.SessionState.EraseString(LinesKey);
            UnityEditor.SessionState.EraseString(TypesKey);
            UnityEditor.SessionState.EraseString(TimesKey);
        }

        /// <summary>Test seam: wipe in-memory lists only, leave SessionState untouched
        /// (mirrors a real domain reload).</summary>
        internal static void SimulateDomainReloadForTest()
        {
            _types.Clear();
            _messages.Clear();
            _timestamps.Clear();
        }

        private static void Persist()
        {
            UnityEditor.SessionState.SetString(LinesKey, string.Join(Delimiter, _messages));
            UnityEditor.SessionState.SetString(TypesKey, string.Join(Delimiter, TypeNames()));
            UnityEditor.SessionState.SetString(TimesKey, string.Join(Delimiter, TickStrings()));
        }

        private static IEnumerable<string> TypeNames()
        {
            foreach (var t in _types) yield return t.ToString();
        }

        private static IEnumerable<string> TickStrings()
        {
            foreach (var t in _timestamps) yield return t.Ticks.ToString();
        }

        private static bool TryLoadFromSessionState(out List<LogType> types, out List<string> messages,
                                                     out List<DateTime> timestamps)
        {
            types = new List<LogType>();
            messages = new List<string>();
            timestamps = new List<DateTime>();

            string linesText = UnityEditor.SessionState.GetString(LinesKey, "");
            if (string.IsNullOrEmpty(linesText)) return false;

            var lines = linesText.Split(Delimiter[0]);
            var typeNames = UnityEditor.SessionState.GetString(TypesKey, "").Split(Delimiter[0]);
            var ticks = UnityEditor.SessionState.GetString(TimesKey, "").Split(Delimiter[0]);

            int n = Math.Min(lines.Length, Math.Min(typeNames.Length, ticks.Length));
            for (int i = 0; i < n; i++)
            {
                if (!long.TryParse(ticks[i], out var t)) continue;
                if (!Enum.TryParse<LogType>(typeNames[i], out var type)) continue;
                messages.Add(lines[i]);
                types.Add(type);
                timestamps.Add(new DateTime(t));
            }
            return messages.Count > 0;
        }
    }
}
