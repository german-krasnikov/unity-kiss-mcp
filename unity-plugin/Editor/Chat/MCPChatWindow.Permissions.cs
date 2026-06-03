// Permissions popup UI for MCPChatWindow — pure UI, all logic in PermissionConfig.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private Button BuildPermissionsButton()
        {
            var btn = new Button(ShowPermissionsPopup) { text = "Perms",
                tooltip = "Tool Permissions — control which MCP tools the agent can use" };
            btn.AddToClassList("chat-btn");
            btn.AddToClassList("chat-btn--perms");
            return btn;
        }

        private void ShowPermissionsPopup() =>
            PermissionsPopup.Show(_permConfig);
    }

    /// <summary>EditorWindow-based popup for per-tool permission toggles.</summary>
    internal sealed class PermissionsPopup : EditorWindow
    {
        private PermissionConfig _config;
        private VisualElement    _root;
        private readonly List<PermCategoryGroup> _groups = new List<PermCategoryGroup>();

        public static void Show(PermissionConfig config)
        {
            if (HasOpenInstances<PermissionsPopup>())
            {
                GetWindow<PermissionsPopup>().Focus();
                return;
            }
            var win = CreateInstance<PermissionsPopup>();
            win._config = config;
            win.titleContent = new GUIContent("Tool Permissions");
            win.minSize = new Vector2(300, 420);
            win.ShowUtility();
        }

        private void CreateGUI()
        {
            _root = rootVisualElement;
            // Load the same stylesheet as MCP Settings so classes match exactly.
            var ss = MCPEditorUtils.LoadStyleSheet("MCPSettings.uss");
            if (ss != null) _root.styleSheets.Add(ss);
            _root.style.paddingTop    = 6;
            _root.style.paddingBottom = 6;
            _root.style.paddingLeft   = 8;
            _root.style.paddingRight  = 8;
            Rebuild();
        }

        private void Rebuild()
        {
            _root.Clear();
            _groups.Clear();

            // Header row: [Allow All] [Deny All] — styled with preset-row / preset-btn
            var header = new VisualElement();
            header.AddToClassList("preset-row");
            MakePresetBtn(header, "Allow All", () => { _config.AllowAll(); Rebuild(); });
            MakePresetBtn(header, "Deny All",  () => { _config.DenyAll();  Rebuild(); });
            _root.Add(header);

            // Search field — same class as Settings window
            var search = new TextField { tooltip = "Filter tools by name" };
            search.AddToClassList("search-field");
            _root.Add(search);

            // Scroll area — same class as Settings window
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("tool-scroll");

            var byCategory = _config.GetToolStates()
                .GroupBy(s => s.category)
                .ToDictionary(g => g.Key, g => g.Select(s => s.toolName).ToArray());

            foreach (var kv in byCategory)
            {
                var group = new PermCategoryGroup(kv.Key, kv.Value, _config);
                scroll.Add(group.Element);
                _groups.Add(group);
            }

            _root.Add(scroll);

            // Wire search
            search.RegisterValueChangedCallback(evt =>
            {
                var q = evt.newValue.Trim();
                foreach (var g in _groups) g.Filter(q);
            });
        }

        private static void MakePresetBtn(VisualElement parent, string label, System.Action onClick)
        {
            var btn = new Button(onClick) { text = label };
            btn.AddToClassList("preset-btn");
            parent.Add(btn);
        }
    }
}
