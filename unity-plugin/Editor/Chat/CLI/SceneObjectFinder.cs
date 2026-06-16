// Finds GameObjects by hierarchy path across all loaded scenes.
// Extracted from ChatRefAction so BuiltInChipProviders (CLI) can use it without a View reference.
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class SceneObjectFinder
    {
        internal static GameObject FindGameObject(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // Parse optional "SceneName:/local/path" prefix
            var parsed = UnityMCP.Editor.ScenePathParser.Parse(path);

            var parts = parsed.LocalPath.TrimStart('/').Split('/');
            if (parts.Length == 0 || string.IsNullOrEmpty(parts[0])) return null;

            GameObject current = null;
            foreach (var root in GetAllRoots(parsed.SceneName))
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

        private static IEnumerable<GameObject> GetAllRoots(string sceneNameFilter = null)
        {
            int n = EditorSceneManager.sceneCount;
            for (int i = 0; i < n; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                if (sceneNameFilter != null && scene.name != sceneNameFilter) continue;
                foreach (var go in scene.GetRootGameObjects())
                    yield return go;
            }
        }
    }
}
