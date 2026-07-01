using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Issue 27 (M14): bounded in-memory log capture — an initial "burst" window buffer
    /// (first N logs within the first few seconds after a domain load) plus a fixed-size
    /// ring buffer for everything after. Extracted out of ConsoleCapture to keep that file
    /// focused on the public query API + SessionState-fallback wiring.
    /// Entries here do NOT survive a domain reload — see ConsoleProblemPersistence for that.
    /// </summary>
    internal static class ConsoleRingBuffer
    {
        internal struct LogEntry
        {
            public string Message;
            public string StackTrace;
            public LogType Type;
            public DateTime Timestamp;
        }

        private const int INIT_CAPACITY = 50;
        private const int RING_CAPACITY = 450;
        private const double INIT_WINDOW_SECONDS = 5.0;

        private static readonly LogEntry[] _initBuffer = new LogEntry[INIT_CAPACITY];
        private static int _initCount = 0;

        private static readonly LogEntry[] _ringBuffer = new LogEntry[RING_CAPACITY];
        private static int _ringHead = 0;   // next write position
        private static int _ringCount = 0;  // filled slots (0..RING_CAPACITY)

        private static bool _initPhaseOpen = true;
        private static DateTime _firstLogTime;
        private static bool _hasFirstLog = false;

        /// <summary>Write an entry into the init window or the ring buffer. Returns true when
        /// the write evicted an existing ring-buffer slot, with `evicted` set to the entry that
        /// was overwritten — caller decides whether that eviction matters (e.g. a problem-type
        /// entry being silently dropped, Issue 27 Step 4).</summary>
        internal static bool Write(LogEntry entry, out LogEntry evicted)
        {
            evicted = default;

            if (_initPhaseOpen)
            {
                if (!_hasFirstLog) { _firstLogTime = entry.Timestamp; _hasFirstLog = true; }

                bool withinWindow = (entry.Timestamp - _firstLogTime).TotalSeconds <= INIT_WINDOW_SECONDS;
                if (_initCount < INIT_CAPACITY && withinWindow)
                {
                    _initBuffer[_initCount++] = entry;
                    return false;
                }
                _initPhaseOpen = false;
            }

            bool didEvict = _ringCount == RING_CAPACITY;
            if (didEvict) evicted = _ringBuffer[_ringHead % RING_CAPACITY];

            _ringBuffer[_ringHead % RING_CAPACITY] = entry;
            _ringHead++;
            if (_ringCount < RING_CAPACITY) _ringCount++;
            return didEvict;
        }

        internal static void Reset()
        {
            _initCount = 0;
            _ringHead = 0;
            _ringCount = 0;
            _initPhaseOpen = true;
            _hasFirstLog = false;
        }

        internal static List<LogEntry> BuildCombined()
        {
            var list = new List<LogEntry>(_initCount + _ringCount);
            for (int i = 0; i < _initCount; i++)
                list.Add(_initBuffer[i]);
            AppendRingInOrder(list);
            return list;
        }

        internal static List<LogEntry> GetInitEntries(int n)
        {
            int take = Math.Min(n, _initCount);
            var list = new List<LogEntry>(take);
            for (int i = 0; i < take; i++) list.Add(_initBuffer[i]);
            return list;
        }

        internal static List<LogEntry> GetRingEntries(int n)
        {
            if (n <= 0) return new List<LogEntry>(0);
            var all = new List<LogEntry>(_ringCount);
            AppendRingInOrder(all);
            int skip = all.Count > n ? all.Count - n : 0;
            return all.GetRange(skip, all.Count - skip);
        }

        private static void AppendRingInOrder(List<LogEntry> list)
        {
            if (_ringCount == 0) return;
            // oldest entry is at (_ringHead - _ringCount) % RING_CAPACITY
            int oldest = (_ringHead - _ringCount + RING_CAPACITY * 2) % RING_CAPACITY;
            for (int i = 0; i < _ringCount; i++)
                list.Add(_ringBuffer[(oldest + i) % RING_CAPACITY]);
        }

        internal static List<LogType> ParseLevels(string level)
        {
            if (string.IsNullOrEmpty(level)) return null;
            var types = new List<LogType>();
            foreach (var part in level.Split(','))
                if (Enum.TryParse<LogType>(part.Trim(), true, out var t))
                    types.Add(t);
            return types.Count > 0 ? types : null;
        }

        internal static List<LogEntry> FilterByTypes(List<LogEntry> entries, List<LogType> types)
        {
            if (types == null) return entries;
            var result = new List<LogEntry>(entries.Count);
            foreach (var e in entries)
                if (types.Contains(e.Type)) result.Add(e);
            return result;
        }

        internal static List<LogEntry> FilterByKeyword(List<LogEntry> entries, string kw)
        {
            var result = new List<LogEntry>(entries.Count);
            foreach (var e in entries)
                if (e.Message.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    result.Add(e);
            return result;
        }
    }
}
