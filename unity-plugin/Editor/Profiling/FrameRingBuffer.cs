namespace UnityMCP.Editor.Profiling
{
    /// <summary>
    /// Fixed-capacity circular buffer for FrameSample. Main-thread only (no locks needed).
    /// Capacity = 600 frames (~10s at 60fps).
    /// </summary>
    internal sealed class FrameRingBuffer
    {
        private readonly FrameSample[] _buf;
        private int _head;   // next write index (absolute, never wraps)
        private int _count;  // filled slots, capped at capacity

        internal FrameRingBuffer(int capacity) => _buf = new FrameSample[capacity];

        internal void Add(FrameSample s)
        {
            _buf[_head % _buf.Length] = s;
            if (++_head < 0) _head = _buf.Length; // guard int overflow (~414 days at 60fps)
            if (_count < _buf.Length) _count++;
        }

        internal void Clear()
        {
            _head = 0;
            _count = 0;
        }

        internal int Count => _count;
        internal int Capacity => _buf.Length;

        /// <summary>Returns _count samples in chronological order (oldest first).</summary>
        internal FrameSample[] ToArray()
        {
            if (_count == 0) return System.Array.Empty<FrameSample>();
            var result = new FrameSample[_count];
            // When unfilled: elements start at index 0.
            // When full (overflow): oldest element is at _head % capacity.
            int start = _count < _buf.Length ? 0 : _head % _buf.Length;
            for (int i = 0; i < _count; i++)
                result[i] = _buf[(start + i) % _buf.Length];
            return result;
        }

        /// <summary>
        /// Copies samples chronologically into pre-allocated dest. Zero-alloc path.
        /// Returns number of elements copied (min of Count and dest.Length).
        /// </summary>
        internal int CopyTo(FrameSample[] dest)
        {
            int start = _count < _buf.Length ? 0 : _head % _buf.Length;
            int n = System.Math.Min(_count, dest.Length);
            for (int i = 0; i < n; i++)
                dest[i] = _buf[(start + i) % _buf.Length];
            return n;
        }
    }
}
