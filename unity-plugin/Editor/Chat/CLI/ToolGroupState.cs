// Pure grouping state machine for consecutive tool calls — ZERO UnityEngine deps.
// Decides SetPending / Promote / Append; tracks count + error. Tested by ToolGroupStateTests.
namespace UnityMCP.Editor.Chat
{
    public enum ToolGroupAction { SetPending, Promote, Append }

    public sealed class ToolGroupState
    {
        public int  Count;          // tools currently in the open group (0 while only pending)
        public bool AnyError;       // any chip in the open group failed
        public bool HasPending;     // a lone bare chip awaits possible promotion
        public bool HasGroup;       // a Foldout group is open
        private bool _pendingError;

        /// <summary>Advance on a ToolStart/Error. pendingAlive = is the bare pending chip still
        /// in the hierarchy (false if the 200-msg cap evicted it before promotion).</summary>
        public ToolGroupAction OnTool(bool isError, bool pendingAlive)
        {
            if (HasGroup) { Count++; AnyError |= isError; return ToolGroupAction.Append; }
            if (HasPending)
            {
                HasPending = false; HasGroup = true;
                Count    = pendingAlive ? 2 : 1;
                AnyError = (pendingAlive && _pendingError) || isError;
                return ToolGroupAction.Promote;
            }
            HasPending = true; _pendingError = isError;
            return ToolGroupAction.SetPending;
        }

        public void Reset()
        {
            Count = 0; AnyError = false; HasPending = false; HasGroup = false; _pendingError = false;
        }
    }
}
