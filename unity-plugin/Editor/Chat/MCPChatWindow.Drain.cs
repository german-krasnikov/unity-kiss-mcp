// Drain loop, event handlers, and post-reload resume logic — partial of MCPChatWindow.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private double _lastRefresh;

        // #1 Save turn state before domain reload so it can be resumed after.
        private void SaveStateBeforeReload()
        {
            if (_activity.Phase == ActivityPhase.Idle) return;
            // FIX A: read from _sentTextCache (set by DispatchTurn before input was cleared),
            // NOT from _input.value which is already "" at this point.
            var text  = _sentTextCache.Get();
            var chips = CollectChipPaths();
            var state = new PendingTurnState(
                _backend?.SessionId,
                text,
                chips.ToArray(),
                _agentMode,
                _selectedAgent,
                _activity.Phase.ToString());
            ReloadGuard.SavePendingState(state);
        }

        // #1 Called by ChatProcess.OnAfterReloadResume after assembly reload completes.
        private void TryResumePendingTurn()
        {
            var pending = ReloadGuard.LoadPendingState();
            if (pending == null) return;
            ReloadGuard.ClearPendingState();

            var p = pending.Value;
            _agentMode     = p.AgentMode;
            _selectedAgent = p.AgentName;
            CreateBackendWithSession(p.SessionId);

            var displayText = p.PendingText;
            if (!string.IsNullOrEmpty(displayText))
            {
                // Build the full text sent to Claude (snapshot + original), but display only original.
                var snap     = EditorStateSnapshot.Capture();
                var sentText = string.IsNullOrEmpty(snap)
                    ? displayText
                    : snap + "\n" + displayText;
                // FIX B: lock reload for the resumed turn, symmetric with DispatchTurn.
                ReloadGuard.OnTurnStarted();
                // Cache the FULL sent text (with snapshot) so a re-reload can persist it.
                _sentTextCache.Set(sentText);
                // Show only the original user text in the bubble (no state dump).
                _transcript?.AppendUserBubble(displayText);
                _backend.SendTurn(UserTurnBuilder.Build(sentText));
                if (_activity.Send()) OnActivityChanged();
            }
        }

        private void DrainAndRender()
        {
            _evBuf.Clear(); _toolBuf.Clear();
            _backend?.DrainEvents(_evBuf, _toolBuf);

            if (_evBuf.Count == 0 && _toolBuf.Count == 0)
            {
                // Dead-process guard: buffer is empty, so no result line is coming.
                // Only fires if the process truly died (crash/kill) without emitting a result.
                // Phase-first: cheap field read before the nullable backend deref.
                if (_activity.Phase != ActivityPhase.Idle && _backend != null && !_backend.IsRunning)
                {
                    // #2 fix: release the reload lock; without this the lock was held until the 120s watchdog.
                    ReloadGuard.OnTurnFinished();
                    if (_activity.Fail()) OnActivityChanged();
                }
                return;
            }
            // Refresh the ref cache at most ~1/sec while streaming so objects Claude just
            // created become clickable without reopening the window.
            if (EditorApplication.timeSinceStartup - _lastRefresh > 1.0)
            {
                _resolver?.Refresh();
                _lastRefresh = EditorApplication.timeSinceStartup;
            }
            foreach (var ev in _evBuf) HandleEvent(ev);
            foreach (var rec in _toolBuf) HandleToolRecord(rec);
            _transcript.FlushStreaming();
            _scroll.scrollOffset = new Vector2(0, float.MaxValue);
        }

        private void HandleEvent(ChatEvent ev)
        {
            switch (ev.Kind)
            {
                case ChatEventKind.TextDelta:
                    if (_activity.FirstToken()) OnActivityChanged();
                    _transcript.AppendOrExtendAssistant(ev.Text);
                    break;
                case ChatEventKind.TurnDone:
                    ReloadGuard.OnTurnFinished(); // #1 unlock after turn complete
                    if (_activity.Done()) OnActivityChanged();
                    _transcript.FinalizeAssistant();
                    if (ev.InputTokens > 0 || ev.OutputTokens > 0)
                    {
                        _inputTokens  += ev.InputTokens;
                        _outputTokens += ev.OutputTokens;
                        _tokenReadout.text =
                            $"↑ {TokenFormat.Abbr(_inputTokens)}  ↓ {TokenFormat.Abbr(_outputTokens)}";
                    }
                    var hasErrors = CompileErrorCapture.GetErrors() != "No compilation errors";
                    if (hasErrors)
                    {
                        // Cap reached: show chip once here, after the 3rd dispatched turn.
                        if (_autoFix.RetriesLeft == 0 && !_autoFix.IsArmed)
                            _transcript?.AppendToolChip("Auto-fix capped at 3 attempts.", ok: false);
                        // Arm only when this turn edited code (CRITICAL #2 gate).
                        else if (_turnEditedCode)
                            _autoFix.Arm();
                    }
                    _turnEditedCode = false;
                    break;
                case ChatEventKind.SessionInit:
                    break; // non-terminal: session established, keep animation running
                case ChatEventKind.Error:
                    ReloadGuard.OnTurnFinished(); // #1 unlock even on error
                    if (_activity.Fail()) OnActivityChanged();
                    _transcript.AppendToolChip(ev.Text ?? "Error", ok: false);
                    _turnEditedCode = false; // provenance gate: symmetric with TurnDone
                    break;
            }
        }

        // Called by CompileAutoFix.OnErrorsDetected — fires on Unity main thread (compilationFinished).
        // Only builds the message and dispatches the turn; cap chip is shown at TurnDone.
        private void InjectCompileErrors(string errors)
        {
            var msg = $"Compile errors after your edit:\n{errors}\nFix them.";
            DispatchTurn(UserTurnBuilder.Build(msg), msg);
        }

        // Tool names that edit source files — used to gate auto-fix arming (CRITICAL #2).
        internal static bool IsCodeEditingTool(ToolCallRecord rec)
        {
            var n = rec.Name;
            if (n == "Edit" || n == "Write" || n == "MultiEdit") return true;
            // MCP mutating tools that write .cs files are identified by a .cs path in args.
            if (rec.ArgsJson != null && rec.ArgsJson.Contains(".cs\"")) return true;
            return false;
        }

        private void HandleToolRecord(ToolCallRecord rec)
        {
            if (rec.ArgsJson == null && !rec.HasResult)
            {
                // null ArgsJson = chip-creation record (ToolStart moment)
                if (_activity.FirstToken()) OnActivityChanged();
                _transcript.AppendToolChip(rec.Name, ok: true, toolId: rec.Id);
            }
            else
            {
                // ArgsJson non-null + no result = args-complete record: ping + check provenance.
                // HasResult = result record: detail update only (no double-ping).
                if (rec.ArgsJson != null && !rec.HasResult)
                {
                    ToolPing.TryPing(rec);
                    if (IsCodeEditingTool(rec)) _turnEditedCode = true;
                }
                _transcript.UpdateToolDetail(rec.Id, rec);
            }
        }
    }
}
