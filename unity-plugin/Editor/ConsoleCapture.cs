using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    [UnityEditor.InitializeOnLoad]
    public static class ConsoleCapture
    {
        private const int INIT_CAPACITY = 50;
        private const int RING_CAPACITY = 450;
        private const double INIT_WINDOW_SECONDS = 5.0;
        private const int MAX_STACKTRACE_LENGTH = 500;

        private static readonly LogEntry[] _initBuffer = new LogEntry[INIT_CAPACITY];
        private static int _initCount = 0;

        private static readonly LogEntry[] _ringBuffer = new LogEntry[RING_CAPACITY];
        private static int _ringHead = 0;   // next write position
        private static int _ringCount = 0;  // filled slots (0..RING_CAPACITY)

        private static bool _initPhaseOpen = true;
        private static DateTime _firstLogTime;
        private static bool _hasFirstLog = false;

        private static readonly object _lock = new object();

        private struct LogEntry
        {
            public string Message;
            public string StackTrace;
            public LogType Type;
            public DateTime Timestamp;
        }

        static ConsoleCapture()
        {
            Application.logMessageReceived += OnLogReceived;
        }

        private static void OnLogReceived(string message, string stackTrace, LogType type)
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                var entry = new LogEntry
                {
                    Message = message,
                    StackTrace = stackTrace != null && stackTrace.Length > MAX_STACKTRACE_LENGTH
                        ? stackTrace.Substring(0, MAX_STACKTRACE_LENGTH)
                        : stackTrace,
                    Type = type,
                    Timestamp = now
                };

                if (_initPhaseOpen)
                {
                    if (!_hasFirstLog) { _firstLogTime = now; _hasFirstLog = true; }

                    bool withinWindow = (now - _firstLogTime).TotalSeconds <= INIT_WINDOW_SECONDS;
                    if (_initCount < INIT_CAPACITY && withinWindow)
                    {
                        _initBuffer[_initCount++] = entry;
                        return;
                    }
                    _initPhaseOpen = false;
                }

                _ringBuffer[_ringHead % RING_CAPACITY] = entry;
                _ringHead++;
                if (_ringCount < RING_CAPACITY) _ringCount++;
            }
        }

        /// <summary>
        /// Get logs. count=-1 means all.
        /// first>0: return first N entries from init buffer + last (count-first) from ring.
        /// first=0: return last count from combined (init + ring in order).
        /// </summary>
        public static string GetLogs(int count = -1, string level = null, int first = 0)
        {
            lock (_lock)
            {
                var combined = BuildCombined();

                if (!string.IsNullOrEmpty(level) && Enum.TryParse<LogType>(level, true, out LogType filterType))
                    combined = FilterByType(combined, filterType);

                List<LogEntry> selected;
                if (first > 0 && count > 0)
                {
                    // first N from init, last (count-first) from ring
                    var initEntries = GetInitEntries(first);
                    var ringEntries = GetRingEntries(count - first);
                    selected = new List<LogEntry>(initEntries.Count + ringEntries.Count);
                    selected.AddRange(initEntries);
                    selected.AddRange(ringEntries);
                    if (!string.IsNullOrEmpty(level) && Enum.TryParse<LogType>(level, true, out LogType ft2))
                        selected = FilterByType(selected, ft2);
                }
                else if (count > 0)
                {
                    int skip = combined.Count > count ? combined.Count - count : 0;
                    selected = combined.GetRange(skip, combined.Count - skip);
                }
                else
                {
                    selected = combined;
                }

                var sb = new StringBuilder();
                foreach (var e in selected)
                    sb.AppendFormat("[{0}] {1:HH:mm:ss.fff} {2}\n", e.Type, e.Timestamp, e.Message);
                return sb.ToString().TrimEnd('\n');
            }
        }

        public static string GetErrorsSince(DateTime since, int maxCount = 5)
        {
            lock (_lock)
            {
                var combined = BuildCombined();
                var sb = new StringBuilder();
                int found = 0;
                foreach (var e in combined)
                {
                    if (found >= maxCount) break;
                    if (e.Timestamp > since && e.Type == LogType.Error)
                    {
                        sb.AppendLine(e.Message);
                        found++;
                    }
                }
                return sb.Length > 0 ? sb.ToString().TrimEnd() : null;
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _initCount = 0;
                _ringHead = 0;
                _ringCount = 0;
                _initPhaseOpen = true;
                _hasFirstLog = false;
            }
        }

        // --- helpers ---

        private static List<LogEntry> BuildCombined()
        {
            var list = new List<LogEntry>(_initCount + _ringCount);
            for (int i = 0; i < _initCount; i++)
                list.Add(_initBuffer[i]);
            AppendRingInOrder(list);
            return list;
        }

        private static void AppendRingInOrder(List<LogEntry> list)
        {
            if (_ringCount == 0) return;
            // oldest entry is at (_ringHead - _ringCount) % RING_CAPACITY
            int oldest = (_ringHead - _ringCount + RING_CAPACITY * 2) % RING_CAPACITY;
            for (int i = 0; i < _ringCount; i++)
                list.Add(_ringBuffer[(oldest + i) % RING_CAPACITY]);
        }

        private static List<LogEntry> GetInitEntries(int n)
        {
            int take = Math.Min(n, _initCount);
            var list = new List<LogEntry>(take);
            for (int i = 0; i < take; i++) list.Add(_initBuffer[i]);
            return list;
        }

        private static List<LogEntry> GetRingEntries(int n)
        {
            if (n <= 0) return new List<LogEntry>(0);
            var all = new List<LogEntry>(_ringCount);
            AppendRingInOrder(all);
            int skip = all.Count > n ? all.Count - n : 0;
            return all.GetRange(skip, all.Count - skip);
        }

        private static List<LogEntry> FilterByType(List<LogEntry> entries, LogType type)
        {
            var result = new List<LogEntry>(entries.Count);
            foreach (var e in entries)
                if (e.Type == type) result.Add(e);
            return result;
        }
    }
}
