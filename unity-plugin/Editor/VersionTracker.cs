using UnityEditor;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    public static class VersionTracker
    {
        private static int _version = 0;

        static VersionTracker()
        {
            EditorApplication.hierarchyChanged += IncrementVersion;
            Undo.undoRedoPerformed += IncrementVersion;
        }

        public static int Version => System.Threading.Volatile.Read(ref _version);

        private static void IncrementVersion()
        {
            System.Threading.Interlocked.Increment(ref _version);
        }
    }
}
