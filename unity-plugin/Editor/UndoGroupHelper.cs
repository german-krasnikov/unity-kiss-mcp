using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class UndoGroupHelper
    {
        private static int _currentGroup = -1;

        public static void BeginGroup(string name)
        {
            if (_currentGroup >= 0)
                Undo.CollapseUndoOperations(_currentGroup);

            Undo.SetCurrentGroupName(name);
            _currentGroup = Undo.GetCurrentGroup();
        }

        public static void EndGroup()
        {
            if (_currentGroup >= 0)
            {
                Undo.CollapseUndoOperations(_currentGroup);
                _currentGroup = -1;
            }
        }

        public static void SetCommandFallback(string command)
        {
            Undo.SetCurrentGroupName($"MCP: {command}");
        }
    }
}
