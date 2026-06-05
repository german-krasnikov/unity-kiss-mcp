// Wave 3 — NBSP reservation helpers. Pure static, no Unity dependency.
// H12/MF3: FindCorruptedChips validates each U+FFFC has expected trailing NBSP count.
// MF4: ComputeN floors to 1 when advance <= 0.
// H13: caller measures actual advance after first GeometryChanged; default N=4 until then.
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat
{
    internal static class NbspReservation
    {
        // U+FFFC OBJECT REPLACEMENT CHARACTER — pill anchor marker
        internal const char   FFfcChar = '￼';
        internal const string FFFC     = "￼";

        // U+00A0 NO-BREAK SPACE — fills the horizontal pill reservation
        internal const char   NbspChar = ' ';
        internal const string NBSP     = " ";

        // Regex: one U+FFFC followed by zero-or-more U+00A0
        private static readonly Regex _reservationRun =
            new Regex("￼\u00A0*", RegexOptions.Compiled);

        /// <summary>
        /// Number of NBSP chars needed to fill <paramref name="pillWidth"/> pixels.
        /// MF4: floors to 1 if <paramref name="nbspAdvance"/> &lt;= 0.
        /// </summary>
        internal static int ComputeN(float pillWidth, float nbspAdvance)
        {
            if (nbspAdvance <= 0f) return 1;
            return Math.Max(1, (int)Math.Ceiling(pillWidth / nbspAdvance));
        }

        /// <summary>FFFC + n x NBSP.</summary>
        internal static string BuildReservation(int n)
        {
            var sb = new StringBuilder(1 + n);
            sb.Append(FFfcChar);
            for (int i = 0; i < n; i++) sb.Append(NbspChar);
            return sb.ToString();
        }

        /// <summary>Remove all FFFC+trailing-NBSP runs from <paramref name="text"/>.</summary>
        internal static string StripReservation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return _reservationRun.Replace(text, "");
        }

        /// <summary>
        /// H12/MF3: for each U+FFFC in <paramref name="text"/>, verify it is followed by
        /// exactly <paramref name="expectedNbspCounts"/>[i] NBSP characters.
        /// Returns indices of chips whose count is wrong.
        /// </summary>
        internal static List<int> FindCorruptedChips(
            string text,
            IReadOnlyList<int> expectedNbspCounts)
        {
            var corrupted = new List<int>();
            if (string.IsNullOrEmpty(text) || expectedNbspCounts == null) return corrupted;

            int chipIndex = 0;
            int i         = 0;
            while (i < text.Length && chipIndex < expectedNbspCounts.Count)
            {
                if (text[i] != FFfcChar) { i++; continue; }

                int actual = CountTrailingNbspAt(text, i + 1);
                if (actual != expectedNbspCounts[chipIndex])
                    corrupted.Add(chipIndex);

                // advance past FFFC + its NBSP run
                i += 1 + actual;
                chipIndex++;
            }

            return corrupted;
        }

        // Count consecutive NBSP chars starting at pos in text.
        private static int CountTrailingNbspAt(string text, int pos)
        {
            int count = 0;
            while (pos < text.Length && text[pos] == NbspChar) { count++; pos++; }
            return count;
        }
    }
}
