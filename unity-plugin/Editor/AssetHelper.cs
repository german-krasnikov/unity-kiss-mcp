using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class AssetHelper
    {
        /// <summary>
        /// Ensures all intermediate folders in the given asset path exist.
        /// Works for paths like "Assets/Foo/Bar/file.asset".
        /// </summary>
        public static void EnsureDirectory(string assetPath)
        {
            var dir = System.IO.Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(dir) || dir == "Assets") return;

            var parts = dir.Replace("\\", "/").Split('/');
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
