using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// UIToolkit foldout with tri-state master toggle for a tool category.
    /// </summary>
    internal sealed class CategoryGroup
    {
        public VisualElement Element { get; }

        private readonly string _category;
        private readonly string[] _tools;
        private readonly Toggle _master;
        private readonly List<(string name, Toggle toggle)> _rows;
        private bool _isCore;
        private string _filterQuery = "";

        public CategoryGroup(string category, string[] tools)
        {
            _category = category;
            _tools = tools;
            _isCore = category == "CORE";
            _rows = new List<(string, Toggle)>();

            var foldout = new Foldout { text = $"{category}  ({tools.Length})" };
            foldout.AddToClassList("category-foldout");
            foldout.value = false; // collapsed by default

            // Master tri-state toggle in foldout header
            _master = new Toggle();
            _master.AddToClassList("master-toggle");
            _master.label = "";
            if (_isCore) _master.SetEnabled(false);
            UpdateMasterState();

            // Inject master toggle into foldout header
            var header = foldout.Q<Toggle>();
            if (header != null)
                header.Add(_master);

            _master.RegisterValueChangedCallback(OnMasterChanged);

            // Per-tool rows
            foreach (var tool in tools)
            {
                var row = BuildToolRow(tool);
                foldout.contentContainer.Add(row.element);
                _rows.Add((tool, row.toggle));
            }

            Element = foldout;
        }

        private (VisualElement element, Toggle toggle) BuildToolRow(string tool)
        {
            var row = new VisualElement();
            row.AddToClassList("tool-row");

            var toggle = new Toggle(tool)
                { value = MCPSettings.IsToolEnabled(tool) };
            toggle.AddToClassList("tool-toggle");

            if (_isCore)
                toggle.SetEnabled(false);

            toggle.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(MCPSettings.KeyPrefix + tool, evt.newValue);
                CommandRouter.InvalidateEnabledToolsCache();
                UpdateMasterState();
            });

            row.Add(toggle);
            return (row, toggle);
        }

        private void OnMasterChanged(ChangeEvent<bool> evt)
        {
            if (_isCore) return;
            foreach (var (tool, toggle) in _rows)
            {
                toggle.SetValueWithoutNotify(evt.newValue);
                EditorPrefs.SetBool(MCPSettings.KeyPrefix + tool, evt.newValue);
            }
            CommandRouter.InvalidateEnabledToolsCache();
        }

        private void UpdateMasterState()
        {
            int enabled = _rows.Count(r => MCPSettings.IsToolEnabled(r.name));
            if (enabled == _rows.Count)
                _master.SetValueWithoutNotify(true);
            else if (enabled == 0)
                _master.SetValueWithoutNotify(false);
            else
            {
                // Mixed — show indeterminate via USS class
                _master.SetValueWithoutNotify(false);
                _master.AddToClassList("toggle-mixed");
                return;
            }
            _master.RemoveFromClassList("toggle-mixed");
        }

        public void Filter(string query)
        {
            _filterQuery = query;
            bool anyVisible = false;
            foreach (var (tool, toggle) in _rows)
            {
                bool visible = string.IsNullOrEmpty(query) || tool.Contains(query);
                toggle.parent.style.display = visible
                    ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible) anyVisible = true;
            }
            Element.style.display = anyVisible || string.IsNullOrEmpty(query)
                ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
