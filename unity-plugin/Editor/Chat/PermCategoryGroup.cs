// UIToolkit tri-state foldout group for PermissionsPopup.
// Mirrors MCPSettingsCategoryGroup visual structure via the same USS classes from MCPSettings.uss.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Foldout with per-tool toggles and a tri-state master toggle.
    /// Reads/writes via <see cref="PermissionConfig"/> (not EditorPrefs directly).
    /// Reuses MCPSettings.uss class names so it looks identical to the Settings window.
    /// </summary>
    internal sealed class PermCategoryGroup
    {
        public VisualElement Element { get; }

        private readonly string _category;
        private readonly string[] _tools;
        private readonly PermissionConfig _config;
        private readonly Action _onChanged;
        private readonly Toggle _master;
        private readonly Foldout _foldout;
        private readonly List<(string name, Toggle toggle)> _rows;

        public PermCategoryGroup(string category, string[] tools, PermissionConfig config, Action onChanged = null)
        {
            _category  = category;
            _tools     = tools;
            _config    = config;
            _onChanged = onChanged;
            _rows      = new List<(string, Toggle)>(tools.Length);

            _foldout = new Foldout { text = HeaderText() };
            _foldout.AddToClassList("category-foldout");
            _foldout.value = false;

            _master = new Toggle { label = "" };
            _master.AddToClassList("master-toggle");
            RefreshMasterState();

            // Inject master toggle into foldout's own toggle element (same pattern as CategoryGroup)
            var headerToggle = _foldout.Q<Toggle>();
            headerToggle?.Add(_master);

            _master.RegisterValueChangedCallback(OnMasterChanged);

            foreach (var tool in tools)
            {
                var row = BuildRow(tool);
                _foldout.contentContainer.Add(row.element);
                _rows.Add((tool, row.toggle));
            }

            Element = _foldout;
        }

        private (VisualElement element, Toggle toggle) BuildRow(string tool)
        {
            var row = new VisualElement();
            row.AddToClassList("tool-row");

            var name   = tool; // capture
            var states = _config.GetToolStates();
            var entry  = states.FirstOrDefault(s => s.toolName == name);
            var toggle = new Toggle(tool) { value = entry.allowed };
            toggle.AddToClassList("tool-toggle");

            toggle.RegisterValueChangedCallback(evt =>
            {
                _config.SetToolAllowed(name, evt.newValue);
                RefreshMasterState();
                _foldout.text = HeaderText();
                _onChanged?.Invoke();
            });

            row.Add(toggle);
            return (row, toggle);
        }

        private void OnMasterChanged(ChangeEvent<bool> evt)
        {
            _config.SetCategoryAllowed(_category, evt.newValue);
            foreach (var (_, toggle) in _rows)
                toggle.SetValueWithoutNotify(evt.newValue);
            _master.RemoveFromClassList("toggle-mixed");
            _foldout.text = HeaderText();
            _onChanged?.Invoke();
        }

        private void RefreshMasterState()
        {
            var states  = _config.GetToolStates();
            int total   = _tools.Length;
            int allowed = states.Count(s => Array.IndexOf(_tools, s.toolName) >= 0 && s.allowed);

            if (allowed == total)
            {
                _master.SetValueWithoutNotify(true);
                _master.RemoveFromClassList("toggle-mixed");
            }
            else if (allowed == 0)
            {
                _master.SetValueWithoutNotify(false);
                _master.RemoveFromClassList("toggle-mixed");
            }
            else
            {
                _master.SetValueWithoutNotify(false);
                _master.AddToClassList("toggle-mixed");
            }
        }

        private string HeaderText()
        {
            var states  = _config.GetToolStates();
            int allowed = states.Count(s => Array.IndexOf(_tools, s.toolName) >= 0 && s.allowed);
            return $"{_category}  ({allowed}/{_tools.Length})";
        }

        public void Filter(string query)
        {
            bool anyVisible = false;
            foreach (var (tool, toggle) in _rows)
            {
                bool visible = string.IsNullOrEmpty(query)
                            || tool.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                toggle.parent.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible) anyVisible = true;
            }
            Element.style.display = (anyVisible || string.IsNullOrEmpty(query))
                ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
