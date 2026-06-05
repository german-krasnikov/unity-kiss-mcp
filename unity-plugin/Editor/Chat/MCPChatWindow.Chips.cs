// Chip methods extracted to a partial to keep MCPChatWindow.cs under 200 lines.
// H6: ChipKind → string kindKey throughout; ShortPrefix removed.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private static void OnDragUpdated(DragUpdatedEvent e)
        {
            if (DragAndDrop.objectReferences.Length > 0)
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
        }

        private void OnDragPerform(DragPerformEvent e)
        {
            DragAndDrop.AcceptDrag();
            // Use _chipField bounds (the full composed control) so drops on pill area are detected.
            bool dropOnField = _chipField != null
                ? _chipField.worldBound.Contains(e.mousePosition)
                : _input != null && _input.worldBound.Contains(e.mousePosition);

            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj == null) continue;

                if (obj is GameObject go && !AssetDatabase.Contains(go))
                {
                    var path    = ComponentSerializer.GetPath(go);
                    var kindKey = ChipKindDetector.Detect(go, null);
                    if (dropOnField)
                        InsertInlineChip(go, path, go.name);
                    else
                        AddChip(obj, path, go.name, kindKey, go.GetInstanceID());
                    continue;
                }

                if (AssetDatabase.Contains(obj) && ChatChipPolicy.IsAllowedAssetType(obj.GetType()))
                {
                    var assetPath = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        var kindKey = ChipKindDetector.Detect(obj, assetPath);
                        if (dropOnField)
                            InsertInlineChip(obj, assetPath, obj.name);
                        else
                            AddChip(obj, assetPath, obj.name, kindKey, 0);
                    }
                }
                else if (!(obj is GameObject))
                    Debug.LogWarning($"[MCP Chat] {obj.GetType().Name} not supported as a context chip");
            }
        }

        private void AddObjChip(GameObject go) =>
            AddChip(go, ComponentSerializer.GetPath(go), go.name, ChipKindDetector.Detect(go, null), go.GetInstanceID());

        private void AddChip(Object cap, string payload, string displayName, string kindKey = null, int instanceID = 0)
        {
            kindKey = kindKey ?? ChipKindKeys.Asset;
            var chip = new VisualElement(); chip.AddToClassList("obj-chip");
            chip.userData = new ChipData(kindKey, payload, displayName, instanceID);

            // kindKey IS the prefix — no ShortPrefix call (H6)
            var kindLbl = new Label(kindKey + ":");
            kindLbl.AddToClassList("obj-chip-kind");

            var lbl = new Label(displayName); lbl.AddToClassList("obj-chip-label");
            lbl.RegisterCallback<ClickEvent>(_ => { EditorGUIUtility.PingObject(cap); Selection.activeObject = cap; });

            var removeBtn = new Button(() =>
            {
                _objChipStrip.Remove(chip);
                UpdateAutoHeight();
            }) { text = "✕" };
            removeBtn.AddToClassList("obj-chip-remove");

            chip.Add(kindLbl);
            chip.Add(lbl);
            chip.Add(removeBtn);
            _objChipStrip.Add(chip);
            UpdateAutoHeight();
        }

        private List<ChipData> CollectChipData()
        {
            var seen   = new HashSet<string>();
            var result = new List<ChipData>();
            foreach (var c in _objChipStrip.Children())
            {
                if (c.userData is ChipData cd && seen.Add(cd.Path))
                    result.Add(cd);
            }
            return result;
        }

        private List<string> CollectChipPaths()
        {
            var result = new List<string>();
            foreach (var cd in CollectChipData()) result.Add(cd.Path);
            return result;
        }
    }
}
