// Permissions popup UI for MCPChatWindow — pure UI, all logic in PermissionConfig.
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

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
            _root.style.paddingTop    = 6;
            _root.style.paddingBottom = 6;
            _root.style.paddingLeft   = 8;
            _root.style.paddingRight  = 8;
            Rebuild();
        }

        private void Rebuild()
        {
            _root.Clear();

            // Header row: [Allow All] [Deny All]
            var header = new VisualElement();
            header.style.flexDirection  = FlexDirection.Row;
            header.style.marginBottom   = 8;
            MakeHeaderBtn(header, "Allow All", () => { _config.AllowAll(); Rebuild(); });
            MakeHeaderBtn(header, "Deny All",  () => { _config.DenyAll();  Rebuild(); });
            _root.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;

            var states = _config.GetToolStates();
            var byCategory = states.GroupBy(s => s.category);

            foreach (var group in byCategory)
            {
                var cat     = group.Key;
                var tools   = group.ToList();
                var enabled = tools.Count(t => t.allowed);

                var foldout = new Foldout { text = $"{cat}  ({enabled}/{tools.Count})" };
                foldout.value = true;

                foreach (var (toolName, _, allowed) in tools)
                {
                    var name = toolName; // capture for lambda
                    var row  = new Toggle(name) { value = allowed };
                    row.style.marginLeft = 12;
                    row.RegisterValueChangedCallback(e =>
                    {
                        _config.SetToolAllowed(name, e.newValue);
                        // Update foldout header count live
                        var newEnabled = _config.GetToolStates()
                            .Count(s => s.category == cat && s.allowed);
                        foldout.text = $"{cat}  ({newEnabled}/{tools.Count})";
                    });
                    foldout.Add(row);
                }

                scroll.Add(foldout);
            }

            _root.Add(scroll);
        }

        private static void MakeHeaderBtn(VisualElement parent, string label, System.Action onClick)
        {
            var btn = new Button(onClick) { text = label };
            btn.style.marginRight = 4;
            parent.Add(btn);
        }
    }
}
