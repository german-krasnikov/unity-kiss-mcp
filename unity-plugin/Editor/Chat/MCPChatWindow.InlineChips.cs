// Partial: inline chip methods and fields extracted to keep MCPChatWindow.cs under 200 lines.
// H6: ChipKind → string kindKey; ChipData uses string ctor.
// H10: NBSP insertion gated by UitkCharRect.IsAvailable; bare FFFC marker used otherwise.
// H12: expectedNbspCount tracked per chip via Add(ChipData, int nbspCount) overload.
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private InlineChipTracker _chipTracker;
        private InlineChipOverlay _chipOverlay;

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

            // H10: if positioning API is available, use NBSP reservation to hold pill width.
            // Otherwise, keep bare FFFC marker as today.
            string insertion;
            int    nbspCount;
            if (UitkCharRect.IsAvailable)
            {
                nbspCount = _defaultNbspN; // default 4; corrected after first GeometryChanged (H13)
                insertion = NbspReservation.BuildReservation(nbspCount);
            }
            else
            {
                nbspCount = 0;
                insertion = InlineChipTracker.Marker.ToString(); // bare FFFC
            }

            _input.value = cur.Substring(0, caret) + insertion + cur.Substring(caret);
            _input.SelectRange(caret + insertion.Length, caret + insertion.Length);

            var instanceID = cap != null ? cap.GetInstanceID() : 0;
            var assetPath  = cap != null ? AssetDatabase.GetAssetPath(cap) : path;
            var kindKey    = ChipKindDetector.Detect(cap, assetPath ?? path);
            // H12: track expected NBSP count alongside chip data.
            _chipTracker.Add(new ChipData(kindKey, path, displayName, instanceID), nbspCount);
            _chipOverlay.Refresh();
            _input.Focus();
            UpdateAutoHeight();
        }

        private void RemoveInlineChipAt(int chipIndex)
        {
            if (chipIndex < 0 || chipIndex >= _chipTracker.Count) return;

            var text = _input.value ?? "";
            int nth  = -1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == InlineChipTracker.Marker)
                {
                    nth++;
                    if (nth == chipIndex)
                    {
                        // Remove the FFFC + any trailing NBSP (the full reservation).
                        int end = i + 1;
                        while (end < text.Length && text[end] == NbspReservation.NbspChar) end++;
                        _input.value = text.Remove(i, end - i);
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
