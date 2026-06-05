// Wave 3 — TokenSpan: represents one chip's FFFC+NBSP run in the text buffer.
// Pure readonly struct + pure static helpers. No Unity dependency.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Inclusive [Start, End] index range of a chip reservation (U+FFFC + N x U+00A0).
    /// </summary>
    internal readonly struct TokenSpan
    {
        internal readonly int Start; // index of U+FFFC
        internal readonly int End;   // index of last U+00A0 (== Start when N==0)

        internal TokenSpan(int start, int end)
        {
            Start = start;
            End   = end;
        }

        // ── Static helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Scan <paramref name="text"/> and return one TokenSpan per U+FFFC reservation.
        /// End = Start + N (last NBSP index), or Start when no NBSP follow.
        /// </summary>
        internal static List<TokenSpan> ComputeTokenSpans(string text)
        {
            var spans = new List<TokenSpan>();
            if (string.IsNullOrEmpty(text)) return spans;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != NbspReservation.FFfcChar) continue;
                int n    = CountTrailingNbsp(text, i);   // count NBSP after FFFC
                int end  = i + n;                         // inclusive last char of run
                spans.Add(new TokenSpan(i, end));
                i = end; // skip past the run (loop i++ will move one more, fine)
            }

            return spans;
        }

        /// <summary>
        /// Count trailing NBSP chars immediately following the U+FFFC at
        /// <paramref name="fffcIndex"/> in <paramref name="text"/>.
        /// </summary>
        internal static int CountTrailingNbsp(string text, int fffcIndex)
        {
            int pos   = fffcIndex + 1;
            int count = 0;
            while (pos < text.Length && text[pos] == NbspReservation.NbspChar)
            {
                count++; pos++;
            }
            return count;
        }

        /// <summary>Returns true if <paramref name="caretPos"/> falls within any span.</summary>
        internal static bool IsInsideSpan(IReadOnlyList<TokenSpan> spans, int caretPos)
            => SpanIndexAtCaret(spans, caretPos) >= 0;

        /// <summary>
        /// Returns the chip index (0-based) whose span contains <paramref name="caretPos"/>,
        /// or -1 if outside all spans.
        /// </summary>
        internal static int SpanIndexAtCaret(IReadOnlyList<TokenSpan> spans, int caretPos)
        {
            for (int i = 0; i < spans.Count; i++)
            {
                if (caretPos >= spans[i].Start && caretPos <= spans[i].End)
                    return i;
            }
            return -1;
        }
    }
}
