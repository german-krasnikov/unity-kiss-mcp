// Partial MCPChatWindow — send path: OnSend, DispatchTurn.
// F13: AppendChipContext removed — ChipTextInterleaver handles chip serialization.
// Text is clean by construction (InlineChipField — no FFFC/NBSP stripping needed).
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private void OnSend()
        {
            if (!_activity.CanSend) return;
            _autoFix.Disarm();

            var store    = BackendConfigStore.Load();
            var rawText  = (_chipField?.Text ?? _input?.value ?? "").Trim();
            var msg      = ChipTextInterleaver.BuildFromRaw(rawText, _chipField?.Model?.PositionedChips);
            var llmText  = ChipTextInterleaver.ToLlmPayload(msg, store.Chips);
            if (string.IsNullOrEmpty(llmText)) return;

            DispatchTurn(UserTurnBuilder.Build(llmText), msg, llmText);
        }

        // Shared send sequence.
        private void DispatchTurn(string turnJson, UserMessage displayMsg,
            string llmPayload, string screenshotPath = null)
        {
            ReloadGuard.OnTurnStarted();
            var displayText = ChipTextInterleaver.ToDisplayText(displayMsg);
            _undoTracker.OnTurnStart(displayText.Length > 40
                ? displayText.Substring(0, 40)
                : displayText);
            _sentTextCache.Set(displayText);
            _sentLlmCache.Set(llmPayload); // task#10: persist the EXACT full-path bytes sent this turn
            _transcript.SetLastTurnChips(displayMsg.Chips);
            _transcript.AppendUserBubble(displayMsg, llmPayload, screenshotPath);
            _backend.SendTurn(turnJson);
            if (_chipField != null) { _chipField.ClearChips(); _chipField.Text = ""; }
            else if (_input != null) { _input.value = ""; _input.cursorIndex = _input.selectIndex = 0; }
            _heightCalc.Reset();
            ResetInputAreaHeight();
            if (_activity.Send()) OnActivityChanged();
        }

        // Overload accepting plain string — used by InjectCompileErrors and ApproveAndExecute.
        // For these callers the sent text equals displayText (no chip context block).
        private void DispatchTurn(string turnJson, string displayText,
            System.Collections.Generic.IReadOnlyList<ChipData> chipSnapshot = null,
            string screenshotPath = null)
        {
            var positioned = new System.Collections.Generic.List<PositionedChip>();
            if (chipSnapshot != null)
                foreach (var c in chipSnapshot)
                    positioned.Add(new PositionedChip(c, 0));
            var msg = ChipTextInterleaver.Build(displayText ?? "", positioned);
            DispatchTurn(turnJson, msg, llmPayload: displayText, screenshotPath: screenshotPath);
        }
    }
}
