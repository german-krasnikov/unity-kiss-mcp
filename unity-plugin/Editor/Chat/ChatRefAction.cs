// Handles click-navigation and "Add to context" for <link> tags in chat labels.
// H2: new linkId format "chip:KEY:REF". Legacy "obj:"/"script:"/"asset:" kept for cached labels.
// FindGameObject made internal so HierarchyChipProvider.Navigate can reuse it.
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace UnityMCP.Editor.Chat
{
    internal static class ChatRefAction
    {
        /// <summary>
        /// Installs link-click navigation and "Add to context" context menu on a Label.
        /// addToContext receives the payload string (hierarchy path or asset path).
        /// </summary>
        internal static void Install(Label label, Action<string> addToContext)
        {
            if (label == null) return;

            string lastHoveredLink = null;

            label.RegisterCallback<PointerOverLinkTagEvent>(evt =>
            {
                lastHoveredLink = evt.linkID;
                label.tooltip = "Alt+Click to add to context";
            });

            label.RegisterCallback<PointerOutLinkTagEvent>(_ =>
            {
                lastHoveredLink = null;
                label.tooltip = null;
            });

            label.RegisterCallback<PointerUpLinkTagEvent>(evt =>
            {
                var linkId = evt.linkID;
                if (string.IsNullOrEmpty(linkId)) return;

                if (evt.altKey)
                {
                    AddToContext(linkId, addToContext);
                    return;
                }
                Navigate(linkId);
            });

            label.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                var linkId = lastHoveredLink;
                if (string.IsNullOrEmpty(linkId)) return;
                evt.menu.AppendAction("Navigate", _ => Navigate(linkId));
                evt.menu.AppendAction("Add to context", _ => AddToContext(linkId, addToContext));
            }));
        }

        private static void Navigate(string linkId)
        {
            // H2: new format "chip:KEY:REF"
            if (linkId.StartsWith("chip:"))
            {
                int secondColon = linkId.IndexOf(':', 5);
                if (secondColon < 0) return;
                var key       = linkId.Substring(5, secondColon - 5);
                var reference = linkId.Substring(secondColon + 1);
                var provider  = ChipKindRegistry.ForKey(key);
                if (provider != null) provider.Navigate(reference);
                else DefaultNavigate(reference);
                return;
            }

            // Legacy fallbacks for cached labels
            if (linkId.StartsWith("obj:"))
            {
                var path = linkId.Substring(4);
                var go   = FindGameObject(path);
                if (go == null) { Debug.LogWarning("[MCP Chat] Reference stale: " + path); return; }
                EditorGUIUtility.PingObject(go);
                Selection.activeObject = go;
            }
            else if (linkId.StartsWith("script:"))
            {
                var assetPath = linkId.Substring(7);
                var ms        = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                if (ms == null) { Debug.LogWarning("[MCP Chat] Script not found: " + assetPath); return; }
                AssetDatabase.OpenAsset(ms);
            }
            else if (linkId.StartsWith("asset:"))
            {
                DefaultNavigate(linkId.Substring(6));
            }
        }

        private static void DefaultNavigate(string assetPath)
        {
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (obj == null) { Debug.LogWarning("[MCP Chat] Asset not found: " + assetPath); return; }
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }

        private static void AddToContext(string linkId, Action<string> addToContext)
        {
            if (addToContext == null) return;
            string payload;
            if (linkId.StartsWith("chip:"))
            {
                int secondColon = linkId.IndexOf(':', 5);
                payload = secondColon >= 0 ? linkId.Substring(secondColon + 1) : linkId;
            }
            else if (linkId.StartsWith("obj:"))         payload = linkId.Substring(4);
            else if (linkId.StartsWith("script:")) payload = linkId.Substring(7);
            else if (linkId.StartsWith("asset:"))  payload = linkId.Substring(6);
            else                                   payload = linkId;
            addToContext(payload);
        }

        // Made internal so HierarchyChipProvider.Navigate can reuse it (CRITIC NOTE).
        // Finds a GameObject by hierarchy path "/Root/Child/..." in loaded scenes.
        internal static GameObject FindGameObject(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var parts = path.TrimStart('/').Split('/');
            if (parts.Length == 0) return null;

            var roots = GetAllRoots();
            GameObject current = null;
            foreach (var root in roots)
            {
                if (root.name == parts[0]) { current = root; break; }
            }
            if (current == null) return null;

            for (int i = 1; i < parts.Length; i++)
            {
                var child = current.transform.Find(parts[i]);
                if (child == null) return null;
                current = child.gameObject;
            }
            return current;
        }

        private static GameObject[] GetAllRoots()
        {
            var list = new System.Collections.Generic.List<GameObject>();
            int n = UnityEditor.SceneManagement.EditorSceneManager.sceneCount;
            for (int i = 0; i < n; i++)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                if (scene.isLoaded) list.AddRange(scene.GetRootGameObjects());
            }
            return list.ToArray();
        }
    }
}
