// Drain loop, event handlers, and post-reload resume logic — partial of MCPChatWindow.
using System;
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
            string[] chipPaths, kindKeys;
            if (_chipField?.Model != null && _chipField.Model.Count > 0)
            {
                var reload = _chipField.Model.SerializeForReload();
                chipPaths  = reload.Paths;
                kindKeys   = reload.KindKeys;
            }
            else
            {
                chipPaths = System.Array.Empty<string>();
                kindKeys  = System.Array.Empty<string>();
            }

            var isIdle = _activity.Phase == ActivityPhase.Idle;

            // Nothing to save: idle with no chips and no input text.
            var inputText = isIdle
                ? (_chipField?.Text ?? _input?.value ?? "").Trim()
                : _sentTextCache.Get();
            if (isIdle && chipPaths.Length == 0 && string.IsNullOrEmpty(inputText))
                return;

            var state = new PendingTurnState(
                isIdle ? null : _backend?.SessionId,
                inputText,
                chipPaths,
                _agentMode,
                _selectedAgent,
                _activity.Phase.ToString(),
                undoGroupId: isIdle ? -1 : _undoTracker.InflightGroupId,
                savedAtUtc:  DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                backendKind: _selectedKind,
                kindKeys:    kindKeys);
            ReloadGuard.SavePendingState(state);
        }

        // #1 Called by ChatProcess.OnAfterReloadResume after assembly reload completes.
        private void TryResumePendingTurn()
        {
            var pending = ReloadGuard.LoadPendingState();
            if (pending == null) return;
            ReloadGuard.ClearPendingState();

            var p = pending.Value;

            // #26 Staleness guard: discard state saved by a crash/restart older than threshold.
            // SavedAtUtc == 0 means legacy file (no timestamp) — allowed through.
            // Idle saves are exempt: they represent user-composed (not in-flight) state.
            const long StalenessThresholdSec = 60;
            if (p.ActivityPhase != "Idle" && p.SavedAtUtc > 0
                && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - p.SavedAtUtc > StalenessThresholdSec)
                return; // stale crash/restart artifact — file already cleared, silently discard

            _agentMode     = p.AgentMode;
            _selectedAgent = p.AgentName;
            _selectedKind  = p.BackendKind;

            // Restore chips from PendingTurnState (v4/v5 format).
            if (_chipField?.Model != null && p.ChipPaths?.Length > 0)
                _chipField.Model.RestoreFromReload(p.ChipPaths, p.KindKeys);
            _chipField?.RebuildFromModel();

            // Idle save: restore chips + input text only, no turn dispatch.
            if (p.ActivityPhase == "Idle")
            {
                if (!string.IsNullOrEmpty(p.PendingText) && _chipField != null)
                    _chipField.Text = p.PendingText;
                return;
            }

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
                // #12: close pre-reload undo group BEFORE opening the resumed one;
                // ordering is critical — CloseNamedGroup must precede OnTurnStart.
                if (p.UndoGroupId >= 0)
                    UndoGroupHelper.CloseNamedGroup(p.UndoGroupId);
                // F6: resumed turns also need an undo group so OnTurnEnd/OnTurnFailed
                // can commit them to the restore stack (MAJOR 1 fix).
                _undoTracker.OnTurnStart(displayText.Length > 40
                    ? displayText.Substring(0, 40)
                    : displayText);
                // Cache the FULL sent text (with snapshot) so a re-reload can persist it.
                _sentTextCache.Set(sentText);
                // Show only the original user text in the bubble (no state dump).
                var chipList = _chipField?.Model?.Chips is { Count: > 0 } c
                    ? new System.Collections.Generic.List<ChipData>(c) : null;
                _transcript?.AppendUserBubble(displayText, chipList);
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
                    // F6: dead process — treat as failed turn.
                    _undoTracker.OnTurnFailed();
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
            if (_autoScrollEnabled)
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
                    var hasErrors = CompileErrorCapture.HasErrors();
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
                    var hadToolCalls = _turnHasToolCalls;
                    _turnHasToolCalls = false;
                    // F6: close the undo group and append a Restore button.
                    _undoTracker.OnTurnEnd();
                    _transcript?.Append(RestoreButton.Create(_undoTracker));
                    // Ask mode + valid session + tool calls present → inject one-shot approve button.
                    if (!_agentMode && !string.IsNullOrEmpty(_backend?.SessionId) && hadToolCalls)
                    {
                        var approveContainer = new VisualElement();
                        _transcript?.Append(approveContainer);
                        ApproveButtonFactory.MaybeAppend(approveContainer,
                            agentMode: false, sessionId: _backend.SessionId,
                            onApprove: ApproveAndExecute);
                    }
                    break;
                case ChatEventKind.SessionInit:
                    break; // non-terminal: session established, keep animation running
                case ChatEventKind.Error:
                    ReloadGuard.OnTurnFinished(); // #1 unlock even on error
                    if (_activity.Fail()) OnActivityChanged();
                    _transcript.AppendToolChip(ev.Text ?? "Error", ok: false);
                    _turnEditedCode   = false; // provenance gate: symmetric with TurnDone
                    _turnHasToolCalls = false;
                    // F6: partial mutations still restorable on error.
                    _undoTracker.OnTurnFailed();
                    _transcript?.Append(RestoreButton.Create(_undoTracker));
                    break;
            }
        }

        // Called by CompileAutoFix.OnErrorsDetected — fires on Unity main thread (compilationFinished).
        // Only builds the message and dispatches the turn; cap chip is shown at TurnDone.
        private void InjectCompileErrors(string errors)
        {
            if (!_activity.CanSend) return;
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
                _turnHasToolCalls = true;
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
