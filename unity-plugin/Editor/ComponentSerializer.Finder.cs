using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    public static partial class ComponentSerializer
    {
        public static GameObject FindObjectOrThrow(string path)
        {
            var go = FindObject(path);
            if (go == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(path));
            return go;
        }

        public static GameObject FindObject(string path, bool strict = false)
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (RefManager.IsRef(path))
            {
                var resolved = RefManager.Resolve(path);
                if (resolved != null) return resolved;
                throw new System.ArgumentException($"Stale ref: {path}. Call get_hierarchy to refresh.");
            }

            if (path.StartsWith("#") && int.TryParse(path.Substring(1), out var iid))
            {
                var byId = FindObjectById(iid);
                if (byId != null) return byId;
                throw new System.ArgumentException($"Instance ID {path} not found");
            }

            // Scene-qualified path: "SceneName:/RootObj/Child"
            var parsed = ScenePathParser.Parse(path);
            if (parsed.SceneName != null)
            {
                var sceneParts = parsed.LocalPath.Split('/');
                var sceneRoot = FindRootInScene(parsed.SceneName, sceneParts[0]);
                if (sceneRoot == null) return null;  // strict scene boundary — no fuzzy cross-scene
                GameObject current = sceneRoot;
                for (int i = 1; i < sceneParts.Length; i++)
                {
                    Transform child = current.transform.Find(sceneParts[i]);
                    if (child == null) return null;  // strict scene boundary — no fuzzy cross-scene
                    current = child.gameObject;
                }
                return current;
            }

            if (path.StartsWith("/")) path = path.Substring(1);
            if (string.IsNullOrEmpty(path)) return null;

            var parts = path.Split('/');
            var root = FindRoot(parts[0]);
            if (root == null)
            {
                // Fallback: try whole path as root name (handles slashes in names)
                root = FindRoot(path);
                if (root != null) return root;
                return strict ? null : TryFuzzyFind(path, parts);
            }

            GameObject cur = root;
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = cur.transform.Find(parts[i]);
                if (child == null) return strict ? null : TryFuzzyFind(path, parts);
                cur = child.gameObject;
            }
            return cur;
        }

        internal static GameObject FindObjectById(int instanceId)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceId);
            return obj as GameObject;
        }

        private static GameObject FindRoot(string name)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot.name == name)
                return stage.prefabContentsRoot;

            GameObject found = null;
            string foundScene = null;
            var ambiguous = new List<string>();

            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == name)
                    {
                        if (found == null)
                        {
                            found = root;
                            foundScene = scene.name;
                        }
                        else
                        {
                            if (ambiguous.Count == 0)
                                ambiguous.Add($"{foundScene}:/{name} (#{found.GetInstanceID()})");
                            ambiguous.Add($"{scene.name}:/{name} (#{root.GetInstanceID()})");
                        }
                    }
                }
            }
            if (ambiguous.Count > 0)
                throw new System.ArgumentException(
                    $"Ambiguous: '{name}' matches {ambiguous.Count} objects. Use instance ID: " + string.Join(" or ", ambiguous));
            return found;
        }

        private static GameObject FindRootInScene(string sceneName, string rootName)
        {
            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                if (scene.name != sceneName) continue;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == rootName) return root;
            }
            return null;
        }

        private static GameObject TryFuzzyFind(string path, string[] parts)
        {
            var lastName = parts[parts.Length - 1];
            var candidates = new List<GameObject>();

            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    FindByName(root, lastName, candidates, 5);
            }

            if (parts.Length > 1)
            {
                var suffix = "/" + string.Join("/", parts);
                candidates.RemoveAll(c => !GetPath(c).EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase));
            }

            if (candidates.Count == 1)
                return candidates[0];
            if (candidates.Count > 1)
                throw new System.ArgumentException(
                    $"Ambiguous: '{path}'. Did you mean: " +
                    string.Join(", ", candidates.ConvertAll(c => GetPath(c))));
            return null;
        }

        private static void FindByName(GameObject root, string name, List<GameObject> results, int max)
        {
            var queue = new Queue<GameObject>();
            queue.Enqueue(root);
            while (queue.Count > 0 && results.Count < max)
            {
                var go = queue.Dequeue();
                if (go.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    results.Add(go);
                for (int i = 0; i < go.transform.childCount; i++)
                    queue.Enqueue(go.transform.GetChild(i).gameObject);
            }
        }

        /// <summary>Strip namespace prefix: "UnityEngine.UI.Button" → "Button"</summary>
        internal static string StripNamespace(string typeName)
        {
            if (typeName == null) return null;
            var dot = typeName.LastIndexOf('.');
            return dot >= 0 ? typeName.Substring(dot + 1) : typeName;
        }

        internal static Component FindComponent(GameObject go, string typeName)
        {
            var shortName = InputNormalizer.NormalizeComponent(StripNamespace(typeName), go);
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                var t = comp.GetType();
                if (t.Name == shortName || t.FullName == typeName)
                    return comp;
                var bt = t.Name.IndexOf('`');
                if (bt > 0 && t.Name.Substring(0, bt).Equals(shortName, System.StringComparison.OrdinalIgnoreCase))
                    return comp;
            }
            return null;
        }

        public static string GetPath(GameObject go)
        {
            var path = go.name;
            var t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return SceneContext.Current.QualifyPath(go, path);
        }
    }

    /// <summary>Parses "SceneName:/local/path" → (sceneName, localPath). Shared between finders.</summary>
    internal readonly struct ScenePathParser
    {
        public readonly string SceneName;   // null if not scene-qualified
        public readonly string LocalPath;   // path without scene prefix, leading '/' stripped

        private ScenePathParser(string scene, string local) { SceneName = scene; LocalPath = local; }

        internal static ScenePathParser Parse(string path)
        {
            if (string.IsNullOrEmpty(path)) return new ScenePathParser(null, path);
            int sep = path.IndexOf(":/", System.StringComparison.Ordinal);
            if (sep <= 0) return new ScenePathParser(null, path);
            var scene = path.Substring(0, sep);
            var local = path.Substring(sep + 2);  // skip ":/"
            if (local.StartsWith("/")) local = local.Substring(1);
            return new ScenePathParser(scene, local);
        }
    }
}
