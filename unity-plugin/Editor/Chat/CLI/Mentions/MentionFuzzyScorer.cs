using System;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Allocation-free fuzzy scorer with bitmask pre-filter.
    /// All spans are lowercase; candidateOrig is used only for CamelCase detection.
    /// Returns 0 for no match.
    /// </summary>
    internal static class MentionFuzzyScorer
    {
        internal static uint BuildCharMask(ReadOnlySpan<char> lower)
        {
            uint mask = 0;
            foreach (char c in lower)
                if (c >= 'a' && c <= 'z') mask |= 1u << (c - 'a');
            return mask;
        }

        internal static bool PassesPreFilter(uint queryMask, uint candidateMask)
            => (queryMask & candidateMask) == queryMask;

        internal static long Score(
            ReadOnlySpan<char> pattern,
            ReadOnlySpan<char> candidateLower,
            ReadOnlySpan<char> candidateOrig)
        {
            if (pattern.IsEmpty || candidateLower.IsEmpty) return 0;

            // Exact match
            if (pattern.Equals(candidateLower, StringComparison.Ordinal)) return 1000;

            // Short query (≤2): prefix/substring fast path only
            if (pattern.Length <= 2)
                return ShortScore(pattern, candidateLower);

            // Prefix match bonus
            if (candidateLower.StartsWith(pattern, StringComparison.Ordinal)) return 500 + pattern.Length;

            return FuzzyScore(pattern, candidateLower, candidateOrig);
        }

        private static long ShortScore(ReadOnlySpan<char> pattern, ReadOnlySpan<char> lower)
        {
            if (lower.StartsWith(pattern, StringComparison.Ordinal)) return 500 + pattern.Length;
            // Substring at any position: return position-based score
            int idx = IndexOf(lower, pattern);
            if (idx < 0) return 0;
            return 100 - idx; // prefix wins over suffix
        }

        private static long FuzzyScore(
            ReadOnlySpan<char> pattern,
            ReadOnlySpan<char> lower,
            ReadOnlySpan<char> orig)
        {
            long score      = 0;
            int  pi         = 0;
            int  firstMatch = -1;
            int  consecutive = 0;

            for (int ci = 0; ci < lower.Length && pi < pattern.Length; ci++)
            {
                if (lower[ci] != pattern[pi]) { consecutive = 0; continue; }

                if (firstMatch < 0) firstMatch = ci;
                pi++;
                consecutive++;

                // Word-start bonus
                bool wordStart = ci == 0
                    || lower[ci - 1] == '/' || lower[ci - 1] == '\\'
                    || lower[ci - 1] == '_' || lower[ci - 1] == '-'
                    || lower[ci - 1] == '.' || lower[ci - 1] == ' ';

                // CamelCase boundary: current orig is upper, previous orig is lower
                bool camel = ci > 0 && ci < orig.Length
                    && char.IsUpper(orig[ci]) && char.IsLower(orig[ci - 1]);

                // Path separator bonus
                bool pathSep = ci > 0 && (lower[ci - 1] == '/' || lower[ci - 1] == '\\');

                // Delimiter bonus
                bool delim = ci > 0 && (lower[ci - 1] == '_' || lower[ci - 1] == '-'
                    || lower[ci - 1] == '.' || lower[ci - 1] == ' ');

                if (wordStart || ci == 0) score += 16;
                else if (camel)           score += 2;

                if (pathSep) score += 5;
                else if (delim) score += 4;

                // Consecutive bonus
                score += consecutive <= 3 ? 6 : 3;

            }

            if (pi < pattern.Length) return 0; // not all chars matched

            // Late-start penalty
            if (firstMatch > 0) score -= firstMatch * 2;

            return score > 0 ? score : 0;
        }

        private static int IndexOf(ReadOnlySpan<char> source, ReadOnlySpan<char> value)
        {
            for (int i = 0; i <= source.Length - value.Length; i++)
                if (source.Slice(i, value.Length).Equals(value, StringComparison.Ordinal))
                    return i;
            return -1;
        }
    }
}
