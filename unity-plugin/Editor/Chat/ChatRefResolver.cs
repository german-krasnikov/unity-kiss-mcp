// Resolves identifier names to scene object paths or MonoScript asset paths.
// Caches results; call Refresh() once before a batch of linkify operations.
// NOT pure — depends on UnityEditor/UnityEngine APIs.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ChatRefResolver
    {
        // name -> first-found hierarchy path e.g. "/Root/Player"
        private readonly Dictionary<string, string> _objects = new Dictionary<string, string>();
        // name (no .cs) -> asset path e.g. "Assets/Scripts/Player.cs"
        private readonly Dictionary<string, string> _scripts = new Dictionary<string, string>();

        /// <summary>Exposes scene object name→path map for BareNameNormalizer scene-wide pass.</summary>
        internal IReadOnlyDictionary<string, string> Objects => _objects;

        /// <summary>Rebuilds both caches. Call once before rendering a batch of blocks.</summary>
        internal void Refresh()
        {
            RefreshObjects();
            RefreshScripts();
        }

        /// <summary>Returns hierarchy path for a scene object by name, or null if not found.</summary>
        internal string ResolveObject(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            _objects.TryGetValue(name, out var path);
            return path;
        }

        /// <summary>Returns asset path for a MonoScript by name (with or without .cs), or null.</summary>
        internal string ResolveScript(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var key = name.EndsWith(".cs") ? name.Substring(0, name.Length - 3) : name;
            _scripts.TryGetValue(key, out var path);
            return path;
        }

        private void RefreshObjects()
        {
            _objects.Clear();
            int sceneCount = EditorSceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    WalkTransform(root.transform, "/" + root.name);
            }
        }

        private void WalkTransform(Transform t, string path)
        {
            // First match wins — deterministic for duplicate names.
            if (!_objects.ContainsKey(t.name))
                _objects[t.name] = path;

            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                WalkTransform(child, path + "/" + child.name);
            }
        }

        private void RefreshScripts()
        {
            _scripts.Clear();
            // Single FindAssets call — O(1) lookup after this.
            var guids = AssetDatabase.FindAssets("t:MonoScript");
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var ms        = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                if (ms == null) continue;
                var scriptName = ms.name; // class/file name without .cs
                if (!_scripts.ContainsKey(scriptName))
                    _scripts[scriptName] = assetPath;
            }
        }
    }
}
