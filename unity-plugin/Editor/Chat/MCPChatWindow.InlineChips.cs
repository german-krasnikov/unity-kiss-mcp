// Partial: inline chip methods and fields extracted to keep MCPChatWindow.cs under 200 lines.
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private InlineChipTracker _chipTracker;
        private InlineChipOverlay _chipOverlay;

        /// <summary>Insert a chip for the active scene GameObject at cursor.</summary>
        internal void InsertInlineChip(GameObject go)
        {
            if (go == null) return;
            var path = ComponentSerializer.GetPath(go);
            InsertInlineChip(go, path, go.name);
        }

        internal void InsertInlineChip(Object cap, string path, string displayName)
        {
            if (string.IsNullOrEmpty(path)) return;
            var cur   = _input.value ?? "";
            var caret = Mathf.Clamp(_input.cursorIndex, 0, cur.Length);
            _input.value = cur.Substring(0, caret)
                         + InlineChipTracker.Marker
                         + cur.Substring(caret);
            _input.SelectRange(caret + 1, caret + 1);

            var instanceID = cap != null ? cap.GetInstanceID() : 0;
            var assetPath  = cap != null ? AssetDatabase.GetAssetPath(cap) : path;
            var kind       = ChipKindDetector.Detect(cap, assetPath ?? path);
            _chipTracker.Add(new ChipData(kind, path, displayName, instanceID));
            _chipOverlay.Refresh();
            _input.Focus();
            UpdateAutoHeight();
        }

        private void RemoveInlineChipAt(int chipIndex)
        {
            if (chipIndex < 0 || chipIndex >= _chipTracker.Count) return;

            // Find and remove the chipIndex-th U+FFFC marker from the text.
            // Setting _input.value triggers ValueChangedCallback which calls SyncToText,
            // so tracker + overlay are updated there — do NOT touch them here.
            var text = _input.value ?? "";
            int nth  = -1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == InlineChipTracker.Marker)
                {
                    nth++;
                    if (nth == chipIndex)
                    {
                        _input.value = text.Remove(i, 1);
                        break;
                    }
                }
            }
            UpdateAutoHeight();
        }

        private void AddRefToContext(string refPath)
        {
            if (string.IsNullOrEmpty(refPath)) return;
            var display = refPath.Contains("/")
                ? refPath.Substring(refPath.LastIndexOf('/') + 1)
                : refPath;
            InsertInlineChip(null, refPath, display);
        }
    }
}
