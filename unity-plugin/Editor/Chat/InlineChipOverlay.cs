// InlineChipOverlay: renders pill VisualElements over the TextField.
// Positioning: row-fallback (pills left-to-right, pinned to field top-left).
// Unity 2021 TextField does not expose a public char-rect API, so exact
// inline positioning at the caret location is not feasible; pills are shown
// as a floating row overlay at the top of the field instead.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class InlineChipOverlay
    {
        private readonly TextField        _field;
        private readonly InlineChipTracker _tracker;
        private readonly VisualElement    _container; // absolute-positioned, covers _field
        private readonly List<VisualElement> _pills = new List<VisualElement>();

        private Action<int> _onRemove; // callback(chipIndex)

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
            // Pointer events pass through to the TextField underneath
            _container.pickingMode = PickingMode.Ignore;
        }

        internal void SetRemoveCallback(Action<int> onRemove) => _onRemove = onRemove;

        /// <summary>Attach overlay container as a sibling after the TextField.</summary>
        internal void AttachTo(VisualElement parent)
        {
            parent.Add(_container);
        }

        /// <summary>Rebuild all pills to match the tracker state.</summary>
        internal void Refresh()
        {
            _container.Clear();
            _pills.Clear();
            for (int i = 0; i < _tracker.Count; i++)
                _pills.Add(BuildPill(i, _tracker[i].Kind, _tracker[i].DisplayName));
        }

        /// <summary>Remove a single pill by chip index.</summary>
        internal void RemovePill(int chipIndex)
        {
            if (chipIndex < 0 || chipIndex >= _pills.Count) return;
            _container.Remove(_pills[chipIndex]);
            _pills.RemoveAt(chipIndex);
        }

        // ── private ───────────────────────────────────────────────────────────

        private VisualElement BuildPill(int index, ChipKind kind, string displayName)
        {
            var pill = new VisualElement();
            pill.AddToClassList("inline-chip-pill");
            pill.style.flexDirection  = FlexDirection.Row;
            pill.style.alignItems     = Align.Center;
            pill.style.paddingLeft    = pill.style.paddingRight = 4f;
            pill.style.marginRight    = 2f;
            pill.style.borderTopLeftRadius = pill.style.borderTopRightRadius     = 4f;
            pill.style.borderBottomLeftRadius = pill.style.borderBottomRightRadius = 4f;
            pill.pickingMode = PickingMode.Position;

            var kindLbl = new Label(ChipKindDetector.ShortPrefix(kind) + ":");
            kindLbl.AddToClassList("inline-chip-kind");
            kindLbl.style.fontSize = 9f;

            var lbl = new Label(displayName); lbl.AddToClassList("inline-chip-label");
            lbl.style.fontSize = 10f;

            // Capture index by value for the closure
            int capturedIndex = index;
            var btn = new Button(() => _onRemove?.Invoke(capturedIndex)) { text = "✕" };
            btn.AddToClassList("inline-chip-remove");
            btn.style.fontSize       = 9f;
            btn.style.marginLeft     = 2f;
            btn.style.paddingLeft    = btn.style.paddingRight = 2f;
            btn.style.paddingTop     = btn.style.paddingBottom = 0f;

            pill.Add(kindLbl);
            pill.Add(lbl);
            pill.Add(btn);
            _container.Add(pill);
            return pill;
        }
    }
}
