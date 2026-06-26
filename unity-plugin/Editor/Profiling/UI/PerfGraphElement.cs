using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Profiling
{
    /// <summary>
    /// Reusable UITK graph via generateVisualContent + Painter2D.
    /// Internal float ring buffer (zero-alloc on PushValue). No Texture2D.
    /// </summary>
    internal sealed class PerfGraphElement : VisualElement
    {
        private readonly float[] _buf;
        private readonly float[] _renderScratch;
        private int _head;   // absolute write index
        private int _count;  // filled slots, capped at capacity

        internal Color LineColor { get; set; } = new Color(0.227f, 0.824f, 0.624f);      // #3ad29f
        internal Color FillColor { get; set; } = new Color(0.227f, 0.824f, 0.624f, 0.15f);
        internal float MinValue { get; set; }
        internal float MaxValue { get; set; } = 100f;

        internal int Count => _count;

        internal PerfGraphElement(int capacity = 120)
        {
            _buf = new float[capacity];
            _renderScratch = new float[capacity];
            AddToClassList("perf-graph");
            style.height = 60;
            generateVisualContent += OnGenerateVisual;
        }

        internal void PushValue(float value)
        {
            _buf[_head % _buf.Length] = value;
            if (++_head < 0) _head = _buf.Length;
            if (_count < _buf.Length) _count++;
            MarkDirtyRepaint();
        }

        internal void SetValues(float[] values)
        {
            _head = 0;
            _count = 0;
            foreach (var v in values)
            {
                _buf[_head % _buf.Length] = v;
                if (++_head < 0) _head = _buf.Length;
                if (_count < _buf.Length) _count++;
            }
            MarkDirtyRepaint();
        }

        /// <summary>Returns _count values in chronological order (oldest first).</summary>
        internal float[] GetValues()
        {
            var result = new float[_count];
            int start = _count < _buf.Length ? 0 : _head % _buf.Length;
            for (int i = 0; i < _count; i++)
                result[i] = _buf[(start + i) % _buf.Length];
            return result;
        }

        /// <summary>Copies values chronologically into dest. Zero-alloc paint path.</summary>
        internal int CopyValuesTo(float[] dest)
        {
            int start = _count < _buf.Length ? 0 : _head % _buf.Length;
            int n = System.Math.Min(_count, dest.Length);
            for (int i = 0; i < n; i++)
                dest[i] = _buf[(start + i) % _buf.Length];
            return n;
        }

        private void OnGenerateVisual(MeshGenerationContext mgc)
        {
            if (_count < 2) return;
            var p = mgc.painter2D;
            float w = contentRect.width;
            float h = contentRect.height;
            int n = CopyValuesTo(_renderScratch);
            float range = Mathf.Max(MaxValue - MinValue, 1f);

            // Filled area
            p.fillColor = FillColor;
            p.BeginPath();
            p.MoveTo(new Vector2(0, h));
            for (int i = 0; i < n; i++)
            {
                float x = i * w / (n - 1);
                float y = h - ((_renderScratch[i] - MinValue) / range) * h;
                p.LineTo(new Vector2(x, y));
            }
            p.LineTo(new Vector2(w, h));
            p.ClosePath();
            p.Fill();

            // Grid lines at 25/50/75%
            p.strokeColor = new Color(1f, 1f, 1f, 0.08f);
            p.lineWidth = 1f;
            foreach (float pct in new[] { 0.25f, 0.5f, 0.75f })
            {
                float y = h * (1f - pct);
                p.BeginPath();
                p.MoveTo(new Vector2(0, y));
                p.LineTo(new Vector2(w, y));
                p.Stroke();
            }

            // Line
            p.strokeColor = LineColor;
            p.lineWidth = 1.5f;
            p.BeginPath();
            for (int i = 0; i < n; i++)
            {
                float x = i * w / (n - 1);
                float y = h - ((_renderScratch[i] - MinValue) / range) * h;
                if (i == 0) p.MoveTo(new Vector2(x, y));
                else p.LineTo(new Vector2(x, y));
            }
            p.Stroke();
        }
    }
}
