namespace UnityMCP.Editor.Tests
{
    public static class TestPaths
    {
        public const string Root = "Assets/TestsTemp";

        public static string ForFixture(string className) => $"{Root}/{className}";

        // Segment-walk: creates nested folders without auto-suffix bug
        public static string EnsureFolder(string assetPath)
        {
            var parts = assetPath.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!UnityEditor.AssetDatabase.IsValidFolder(next))
                    UnityEditor.AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            return assetPath;
        }

        // Backwards-compat alias (no-arg) used by MultiSceneTestBase
        public static void EnsureRoot() => EnsureFolder(Root);

        // Backwards-compat no-arg overload for existing callers
        public static void EnsureFolder() => EnsureFolder(Root);

        public static void DeleteRoot()
        {
            if (UnityEditor.AssetDatabase.IsValidFolder(Root))
                UnityEditor.AssetDatabase.DeleteAsset(Root);
        }

        // Backwards-compat alias for MultiSceneTestBase
        public const string TempFolder = Root;
    }
}
