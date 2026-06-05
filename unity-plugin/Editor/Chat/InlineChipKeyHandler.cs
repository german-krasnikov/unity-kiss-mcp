// InlineChipKeyHandler: wires TextField.ValueChanged and atomic-caret KeyDown.
// H11: AttachAtomicCaret first line is the Enter guard — Enter must ALWAYS send, never swallowed.
// H10: all NBSP/atomic-caret paths gated behind UitkCharRect.IsAvailable.
// MF3: ValueChangedCallback runs FindCorruptedChips and marks corrupted chips for removal.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class InlineChipKeyHandler
    {
        /// <summary>
        /// Wire the ValueChanged callback so that U+FFFC deletions update
        /// <paramref name="tracker"/> and rebuild <paramref name="overlay"/>.
        /// MF3: also validates NBSP counts and marks corrupted chips for removal.
        /// </summary>
        internal static void Attach(
            TextField          field,
            InlineChipTracker  tracker,
            InlineChipOverlay  overlay)
        {
            field.RegisterValueChangedCallback<string>(evt =>
            {
                var removed = tracker.SyncToText(evt.previousValue, evt.newValue);
                if (removed.Count > 0)
                    overlay.Refresh();

                // MF3: detect corrupted NBSP reservations (H10 gated)
                if (UitkCharRect.IsAvailable)
                {
                    var corrupted = NbspReservation.FindCorruptedChips(
                        evt.newValue, tracker.ExpectedNbspCounts);
                    if (corrupted.Count > 0)
                        RemoveCorruptedChips(corrupted, tracker, overlay);
                }
            });
        }

        /// <summary>
        /// Register a KeyDown TrickleDown handler for atomic-caret navigation.
        /// H10: no-op when UitkCharRect.IsAvailable is false.
        /// H11 STRUCTURAL: Enter guard is the literal first executable line.
        /// </summary>
        internal static void AttachAtomicCaret(
            TextField          field,
            InlineChipTracker  tracker)
        {
            // H10: only wire atomic caret when positioning API is available
            if (!UitkCharRect.IsAvailable) return;

            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                // H11: Enter must always send — never swallow it here
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) return;

                var text  = field.value;
                var spans = TokenSpan.ComputeTokenSpans(text);
                if (spans.Count == 0) return;

                var sel   = field.textSelection;
                int caret = sel?.cursorIndex ?? -1;
                if (caret < 0) return;

                if (evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.RightArrow)
                {
                    HandleArrowKey(evt, sel, spans, caret);
                }
                else if (evt.keyCode == KeyCode.Backspace || evt.keyCode == KeyCode.Delete)
                {
                    HandleDeleteKey(evt, sel, spans, caret);
                }
            }, TrickleDown.TrickleDown);
        }

        // ── Arrow: skip over token spans atomically ───────────────────────────

        private static void HandleArrowKey(
            KeyDownEvent       evt,
            ITextSelection     sel,
            List<TokenSpan>    spans,
            int                caret)
        {
            bool movingRight = evt.keyCode == KeyCode.RightArrow;

            int spanIdx = TokenSpan.SpanIndexAtCaret(spans, caret);
            if (spanIdx < 0) return; // not inside any span — default behavior

            evt.StopPropagation();
            evt.PreventDefault();

            if (movingRight)
                sel.cursorIndex = spans[spanIdx].End + 1;
            else
                sel.cursorIndex = spans[spanIdx].Start;
        }

        // ── Backspace/Delete: select whole span first, then let default delete it ─

        private static void HandleDeleteKey(
            KeyDownEvent       evt,
            ITextSelection     sel,
            List<TokenSpan>    spans,
            int                caret)
        {
            int spanIdx = TokenSpan.SpanIndexAtCaret(spans, caret);
            if (spanIdx < 0) return;

            // Select the entire span so the next delete removes it atomically
            evt.StopPropagation();
            evt.PreventDefault();
            sel.SelectRange(spans[spanIdx].Start, spans[spanIdx].End + 1);
        }

        // ── MF3: remove chips whose NBSP reservation is corrupted ─────────────

        private static void RemoveCorruptedChips(
            List<int>          corruptedIndices,
            InlineChipTracker  tracker,
            InlineChipOverlay  overlay)
        {
            // Remove in reverse to keep index validity
            for (int i = corruptedIndices.Count - 1; i >= 0; i--)
                tracker.RemoveAt(corruptedIndices[i]);

            overlay.Refresh();
        }
    }
}
