// Partial: inline chip methods — simplified with InlineChipField (Wave 0).
// No FFFC/NBSP markers. Clean text by construction.
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private InlineChipField _chipField;

        internal void InsertInlineChip(GameObject go)
        {
            if (go == null) return;
            var path = ComponentSerializer.GetPath(go);
            InsertInlineChip(go, path, go.name);
        }

        internal void InsertInlineChip(Object cap, string path, string displayName)
        {
            if (string.IsNullOrEmpty(path)) return;
            var assetPath  = cap != null ? AssetDatabase.GetAssetPath(cap) : path;
            var kindKey    = ChipKindDetector.Detect(cap, assetPath ?? path);
            var instanceID = cap != null ? cap.GetInstanceID() : 0;

            var tf     = _chipField?.TextField;
            int cursor = tf != null
                ? System.Math.Clamp(tf.cursorIndex, 0, (tf.value ?? "").Length) : 0;

            _chipField?.AddChip(new ChipData(kindKey, path, displayName, instanceID));

            // Insert @name at cursor position so text reads naturally inline.
            if (tf != null)
            {
                var mention = "@" + displayName + " ";
                var current = tf.value ?? "";
                tf.value = current.Insert(cursor, mention);
                tf.selectIndex = tf.cursorIndex = cursor + mention.Length;
                tf.Focus();
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
