using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    public static class SceneHelper
    {
        /// <summary>Find loaded scene by path first, then by name.</summary>
        private static Scene FindScene(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                throw new System.ArgumentException("identifier required");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == identifier || s.name == identifier)
                    return s;
            }
            throw new System.ArgumentException($"Scene not found or not loaded: {identifier}");
        }

        public static string OpenAdditive(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new System.ArgumentException("path required");
            if (!System.IO.File.Exists(path))
                throw new System.ArgumentException($"Scene file not found: {path}");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            return scene.name;
        }

        public static string CloseScene(string identifier)
        {
            if (SceneManager.sceneCount <= 1)
                throw new System.InvalidOperationException("Cannot close the only loaded scene");
            var scene = FindScene(identifier);
            var name = scene.name;
            // If closing the active scene, promote another
            if (SceneManager.GetActiveScene() == scene)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var other = SceneManager.GetSceneAt(i);
                    if (other != scene) { SceneManager.SetActiveScene(other); break; }
                }
            }
            EditorSceneManager.CloseScene(scene, true);
            return $"Closed: {name}";
        }

        public static string SetActiveScene(string identifier)
        {
            var scene = FindScene(identifier);
            if (!scene.isLoaded)
                throw new System.ArgumentException($"Scene '{identifier}' is not loaded");
            SceneManager.SetActiveScene(scene);
            return scene.name;
        }

        public static string ListScenes()
        {
            var sb = new StringBuilder();
            var active = SceneManager.GetActiveScene();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                var prefix = s == active ? "* " : "  ";
                var objCount = s.rootCount;
                var dirty = s.isDirty ? " [dirty]" : "";
                var path = string.IsNullOrEmpty(s.path) ? "(unsaved)" : s.path;
                sb.AppendLine($"{prefix}{s.name}  {path}  {objCount} objs{dirty}");
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Creates a new empty scene, discarding current changes without save dialog.
        /// </summary>
        public static string NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            return scene.name;
        }

        /// <summary>
        /// Saves the current scene. If path is null, saves to current path.
        /// For untitled scenes, path is required.
        /// </summary>
        public static string SaveScene(string path)
        {
            var scene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(path))
            {
                EditorSceneManager.SaveScene(scene, path);
                return path;
            }
            if (string.IsNullOrEmpty(scene.path))
                throw new System.ArgumentException("untitled scene, path required");
            EditorSceneManager.SaveScene(scene);
            return scene.path;
        }

        /// <summary>
        /// Opens a scene by path, discarding dirty state first to prevent save dialog.
        /// </summary>
        public static string OpenScene(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new System.ArgumentException("path required");

            if (!System.IO.File.Exists(path))
                throw new System.ArgumentException($"Scene not found: {path}");

            // NewScene silently discards dirty state — no save dialog
            var current = SceneManager.GetActiveScene();
            if (current.isDirty)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            return scene.name;
        }

        /// <summary>
        /// Discards all changes by reloading from disk or creating new scene.
        /// Never shows a save dialog.
        /// </summary>
        public static string DiscardChanges()
        {
            var scene = SceneManager.GetActiveScene();
            var path = scene.path;

            // NewScene silently discards dirty state — no save dialog
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (!string.IsNullOrEmpty(path))
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                return "reloaded";
            }
            return "new scene";
        }
    }
}
