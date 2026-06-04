using UnityEditor;

namespace UnityMCP.Editor
{
    public static class UndoGroupHelper
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

        // ── Per-turn / per-batch named group primitive (F6 / F27) ──────────────

        public static int OpenNamedGroup(string name)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(name);
            return Undo.GetCurrentGroup();
        }

        public static void CloseNamedGroup(int groupId)
        {
            Undo.CollapseUndoOperations(groupId);
        }

        public static void RevertToBeforeGroup(int groupId)
        {
            Undo.RevertAllDownTo(groupId);
        }

        public static bool CanRevert(int groupId) => groupId >= 0;
    }
}
