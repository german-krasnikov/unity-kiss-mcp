// Partial MCPChatWindow — chip-overlay wiring, dirty-tick, merged context menu.
// H9: extracted from MCPChatWindow.cs to keep it under 200 lines.
// MF1: overlay attaches to _input (TextField), not _inputArea parent.
// H13: NBSP advance measured after first GeometryChangedEvent.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        // Dirty flag: set when geometry/scroll/focus changes; cleared by TickDirty.
        private bool _chipsDirty;
        // Default NBSP count per chip until measured (H13).
        private int  _defaultNbspN = 4;
        private bool _nbspMeasured;

        /// <summary>
        /// Wire the inline-chip overlay to <c>_input</c>. Called from BuildInputArea.
        /// MF1: overlay is a child of _input, not _inputArea.
        /// </summary>
        internal void WireChipInput()
        {
            // MF1: attach overlay as child of _input (TextField), not parent area.
            _chipOverlay.AttachTo(_input);

            // Combined context menu: generic "Add Selection" + conditional chip items.
            _input.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction(
                    "Add Selection to Context",
                    _ => InsertInlineChip(Selection.activeGameObject),
                    _ => Selection.activeGameObject != null
                        ? DropdownMenuAction.Status.Normal
                        : DropdownMenuAction.Status.Disabled);

                // Chip-specific items only when caret is inside a chip span.
                if (!UitkCharRect.IsAvailable) return;
                var text   = _input.value ?? "";
                var spans  = TokenSpan.ComputeTokenSpans(text);
                var sel    = _input.textSelection;
                int caret  = sel?.cursorIndex ?? -1;
                int chipIdx = TokenSpan.SpanIndexAtCaret(spans, caret);
                if (chipIdx < 0 || chipIdx >= _chipTracker.Count) return;

                var chip = _chipTracker[chipIdx];

                evt.menu.AppendAction("Show LLM payload", _ =>
                {
                    // H14b symmetry: same pipeline as send path — ResolveAllTyped is the
                    // exact same call OnSend takes (via AppendChipContext).
                    var cfg     = BackendConfigStore.Load().Chips;
                    var payload = ChipContextResolver.ResolveAllTyped(
                        new List<ChipData> { chip }, cfg);
                    Debug.Log($"[MCP Chat] LLM payload for chip [{chip.KindKey}:{chip.Path}]:\n{payload}");
                });

                evt.menu.AppendAction("Copy path", _ =>
                    EditorGUIUtility.systemCopyBuffer = chip.Path);

                int capturedIdx = chipIdx;
                evt.menu.AppendAction("Remove", _ => RemoveInlineChipAt(capturedIdx));
            }));

            // Dirty on geometry / scroll / focus changes → TickDirty rebuilds overlay.
            bool _scrollWired = false;
            _input.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                _chipsDirty = true;
                // H13: measure NBSP advance after first GeometryChangedEvent.
                if (!_nbspMeasured && UitkCharRect.IsAvailable)
                    ScheduleNbspMeasure(); // Already-inserted chips keep their per-chip recorded N (H12 _expectedNbsp) — measure change cannot cause false corruption.
                // FIX 7: wire scroll once after layout — inner ScrollView may not exist before first geometry.
                // Null-safe: Q<ScrollView>() returns null if the inner scroller is absent on this Unity version.
                if (!_scrollWired)
                {
                    _scrollWired = true;
                    // FIX A: cannot use ?. on the left side of +=  (CS0079); explicit null-check required.
                    var sv = _input.Q<ScrollView>();
                    if (sv != null)
                        sv.verticalScroller.valueChanged += _ => _chipsDirty = true;
                }
            });

            _input.RegisterCallback<FocusEvent>(_ => _chipsDirty = true);

            // Schedule dirty flush at ~33 ms cadence (aligned with DrainAndRender).
            _input.schedule.Execute(TickDirty).Every(33);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void TickDirty()
        {
            if (!_chipsDirty) return;
            _chipsDirty = false;
            _chipOverlay?.Refresh();
        }

        /// <summary>
        /// H13: measure actual NBSP pixel advance in one deferred frame, then recompute
        /// chip N values. Uses schedule.ExecuteLater(1) — fires after layout.
        /// </summary>
        private void ScheduleNbspMeasure()
        {
            _input.schedule.Execute(() =>
            {
                if (_nbspMeasured) return;
                float advance = UitkCharRect.MeasureNbspAdvance(_input);
                if (advance <= 0f) return; // layout not ready yet; retry next GeometryChanged
                _nbspMeasured  = true;
                _defaultNbspN  = NbspReservation.ComputeN(32f, advance); // 32px default pill width
                // Chips already inserted keep their N=4 default for this session (single-frame correction
                // is acceptable per brief — H13 known limitation).
            }).ExecuteLater(1);
        }
    }
}
