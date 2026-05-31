using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    public static class SceneHelper
    {
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
