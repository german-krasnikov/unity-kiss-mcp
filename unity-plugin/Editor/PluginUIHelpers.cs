using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Convenience layer for plugin settings UI.
    /// Styles inherited automatically when inside MCP Hub pages.
    /// For standalone EditorWindows call <see cref="LoadStyles"/> once on the root.
    /// </summary>
    public static class PluginUIHelpers
    {
        /// <summary>Adds MCPSettings.uss + MCPHub.uss. Only needed for standalone EditorWindows.</summary>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="root"/> is null.</exception>
        public static void LoadStyles(VisualElement root)
        {
            if (root == null) throw new System.ArgumentNullException(nameof(root));
            var s1 = MCPEditorUtils.LoadStyleSheet("MCPSettings.uss");
            if (s1 != null) root.styleSheets.Add(s1);
            var s2 = MCPEditorUtils.LoadStyleSheet("MCPHub.uss");
            if (s2 != null) root.styleSheets.Add(s2);
        }

        /// <summary>Bordered foldout using sampling-card style.</summary>
        public static Foldout MakeCard(string title, bool open = false)
        {
            var card = new Foldout { text = title, value = open };
            card.AddToClassList("sampling-card");
            return card;
        }

        /// <summary>Horizontal row — children stretch equally.</summary>
        public static VisualElement InlineRow()
        {
            var row = new VisualElement();
            row.AddToClassList("sampling-inline-row");
            return row;
        }

        /// <summary>Adds a text field bound to <see cref="PluginConfig"/>. Changes are saved immediately.</summary>
        public static TextField AddTextField(VisualElement parent, string label,
            string pluginId, string key, string defaultValue = "")
        {
            var el = new TextField(label) { value = PluginConfig.GetString(pluginId, key, defaultValue) };
            el.RegisterValueChangedCallback(e => PluginConfig.SetString(pluginId, key, e.newValue));
            parent.Add(el);
            return el;
        }

        /// <summary>Adds a toggle bound to <see cref="PluginConfig"/>. Changes are saved immediately.</summary>
        public static Toggle AddToggle(VisualElement parent, string label,
            string pluginId, string key, bool defaultValue = false)
        {
            var el = new Toggle(label) { value = PluginConfig.GetBool(pluginId, key, defaultValue) };
            el.RegisterValueChangedCallback(e => PluginConfig.SetBool(pluginId, key, e.newValue));
            parent.Add(el);
            return el;
        }

        /// <summary>Adds a float slider bound to <see cref="PluginConfig"/>. Changes are saved immediately.</summary>
        public static Slider AddSlider(VisualElement parent, string label,
            string pluginId, string key, float defaultValue, float min, float max)
        {
            var el = new Slider(label, min, max) { value = PluginConfig.GetFloat(pluginId, key, defaultValue) };
            el.RegisterValueChangedCallback(e => PluginConfig.SetFloat(pluginId, key, e.newValue));
            parent.Add(el);
            return el;
        }

        /// <summary>Adds an integer slider bound to <see cref="PluginConfig"/>. Changes are saved immediately.</summary>
        public static SliderInt AddIntSlider(VisualElement parent, string label,
            string pluginId, string key, int defaultValue, int min, int max)
        {
            var el = new SliderInt(label, min, max) { value = PluginConfig.GetInt(pluginId, key, defaultValue) };
            el.RegisterValueChangedCallback(e => PluginConfig.SetInt(pluginId, key, e.newValue));
            parent.Add(el);
            return el;
        }

        /// <summary>
        /// Adds a dropdown bound to <see cref="PluginConfig"/>. Changes are saved immediately.
        /// If the saved value is no longer in <paramref name="choices"/>, falls back to <paramref name="defaultValue"/>.
        /// </summary>
        /// <exception cref="System.ArgumentException">Thrown when <paramref name="choices"/> is null or empty.</exception>
        public static DropdownField AddDropdown(VisualElement parent, string label,
            string pluginId, string key, string[] choices, string defaultValue = null)
        {
            if (choices == null || choices.Length == 0)
                throw new System.ArgumentException("choices must be non-null and non-empty", nameof(choices));

            var def   = defaultValue ?? choices[0];
            var saved = PluginConfig.GetString(pluginId, key, def);
            if (System.Array.IndexOf(choices, saved) < 0) saved = def;

            var el = new DropdownField(label, new List<string>(choices), 0) { value = saved };
            el.RegisterValueChangedCallback(e => PluginConfig.SetString(pluginId, key, e.newValue));
            parent.Add(el);
            return el;
        }
    }
}
