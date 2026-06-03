// Agent Tool Permissions section for MCP Settings window.
// Hosts PermCategoryGroup foldouts backed by PermissionConfig (shared EditorPrefs prefix).
// Always shown — no #if guard — lets users pre-configure before enabling Agent Chat.
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class MCPSettingsPermUI
    {
        public static VisualElement BuildSection()
        {
            var config = new PermissionConfig(); // default ctor → DEFAULT_PREFIX, shared prefs
            var groups = new List<PermCategoryGroup>();

            var foldout = new Foldout { text = "Agent Tool Permissions" };
            foldout.AddToClassList("category-foldout");
            foldout.value = false; // collapsed by default

            // Preset row: Allow All / Deny All
            var presetRow = new VisualElement();
            presetRow.AddToClassList("preset-row");
            AddPresetBtn(presetRow, "Allow All", () => { config.AllowAll(); RebuildScroll(foldout, config, groups); });
            AddPresetBtn(presetRow, "Deny All",  () => { config.DenyAll();  RebuildScroll(foldout, config, groups); });
            foldout.Add(presetRow);

            // Search field
            var search = new TextField { tooltip = "Filter tools by name" };
            search.AddToClassList("search-field");
            foldout.Add(search);

            // Scroll area
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("tool-scroll");
            foldout.Add(scroll);

            BuildGroups(scroll, config, groups);

            // Wire search
            search.RegisterValueChangedCallback(evt =>
            {
                var q = evt.newValue.Trim();
                foreach (var g in groups) g.Filter(q);
            });

            return foldout;
        }

        private static void RebuildScroll(Foldout foldout, PermissionConfig config, List<PermCategoryGroup> groups)
        {
            var scroll = foldout.Q<ScrollView>();
            if (scroll == null) return;
            scroll.Clear();
            groups.Clear();
            BuildGroups(scroll, config, groups);
        }

        private static void BuildGroups(ScrollView scroll, PermissionConfig config, List<PermCategoryGroup> groups)
        {
            var byCategory = config.GetToolStates()
                .GroupBy(s => s.category)
                .ToDictionary(g => g.Key, g => g.Select(s => s.toolName).ToArray());

            foreach (var kv in byCategory)
            {
                var group = new PermCategoryGroup(kv.Key, kv.Value, config);
                scroll.Add(group.Element);
                groups.Add(group);
            }
        }

        private static void AddPresetBtn(VisualElement parent, string label, System.Action onClick)
        {
            var btn = new Button(onClick) { text = label };
            btn.AddToClassList("preset-btn");
            parent.Add(btn);
        }
    }
}
