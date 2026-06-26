using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityMCP.Editor
{
    internal static class SparklineHelper
    {
        private static readonly char[] Blocks = { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

        // values: last N samples. Returns Unicode sparkline string like "▁▂▃▅▇█▅▃".
        public static string Generate(IReadOnlyList<float> values, int width = 8)
        {
            if (values == null || values.Count == 0) return "";

            var samples = TakeRight(values, width);
            var min = samples.Min();
            var max = samples.Max();
            var range = max - min;

            var sb = new StringBuilder(samples.Count);
            foreach (var v in samples)
            {
                int idx = range < 0.0001f
                    ? Blocks.Length / 2
                    : (int)((v - min) / range * (Blocks.Length - 1));
                idx = Math.Max(0, Math.Min(Blocks.Length - 1, idx));
                sb.Append(Blocks[idx]);
            }
            return sb.ToString();
        }

        private static List<float> TakeRight(IReadOnlyList<float> values, int count)
        {
            int skip = values.Count > count ? values.Count - count : 0;
            var result = new List<float>(Math.Min(count, values.Count));
            for (int i = skip; i < values.Count; i++)
                result.Add(values[i]);
            return result;
        }
    }
}
