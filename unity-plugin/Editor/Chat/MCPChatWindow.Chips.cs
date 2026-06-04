// Chip methods extracted to a partial to keep MCPChatWindow.cs under 200 lines.
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
            // F5: if drop lands inside the text field → inline chip; else strip chip
            bool dropOnField = _input != null && _input.worldBound.Contains(e.mousePosition);

            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj == null) continue;

                // Scene GameObject (not an asset).
                if (obj is GameObject go && !AssetDatabase.Contains(go))
                {
                    if (dropOnField)
                        InsertInlineChip(go, ComponentSerializer.GetPath(go), go.name);
                    else
                        AddChip(go, ComponentSerializer.GetPath(go), go.name);
                    continue;
                }

                // Asset — check type allowlist first.
                if (AssetDatabase.Contains(obj) && ChatChipPolicy.IsAllowedAssetType(obj.GetType()))
                {
                    var assetPath = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        if (dropOnField)
                            InsertInlineChip(obj, assetPath, obj.name);
                        else
                            AddChip(obj, assetPath, obj.name);
                    }
                }
                else if (!(obj is GameObject)) // not a scene GO, not an allowlisted asset
                    Debug.LogWarning($"[MCP Chat] {obj.GetType().Name} not supported as a context chip");
            }
        }

        // Kept for call-sites that pass a scene GameObject directly.
        private void AddObjChip(GameObject go) =>
            AddChip(go, ComponentSerializer.GetPath(go), go.name);

        private void AddChip(Object cap, string payload, string displayName)
        {
            var chip = new VisualElement(); chip.AddToClassList("obj-chip");
            chip.userData = payload;

            var lbl = new Label(displayName); lbl.AddToClassList("obj-chip-label");
            lbl.RegisterCallback<ClickEvent>(_ => { EditorGUIUtility.PingObject(cap); Selection.activeObject = cap; });

            var removeBtn = new Button(() =>
            {
                _objChipStrip.Remove(chip);
                UpdateAutoHeight();
            }) { text = "✕" };
            removeBtn.AddToClassList("obj-chip-remove");

            chip.Add(lbl);
            chip.Add(removeBtn);
            _objChipStrip.Add(chip);
            UpdateAutoHeight();
        }

        private List<string> CollectChipPaths()
        {
            var seen = new HashSet<string>();
            var result = new List<string>();
            foreach (var c in _objChipStrip.Children())
            {
                if (c.userData is string path && seen.Add(path))
                    result.Add(path);
            }
            return result;
        }
    }
}
