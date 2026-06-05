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

        internal static void InsertChip(InlineChipField field, ChipData chip, string displayName)
        {
            var tf     = field.TextField;
            int cursor = tf.cursorIndex;
            field.AddChip(chip);
            var mention = "@" + displayName + " ";
            tf.value = (tf.value ?? "").Insert(cursor, mention);
            tf.selectIndex = tf.cursorIndex = cursor + mention.Length;
        }

        internal static void SetCursor(InlineChipField field, int pos)
        {
            field.TextField.cursorIndex = pos;
            field.TextField.selectIndex = pos;
        }

        internal static void Type(InlineChipField field, string text)
        {
            field.Text = (field.Text ?? "") + text;
            SetCursor(field, field.Text.Length);
        }

        internal static (string turnJson, string rawText) SimulateSend(
            InlineChipField field, ChatTranscript transcript, ChipConfig cfg)
        {
            var rawText  = (field.Text ?? "").Trim();
            var snapshot = field.Model.Count > 0 ? new List<ChipData>(field.Model.Chips) : null;
            var llmText  = rawText;
            if (field.Model.Count > 0)
            {
                var ctx = field.Model.SerializePayload(cfg);
                if (!string.IsNullOrEmpty(ctx)) llmText += "\n" + ctx;
            }
            if (string.IsNullOrEmpty(llmText)) return (null, rawText);
            var turnJson = UserTurnBuilder.Build(llmText);
            transcript.AppendUserBubble(rawText, snapshot);
            field.ClearChips();
            field.Text = "";
            return (turnJson, rawText);
        }
    }
}
