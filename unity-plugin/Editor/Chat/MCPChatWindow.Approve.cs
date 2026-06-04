// Approve & Execute partial — resumes the same session in Agent mode after Ask-mode plan.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        /// <summary>
        /// One-click plan approval: flips to Agent mode, resumes session, dispatches execute prompt.
        /// Called from the one-shot button injected after a TurnDone in Ask mode.
        /// </summary>
        internal void ApproveAndExecute()
        {
            var sessionId = _backend?.SessionId;
            var prompt = ApproveHelper.BuildPromptOrNull(sessionId);
            if (prompt == null) return;

            _agentMode = true;
            _askBtn?.EnableInClassList("mode-toggle-btn--active",   false);
            _agentBtn?.EnableInClassList("mode-toggle-btn--active", true);

            _backend?.Stop();
            CreateBackendWithSession(sessionId);   // reuse → acceptEdits + --resume

            try { DispatchTurn(UserTurnBuilder.Build(prompt), prompt); }
            catch (System.Exception e)
            {
                _transcript?.AppendToolChip("Approve failed: " + e.Message, ok: false);
            }
        }
    }
}
