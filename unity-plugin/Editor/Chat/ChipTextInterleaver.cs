// ChipTextInterleaver: builds a UserMessage (interleaved text+chip segments) at send time.
// Pure static — no Unity dependencies. Fixes Bug 1 (double display in bubble).
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    /// <summary>Immutable snapshot of a user message for display + LLM serialization.</summary>
    internal readonly struct UserMessage
    {
        internal readonly IReadOnlyList<MessageSegment> Segments;
        internal readonly IReadOnlyList<ChipData>       Chips;

        internal UserMessage(IReadOnlyList<MessageSegment> segments, IReadOnlyList<ChipData> chips)
        {
            Segments = segments ?? new List<MessageSegment>();
            Chips    = chips    ?? new List<ChipData>();
        }
    }

    /// <summary>One run in a UserMessage: either a text run or a chip reference.</summary>
    internal readonly struct MessageSegment
    {
        internal readonly bool     IsChip;
        internal readonly string   Text;
        internal readonly ChipData Chip;

        internal MessageSegment(string text)  { IsChip = false; Text = text ?? ""; Chip = default; }
        internal MessageSegment(ChipData chip) { IsChip = true;  Text = "";         Chip = chip; }
    }

    /// <summary>Converts positioned chips + text into an interleaved UserMessage.</summary>
    internal static class ChipTextInterleaver
    {
        /// <summary>Build UserMessage from text + positioned chips.</summary>
        internal static UserMessage Build(string text, IReadOnlyList<PositionedChip> positioned)
        {
            text = text ?? "";
            var sorted   = (positioned ?? new List<PositionedChip>())
                .OrderBy(p => p.TextOffset).ToList();
            var segments = new List<MessageSegment>();
            int pos = 0;

            foreach (var pc in sorted)
            {
                int offset = System.Math.Min(System.Math.Max(pc.TextOffset, pos), text.Length);
                if (offset > pos)
                    segments.Add(new MessageSegment(text.Substring(pos, offset - pos)));
                segments.Add(new MessageSegment(pc.Chip));
                pos = offset;
            }
            if (pos < text.Length)
                segments.Add(new MessageSegment(text.Substring(pos)));
            if (segments.Count == 0)
                segments.Add(new MessageSegment(""));

            var chips = sorted.Select(p => p.Chip).ToList();
            return new UserMessage(segments, chips);
        }

        /// <summary>Serialize to LLM: plain text + chip context block.</summary>
        internal static string ToLlmPayload(UserMessage msg, ChipConfig cfg)
        {
            var sb = new StringBuilder();
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) sb.Append(seg.Text);
            var plainText = sb.ToString().Trim();

            var chipCtx = ChipContextResolver.ResolveAllTyped(
                new List<ChipData>(msg.Chips), cfg);

            if (string.IsNullOrEmpty(chipCtx)) return plainText;
            return plainText + "\n" + chipCtx;
        }

        /// <summary>Reconstruct display text (text segments only) from a UserMessage.</summary>
        internal static string ToDisplayText(UserMessage msg)
        {
            var sb = new StringBuilder();
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) sb.Append(seg.Text);
            return sb.ToString().Trim();
        }
    }
}
