// Partial MCPChatWindow — chip field wiring (Wave 0 replacement).
// Gutted from overlay/dirty-tick wiring to InlineChipField context menu wiring.
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        /// <summary>
        /// Wire the InlineChipField context menu for "Add Selection to Context"
        /// and "Copy as sent to LLM".
        /// Called from BuildInputArea.
        /// </summary>
        internal void WireChipInput()
        {
            if (_chipField == null) return;
            var menuManipulator = new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction(
                    "Add Selection to Context",
                    _ => InsertInlineChip(Selection.activeGameObject),
                    _ => Selection.activeGameObject != null
                        ? DropdownMenuAction.Status.Normal
                        : DropdownMenuAction.Status.Disabled);

                evt.menu.AppendAction(
                    CopyableText.LabelCopyAsSent,
                    _ =>
                    {
                        var payload = BuildInputLlmPayload();
                        if (!string.IsNullOrEmpty(payload))
                        {
                            EditorGUIUtility.systemCopyBuffer = payload;
                            CopyFlash.Show();
                        }
                    },
                    _ => (string.IsNullOrEmpty(_chipField?.Text) && (_chipField?.Model?.Count ?? 0) == 0)
                        ? DropdownMenuAction.Status.Disabled
                        : DropdownMenuAction.Status.Normal);
            });
            _chipField.AddManipulator(menuManipulator);
            _input?.AddManipulator(menuManipulator);
        }

        // Builds the LLM payload from the current input field state (same logic as OnSend).
        private string BuildInputLlmPayload()
        {
            var rawText = (_chipField?.Text ?? _input?.value ?? "").Trim();
            if (string.IsNullOrEmpty(rawText) && (_chipField?.Model?.Count ?? 0) == 0)
                return null;
            var store = BackendConfigStore.Load();
            var msg   = ChipTextInterleaver.BuildFromRaw(rawText, _chipField?.Model?.PositionedChips);
            return ChipTextInterleaver.ToLlmPayload(msg, store.Chips);
        }
    }
}
