// Shared test helpers for chip sequence / send flow tests.
using System.Collections.Generic;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    internal static class ChipTestHelpers
    {
        internal static ChipData H(string path, string name, int id = 0)
            => new ChipData(ChipKindKeys.Hierarchy, path, name, id);

        internal static ChipData S(string path, string name)
            => new ChipData(ChipKindKeys.Script, path, name, 0);

        // F13: no @mention injection — chips are position-tracked only.
        internal static void InsertChip(InlineChipField field, ChipData chip)
            => field.AddChip(chip);

        internal static void SetCursor(InlineChipField field, int pos)
        {
            int clamped = System.Math.Min(pos, (field.Text ?? "").Length);
            field.TextField.cursorIndex = clamped;
            field.TextField.selectIndex = clamped;
        }

        internal static void Type(InlineChipField field, string text)
        {
            field.Text = (field.Text ?? "") + text;
            SetCursor(field, field.Text.Length);
        }

        // F13: uses ChipTextInterleaver + AppendUserBubble(UserMessage).
        internal static (string turnJson, string rawText) SimulateSend(
            InlineChipField field, ChatTranscript transcript, ChipConfig cfg)
        {
            var rawText    = (field.Text ?? "").Trim();
            var positioned = new List<PositionedChip>(field.Model.PositionedChips);
            var msg        = ChipTextInterleaver.Build(rawText, positioned);
            var llmText    = ChipTextInterleaver.ToLlmPayload(msg, cfg);
            if (string.IsNullOrEmpty(llmText)) return (null, rawText);
            var turnJson = UserTurnBuilder.Build(llmText);
            transcript.SetLastTurnChips(msg.Chips);
            transcript.AppendUserBubble(msg);
            field.ClearChips();
            field.Text = "";
            SetCursor(field, 0);
            return (turnJson, rawText);
        }
    }
}
