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
                    var path = ComponentSerializer.GetPath(go);
                    var kind = ChipKindDetector.Detect(go, null);
                    if (dropOnField)
                        InsertInlineChip(go, path, go.name);
                    else
                        AddChip(obj, path, go.name, kind, go.GetInstanceID());
                    continue;
                }

                // Asset — check type allowlist first.
                if (AssetDatabase.Contains(obj) && ChatChipPolicy.IsAllowedAssetType(obj.GetType()))
                {
                    var assetPath = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        var kind = ChipKindDetector.Detect(obj, assetPath);
                        if (dropOnField)
                            InsertInlineChip(obj, assetPath, obj.name);
                        else
                            AddChip(obj, assetPath, obj.name, kind, 0);
                    }
                }
                else if (!(obj is GameObject)) // not a scene GO, not an allowlisted asset
                    Debug.LogWarning($"[MCP Chat] {obj.GetType().Name} not supported as a context chip");
            }
        }

        // Kept for call-sites that pass a scene GameObject directly.
        private void AddObjChip(GameObject go) =>
            AddChip(go, ComponentSerializer.GetPath(go), go.name, ChipKindDetector.Detect(go, null), go.GetInstanceID());

        private void AddChip(Object cap, string payload, string displayName, ChipKind kind = ChipKind.Asset, int instanceID = 0)
        {
            var chip = new VisualElement(); chip.AddToClassList("obj-chip");
            // F10: store typed ref so CollectChipData can emit kind-aware context.
            chip.userData = new ChipData(kind, payload, displayName, instanceID);

            var kindLbl = new Label(ChipKindDetector.ShortPrefix(kind) + ":");
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

        // F10: returns full ChipData (kind+path+instanceID) for all strip chips.
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

        // Backward-compat shim for SaveStateBeforeReload (PendingTurnState stores string[]).
        private List<string> CollectChipPaths()
        {
            var result = new List<string>();
            foreach (var cd in CollectChipData()) result.Add(cd.Path);
            return result;
        }
    }
}
