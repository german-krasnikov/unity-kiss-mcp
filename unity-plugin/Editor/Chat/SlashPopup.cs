// UIToolkit popup for slash-command template selection.
// Shows a filtered list; arrow keys navigate, Enter/click selects, Esc/blur dismisses.
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class SlashPopup
    {
        private readonly VisualElement _anchor;
        private readonly TextField     _field;
        private VisualElement          _root;
        private ScrollView             _scrollView;
        private List<SlashTemplate>    _items = new List<SlashTemplate>();
        private int                    _selectedIndex;

        internal bool IsVisible    => _root != null && _root.parent != null;
        internal int  SelectedIndex => _selectedIndex;

        /// <summary>Number of rendered items — for testing (no MaxVisible cap).</summary>
        internal int VisibleCount => _scrollView?.childCount ?? 0;

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
            _scrollView = null;
        }

        internal void OnBlur() => Dismiss();

        internal void MoveDown()
        {
            if (_items.Count == 0) return;
            _selectedIndex = (_selectedIndex + 1) % _items.Count;
            HighlightSelected();
            ScrollToSelected();
        }

        internal void MoveUp()
        {
            if (_items.Count == 0) return;
            _selectedIndex = (_selectedIndex - 1 + _items.Count) % _items.Count;
            HighlightSelected();
            ScrollToSelected();
        }

        private void Rebuild()
        {
            Dismiss();
            if (_items.Count == 0) return;
            _root = new VisualElement();
            _root.AddToClassList("slash-popup");

            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            for (int i = 0; i < _items.Count; i++)
            {
                var idx = i;
                var item = new Label(_items[i].Name);
                item.AddToClassList("slash-item");
                if (i == _selectedIndex) item.AddToClassList("slash-item--selected");
                item.RegisterCallback<ClickEvent>(_ => Apply(_items[idx]));
                _scrollView.Add(item);
            }

            _root.Add(_scrollView);
            _anchor.Add(_root);
        }

        private void HighlightSelected()
        {
            if (_scrollView == null) return;
            for (int i = 0; i < _scrollView.childCount; i++)
                _scrollView[i].EnableInClassList("slash-item--selected", i == _selectedIndex);
        }

        private void ScrollToSelected()
        {
            if (_scrollView?.panel == null || _selectedIndex < 0 || _selectedIndex >= _scrollView.childCount) return;
            _scrollView.ScrollTo(_scrollView[_selectedIndex]);
        }
    }
}
