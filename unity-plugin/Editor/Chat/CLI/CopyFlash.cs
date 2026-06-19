// Static seam: View wires ShowAction in OnEnable/OnDisable.
// Lives in CLI assembly (no Unity UI deps) so tests can reference it freely.
using System;

namespace UnityMCP.Editor.Chat
{
    internal static class CopyFlash
    {
        internal static Action ShowAction;
        internal static void Show() => ShowAction?.Invoke();
    }
}
