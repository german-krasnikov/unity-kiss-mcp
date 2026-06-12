namespace UnityMCP.Editor.Tests
{
    internal static class TestPaths
    {
        internal const string TempFolder = "Assets/TestsTemp";

        internal static void EnsureFolder()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(TempFolder))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "TestsTemp");
        }
    }
}
