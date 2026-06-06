// Chip methods extracted to a partial to keep MCPChatWindow.cs under 200 lines.
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
            {
                if (obj == null) continue;

                if (obj is GameObject go && !AssetDatabase.Contains(go))
                {
                    InsertInlineChip(go, ComponentSerializer.GetPath(go), go.name);
                    continue;
                }

                if (AssetDatabase.Contains(obj) && ChatChipPolicy.IsAllowedAssetType(obj.GetType()))
                {
                    var assetPath = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(assetPath))
                        InsertInlineChip(obj, assetPath, obj.name);
                }
                else if (!(obj is GameObject))
                    Debug.LogWarning($"[MCP Chat] {obj.GetType().Name} not supported as a context chip");
            }
        }
    }
}
