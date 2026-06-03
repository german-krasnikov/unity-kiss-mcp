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
            foreach (var obj in DragAndDrop.objectReferences)
                if (obj is GameObject go) AddObjChip(go);
        }

        private void AddObjChip(GameObject go)
        {
            var chip = new VisualElement(); chip.AddToClassList("obj-chip");
            chip.userData = ComponentSerializer.GetPath(go);

            var lbl = new Label(go.name); lbl.AddToClassList("obj-chip-label");
            var cap = go;
            lbl.RegisterCallback<ClickEvent>(_ => { EditorGUIUtility.PingObject(cap); Selection.activeObject = cap; });

            var removeBtn = new Button(() => _objChipStrip.Remove(chip)) { text = "✕" };
            removeBtn.AddToClassList("obj-chip-remove");

            chip.Add(lbl);
            chip.Add(removeBtn);
            _objChipStrip.Add(chip);
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
