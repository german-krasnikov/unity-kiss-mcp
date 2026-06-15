// Agent / backend selector dropdown — partial of MCPChatWindow.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private string             _selectedAgent; // null = default Claude
        private List<BackendSpec>  _backends;
        private DropdownField      _agentDropdown;
        private const string       DropdownPrefKey = "MCPChat.SelectedBackend";

        private void RefreshBackends()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var home        = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            _backends       = BackendRegistry.Discover(AgentSearchPath.Resolve(projectRoot, home));
        }

        private VisualElement BuildAgentSelector()
        {
            if (_backends == null) RefreshBackends();

            var choices = new List<string>();
            foreach (var b in _backends) choices.Add(b.DisplayName);

            _agentDropdown = new DropdownField(choices, 0)
            {
                tooltip = "Backend / Agent"
            };
            _agentDropdown.AddToClassList("agent-selector");

            // F23: restore last selection from EditorPrefs.
            var saved = EditorPrefs.GetString(DropdownPrefKey, "");
            if (saved == "Codex (Session)") saved = "Codex"; // F28: migrate renamed backend
            if (!string.IsNullOrEmpty(saved) && choices.Contains(saved))
            {
                _agentDropdown.SetValueWithoutNotify(saved);
                var spec = _backends.Find(b => b.DisplayName == saved);
                if (spec.Enabled)
                {
                    _selectedKind  = spec.Kind;
                    _selectedAgent = spec.AgentName;
                    _backend?.Stop();    // P0-3: OnEnable created a default-Kind backend before this restore
                    CreateBackend();     // P0-3: recreate to match restored selection
                }
            }

            _agentDropdown.RegisterValueChangedCallback(evt =>
            {
                var chosenName = evt.newValue;
                var spec       = _backends.Find(b => b.DisplayName == chosenName);

                if (!spec.Enabled)
                {
                    // Revert to previous selection — placeholder, do nothing
                    _agentDropdown.SetValueWithoutNotify(evt.previousValue);
                    return;
                }

                _selectedKind  = spec.Kind;
                _selectedAgent = spec.AgentName;
                EditorPrefs.SetString(DropdownPrefKey, chosenName); // F23: persist selection.
                _backend?.Stop();
                ResetTokenCounters();
                CreateBackend();
            });

            return _agentDropdown;
        }
    }
}
