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

        /// <summary>Serialize to LLM: text with @mentions + chip context block.</summary>
        internal static string ToLlmPayload(UserMessage msg, ChipConfig cfg)
        {
            var plainText = ToDisplayText(msg);

            var chipCtx = ChipContextResolver.ResolveAllTyped(
                new List<ChipData>(msg.Chips), cfg);

            if (string.IsNullOrEmpty(chipCtx)) return plainText;
            return plainText + "\n" + chipCtx;
        }

        /// <summary>Reconstruct display text with @mentions from a UserMessage.</summary>
        internal static string ToDisplayText(UserMessage msg)
        {
            var sb = new StringBuilder();
            foreach (var seg in msg.Segments)
            {
                if (seg.IsChip)
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                        sb.Append(' ');
                    sb.Append('@').Append(seg.Chip.DisplayName).Append(' ');
                }
                else
                {
                    sb.Append(seg.Text);
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// Build UserMessage from raw text that contains @mentions (from InlineChipField),
        /// stripping @mentions and remapping chip offsets to clean text positions.
        /// </summary>
        internal static UserMessage BuildFromRaw(string rawText,
            IReadOnlyList<PositionedChip> positioned)
        {
            rawText = rawText ?? "";
            if (positioned == null || positioned.Count == 0)
                return Build(rawText, new List<PositionedChip>());

            var sorted = new List<PositionedChip>(positioned);
            sorted.Sort((a, b) => a.TextOffset.CompareTo(b.TextOffset));

            var cleanText  = new StringBuilder();
            var cleanChips = new List<PositionedChip>();
            int rawPos     = 0;

            foreach (var pc in sorted)
            {
                int chipRawOffset = System.Math.Clamp(pc.TextOffset, rawPos, rawText.Length);
                // text before this chip (clean)
                if (chipRawOffset > rawPos)
                    cleanText.Append(rawText, rawPos, chipRawOffset - rawPos);
                cleanChips.Add(new PositionedChip(pc.Chip, cleanText.Length));
                // skip "@DisplayName " (or "@DisplayName" at end of string)
                int mentionWithSpace    = chipRawOffset + 1 + pc.Chip.DisplayName.Length + 1;
                int mentionWithoutSpace = mentionWithSpace - 1;
                if (mentionWithSpace <= rawText.Length)
                    rawPos = mentionWithSpace;
                else if (mentionWithoutSpace <= rawText.Length)
                    rawPos = mentionWithoutSpace;
                else
                    rawPos = chipRawOffset; // guard: don't skip if mention not found
            }
            if (rawPos < rawText.Length)
                cleanText.Append(rawText, rawPos, rawText.Length - rawPos);

            return Build(cleanText.ToString(), cleanChips);
        }
    }
}
