// Pure bounded ring buffer for stderr lines + exit-error message formatter.
// No Unity deps — fully unit-testable.
using System.Collections.Generic;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    /// <remarks>Not thread-safe: call Add and read Lines from the same (stderr drain) thread, or only after it exits.</remarks>
    internal sealed class StderrRingBuffer
    {
        private readonly string[] _buf;
        private int _count;
        private int _head; // next write slot (circular)

        internal StderrRingBuffer(int capacity)
        {
            _buf = new string[capacity];
        }

        internal void Add(string line)
        {
            _buf[_head] = line;
            _head = (_head + 1) % _buf.Length;
            if (_count < _buf.Length) _count++;
        }

        // Returns lines in insertion order (oldest first), up to capacity entries.
        internal IEnumerable<string> Lines
        {
            get
            {
                // Start index of oldest entry in the circular buffer.
                int start = _count < _buf.Length ? 0 : _head;
                for (int i = 0; i < _count; i++)
                    yield return _buf[(start + i) % _buf.Length];
            }
        }

        // Pure formatter — terse (token economy).
        internal static string BuildExitErrorMessage(int exitCode, IEnumerable<string> lastStderr, string binaryName = "claude")
        {
            var sb = new StringBuilder();
            sb.Append($"{binaryName} exited (code {exitCode})");
            bool hasLines = false;
            foreach (var line in lastStderr)
            {
                if (!hasLines) { sb.Append(": "); hasLines = true; }
                else sb.Append(" | ");
                sb.Append(line);
            }
            return sb.ToString();
        }
    }
}
