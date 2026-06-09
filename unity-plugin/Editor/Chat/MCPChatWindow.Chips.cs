// Chip methods extracted to a partial to keep MCPChatWindow.cs under 200 lines.
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        internal delegate void ChipInserter(Object obj, string path, string name);

        private static void OnDragUpdated(DragUpdatedEvent e)
        {
            if (DragAndDrop.objectReferences.Length > 0 || DragAndDrop.paths.Length > 0)
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
        }

        private void OnDragPerform(DragPerformEvent e)
        {
            DragAndDrop.AcceptDrag();
            var selected = Selection.activeGameObject;
            foreach (var obj in DragAndDrop.objectReferences)
                ProcessDraggedObject(obj, selected, InsertInlineChip);
            // F29: handle external paths (Finder drag) only when no Unity objects present
            if (DragAndDrop.objectReferences.Length == 0 && DragAndDrop.paths.Length > 0)
                foreach (var path in DragAndDrop.paths)
                    ProcessExternalPath(path, InsertInlineChip);
        }

        internal delegate bool HasComponentFn(GameObject go, System.Type type);

        // F26: extracted for testability — injectable selectedGO + ChipInserter delegate
        // hasComponent is injectable for tests where FromMonoBehaviour can't bind test-assembly types
        internal static void ProcessDraggedObject(Object obj, GameObject selectedGO, ChipInserter insert,
            HasComponentFn hasComponent = null)
        {
            if (obj == null) return;
            hasComponent ??= (go, t) => go.GetComponent(t) != null;

            if (obj is GameObject go && !AssetDatabase.Contains(go))
            {
                insert(go, ComponentSerializer.GetPath(go), go.name);
                return;
            }

            // F29: folder from Project window
            if (obj is DefaultAsset)
            {
                var folderPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath))
                    insert(obj, folderPath, obj.name);
                // non-folder DefaultAsset — reject silently
                return;
            }

            if (obj is MonoScript ms)
            {
                var assetPath = AssetDatabase.GetAssetPath(ms);
                if (string.IsNullOrEmpty(assetPath)) return;
                var cls = ms.GetClass();
                if (selectedGO != null && !AssetDatabase.Contains(selectedGO)
                    && cls != null && typeof(UnityEngine.Component).IsAssignableFrom(cls)
                    && hasComponent(selectedGO, cls))
                {
                    insert(selectedGO, ComponentSerializer.GetPath(selectedGO), selectedGO.name);
                }
                insert(ms, assetPath, ms.name);
                return;
            }

            if (AssetDatabase.Contains(obj) && ChatChipPolicy.IsAllowedAssetType(obj.GetType()))
            {
                var assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath))
                    insert(obj, assetPath, obj.name);
            }
            else if (!(obj is GameObject))
                Debug.LogWarning($"[MCP Chat] {obj.GetType().Name} not supported as a context chip");
        }

        // F29: external path handler (Finder drag) — internal static for testability
        internal static void ProcessExternalPath(string path, ChipInserter insert)
        {
            if (string.IsNullOrEmpty(path)) return;
            var name = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(name)) name = path;
            insert(null, path, name);
        }
    }
}
