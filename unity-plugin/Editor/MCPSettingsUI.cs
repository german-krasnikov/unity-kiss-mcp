using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Builds the UIToolkit settings form.
    /// Split from MCPSettings.cs to keep each file under 200 lines.
    /// </summary>
    internal static class MCPSettingsUI
    {
        // ── Entry point ───────────────────────────────────────────────────────
        public static void Build(VisualElement root)
        {
            var ss = LoadStyleSheet();
            if (ss != null) root.styleSheets.Add(ss);

            // Header banner
            root.Add(BuildHeader());

            // Presets row
            root.Add(BuildPresets());

            // Auto-discard toggle
            root.Add(BuildAutoDiscard());

            // Info label
            var info = new Label("Changes apply on next MCP reconnect.");
            info.AddToClassList("info-label");
            root.Add(info);

            // Search box
            var searchField = new TextField();
            searchField.value = "";
            searchField.tooltip = "Filter tools by name";
            searchField.AddToClassList("search-field");
            root.Add(searchField);

            // Scroll area for categories
            var scroll = new ScrollView();
            scroll.AddToClassList("tool-scroll");
            root.Add(scroll);

            var categories = MCPSettings.GetCatalogCategories();
            var allGroups = new List<CategoryGroup>();

            foreach (var kv in categories)
            {
                if (kv.Value == null || kv.Value.Length == 0) continue;
                var group = new CategoryGroup(kv.Key, kv.Value);
                scroll.Add(group.Element);
                allGroups.Add(group);
            }

            // Plugins section
            var pluginGroup = BuildPluginsSection();
            if (pluginGroup != null) scroll.Add(pluginGroup);

            // Wire search
            searchField.RegisterValueChangedCallback(evt =>
            {
                var q = evt.newValue.ToLowerInvariant().Trim();
                foreach (var g in allGroups) g.Filter(q);
            });
        }

        // ── Header ───────────────────────────────────────────────────────────
        private static VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("mcp-header");
            var title = new Label("Unity MCP Settings");
            title.AddToClassList("mcp-header-title");
            header.Add(title);
            return header;
        }

        // ── Presets ───────────────────────────────────────────────────────────
        private static VisualElement BuildPresets()
        {
            var row = new VisualElement();
            row.AddToClassList("preset-row");
            AddPresetButton(row, "Minimal",    ApplyMinimal);
            AddPresetButton(row, "Full",       ApplyFull);
            AddPresetButton(row, "No-visuals", ApplyNoVisuals);
            return row;
        }

        private static void AddPresetButton(VisualElement parent, string label, Action action)
        {
            var btn = new Button(action) { text = label };
            btn.AddToClassList("preset-btn");
            parent.Add(btn);
        }

        private static void ApplyMinimal()
        {
            var cats = MCPSettings.GetCatalogCategories();
            var allTools = cats.SelectMany(kv => kv.Value).Distinct();
            var core = cats.TryGetValue("CORE", out var c) ? new HashSet<string>(c) : new HashSet<string>();
            foreach (var t in allTools)
                EditorPrefs.SetBool(MCPSettings.KeyPrefix + t, core.Contains(t));
            CommandRouter.InvalidateEnabledToolsCache();
        }

        private static void ApplyFull()
        {
            var allTools = MCPSettings.GetToolNames();
            foreach (var t in allTools) EditorPrefs.SetBool(MCPSettings.KeyPrefix + t, true);
            CommandRouter.InvalidateEnabledToolsCache();
        }

        private static readonly HashSet<string> _noVisualsOff =
            new HashSet<string> { "SCREENSHOTS", "ANIMATION", "SHADERS_MATERIAL", "VFX" };

        private static void ApplyNoVisuals()
        {
            var cats = MCPSettings.GetCatalogCategories();
            foreach (var kv in cats)
            {
                bool off = _noVisualsOff.Contains(kv.Key);
                foreach (var t in kv.Value) EditorPrefs.SetBool(MCPSettings.KeyPrefix + t, !off);
            }
            CommandRouter.InvalidateEnabledToolsCache();
        }

        // ── Auto-discard ──────────────────────────────────────────────────────
        private static VisualElement BuildAutoDiscard()
        {
            var row = new VisualElement();
            row.AddToClassList("option-row");
            var toggle = new Toggle("Auto-discard scene on quit")
                { value = MCPSettings.AutoDiscardScene };
            toggle.RegisterValueChangedCallback(e =>
                EditorPrefs.SetBool(MCPSettings.KeyAutoDiscard, e.newValue));
            row.Add(toggle);
            return row;
        }

        // ── Plugins section ───────────────────────────────────────────────────
        private static VisualElement BuildPluginsSection()
        {
            var plugins = PluginRegistry.GetAll();
            if (plugins.Count == 0) return null;

            var section = new VisualElement();
            section.AddToClassList("plugin-section");
            var hdr = new Label("Plugins");
            hdr.AddToClassList("plugin-section-header");
            section.Add(hdr);

            foreach (var plugin in plugins)
            {
                var pluginTools = CommandRegistry.GetAllCommands()
                    .Where(c => (!string.IsNullOrEmpty(plugin.CommandPrefix) &&
                                 (c == plugin.CommandPrefix || c.StartsWith(plugin.CommandPrefix + "_")))
                             || plugin.AdditionalCommands.Contains(c))
                    .ToArray();
                if (pluginTools.Length == 0) continue;
                var group = new CategoryGroup(plugin.Name, pluginTools);
                section.Add(group.Element);
            }
            return section;
        }

        // ── StyleSheet ───────────────────────────────────────────────────────
        private static StyleSheet LoadStyleSheet() =>
            MCPEditorUtils.LoadStyleSheet("MCPSettings.uss");
    }
}
