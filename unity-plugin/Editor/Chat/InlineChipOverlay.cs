// InlineChipOverlay: renders pill VisualElements over the TextField.
// MF1: container attaches to _input (TextField), not the parent area.
// MF5: out-of-view pills hidden during RepositionExisting.
// H10: when UitkCharRect.IsAvailable is false, row-layout only (current behavior).
// Flicker fix: Refresh diffs — RepositionExisting if count unchanged, RebuildAll on mismatch.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class InlineChipOverlay
    {
        private readonly TextField         _field;
        private readonly InlineChipTracker _tracker;
        private readonly VisualElement     _container;
        private readonly List<VisualElement> _pills = new List<VisualElement>();

        private Action<int> _onRemove;

        internal InlineChipOverlay(TextField field, InlineChipTracker tracker)
        {
            _field   = field;
            _tracker = tracker;

            _container = new VisualElement();
            _container.AddToClassList("inline-chip-overlay");
            _container.style.position      = Position.Absolute;
            _container.style.flexDirection = FlexDirection.Row;
            _container.style.flexWrap      = Wrap.Wrap;
            _container.style.top           = 4f;
            _container.style.left          = 4f;
            _container.pickingMode = PickingMode.Ignore;
        }

        internal void SetRemoveCallback(Action<int> onRemove) => _onRemove = onRemove;

        // MF1: caller passes _input (TextField), not the parent area.
        internal void AttachTo(VisualElement parent) => parent.Add(_container);

        /// <summary>
        /// Flicker-diff: RepositionExisting when chip count unchanged; RebuildAll on mismatch.
        /// H10: if UitkCharRect.IsAvailable is false, uses row-layout (current pill strip).
        /// </summary>
        internal void Refresh()
        {
            if (_pills.Count == _tracker.Count)
                RepositionExisting();
            else
                RebuildAll();
        }

        internal void RemovePill(int chipIndex)
        {
            if (chipIndex < 0 || chipIndex >= _pills.Count) return;
            _container.Remove(_pills[chipIndex]);
            _pills.RemoveAt(chipIndex);
        }

        // ── private ───────────────────────────────────────────────────────────

        private void RebuildAll()
        {
            _container.Clear();
            _pills.Clear();
            for (int i = 0; i < _tracker.Count; i++)
                _pills.Add(BuildPill(i, _tracker[i].KindKey, _tracker[i].DisplayName));
        }

        /// <summary>
        /// Move existing pills to updated positions (inline when IsAvailable, else row-layout).
        /// MF5: pills whose Y rect exceeds the field height are hidden.
        /// </summary>
        private void RepositionExisting()
        {
            // MF5: use contentRect.height (actual content area) to avoid stale resolvedStyle.height
            // returning a huge finite value on first frame and flash-hiding valid pills.
            float fieldH = _field.contentRect.height;
            // Guard: fieldH == 0 before first layout — skip MF5 clip until layout is ready.
            bool  hasLayout = float.IsFinite(fieldH) && fieldH > 0f;
            bool  inline    = UitkCharRect.IsAvailable;

            for (int i = 0; i < _pills.Count; i++)
            {
                var pill = _pills[i];

                if (inline)
                {
                    // Inline positioning: place pill at the FFFC char position.
                    var text    = _field.value ?? "";
                    var spans   = TokenSpan.ComputeTokenSpans(text);
                    if (i < spans.Count && UitkCharRect.TryGetCharRect(_field, spans[i].Start, out var r))
                    {
                        pill.style.position = Position.Absolute;
                        pill.style.left     = r.x;
                        pill.style.top      = r.y;
                        // MF5: hide pill if its vertical position is outside the content area.
                        bool visible = !hasLayout || (r.y >= 0f && r.y < fieldH);
                        pill.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                        continue;
                    }
                }

                // Row-layout fallback: reset absolute positioning so flex wrapping applies.
                pill.style.position = Position.Relative;
                pill.style.left     = StyleKeyword.Auto;
                pill.style.top      = StyleKeyword.Auto;
                pill.style.display  = DisplayStyle.Flex;
            }
        }

        // Parse "#rrggbb" or "#rrggbbaa" hex color. Returns false if malformed.
        private static bool TryParseHex(string hex, out Color col)
        {
            col = Color.gray;
            if (string.IsNullOrEmpty(hex) || hex[0] != '#') return false;
            return ColorUtility.TryParseHtmlString(hex, out col);
        }

        private VisualElement BuildPill(int index, string kindKey, string displayName)
        {
            // Icon/color from registry (H6 identity — Key IS the prefix).
            var provider = ChipKindRegistry.ForKey(kindKey);

            var pill = new VisualElement();
            pill.AddToClassList("inline-chip-pill");
            pill.style.flexDirection  = FlexDirection.Row;
            pill.style.alignItems     = Align.Center;
            pill.style.paddingLeft    = pill.style.paddingRight = 4f;
            pill.style.marginRight    = 2f;
            pill.style.borderTopLeftRadius = pill.style.borderTopRightRadius     = 4f;
            pill.style.borderBottomLeftRadius = pill.style.borderBottomRightRadius = 4f;
            pill.pickingMode = PickingMode.Position;
            // Apply provider background color if available.
            if (provider != null && TryParseHex(provider.HexColor, out var col))
            {
                col.a = 0.85f;
                pill.style.backgroundColor = col;
            }

            var kindLbl = new Label(kindKey + ":");
            kindLbl.AddToClassList("inline-chip-kind");
            kindLbl.style.fontSize = 9f;

            var lbl = new Label(displayName); lbl.AddToClassList("inline-chip-label");
            lbl.style.fontSize = 10f;

            int capturedIndex = index;
            var btn = new Button(() => _onRemove?.Invoke(capturedIndex)) { text = "✕" };
            btn.AddToClassList("inline-chip-remove");
            btn.style.fontSize    = 9f;
            btn.style.marginLeft  = 2f;
            btn.style.paddingLeft = btn.style.paddingRight = 2f;
            btn.style.paddingTop  = btn.style.paddingBottom = 0f;

            pill.Add(kindLbl);
            pill.Add(lbl);
            pill.Add(btn);
            _container.Add(pill);
            return pill;
        }
    }
}
