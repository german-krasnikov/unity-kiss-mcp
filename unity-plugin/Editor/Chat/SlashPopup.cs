// UIToolkit popup for slash-command template selection.
// Shows a filtered list; arrow keys navigate, Enter/click selects, Esc/blur dismisses.
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class SlashPopup
    {
        private const int MaxVisible = 5;

        private readonly VisualElement _anchor;
        private readonly TextField     _field;
        private VisualElement          _root;
        private List<SlashTemplate>    _items = new List<SlashTemplate>();
        private int                    _selectedIndex;

        internal bool IsVisible    => _root != null && _root.parent != null;
        internal int  SelectedIndex => _selectedIndex;

        internal SlashPopup(VisualElement anchor, TextField field)
        {
            _anchor = anchor;
            _field  = field;
        }

        internal void Show(IEnumerable<SlashTemplate> templates)
        {
            _items.Clear();
            foreach (var t in templates) _items.Add(t);
            _selectedIndex = 0;
            Rebuild();
        }

        /// <summary>Applies the currently selected item. Called by keyboard Enter handler.</summary>
        internal void ApplySelected()
        {
            if (_items.Count == 0) return;
            Apply(_items[_selectedIndex]);
        }

        internal void Apply(SlashTemplate t, Func<ContextGather, string> gatherOverride = null)
        {
            _field.value = SlashRegistry.Resolve(t, gatherOverride);
            Dismiss();
        }

        internal void Dismiss()
        {
            _root?.RemoveFromHierarchy();
            _root = null;
        }

        internal void OnBlur() => Dismiss();

        internal void MoveDown()
        {
            if (_items.Count == 0) return;
            _selectedIndex = (_selectedIndex + 1) % _items.Count;
            HighlightSelected();
        }

        internal void MoveUp()
        {
            if (_items.Count == 0) return;
            _selectedIndex = (_selectedIndex - 1 + _items.Count) % _items.Count;
            HighlightSelected();
        }

        private void Rebuild()
        {
            Dismiss();
            if (_items.Count == 0) return;
            _root = new VisualElement();
            _root.AddToClassList("slash-popup");

            for (int i = 0; i < _items.Count && i < MaxVisible; i++)
            {
                var idx = i;
                var item = new Label(_items[i].Name);
                item.AddToClassList("slash-item");
                if (i == _selectedIndex) item.AddToClassList("slash-item--selected");
                item.RegisterCallback<ClickEvent>(_ => Apply(_items[idx]));
                _root.Add(item);
            }
            _anchor.Add(_root);
        }

        private void HighlightSelected()
        {
            if (_root == null) return;
            int shown = System.Math.Min(_items.Count, MaxVisible);
            for (int i = 0; i < shown; i++)
            {
                var child = _root[i];
                child.EnableInClassList("slash-item--selected", i == _selectedIndex);
            }
        }
    }
}
