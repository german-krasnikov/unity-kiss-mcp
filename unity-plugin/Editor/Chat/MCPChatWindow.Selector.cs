// Agent / backend selector dropdown — partial of MCPChatWindow.
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private string             _selectedAgent; // null = default Claude
        private List<BackendSpec>  _backends;
        private DropdownField      _agentDropdown;

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
                _backend?.Stop();
                ResetTokenCounters();
                CreateBackend();
            });

            return _agentDropdown;
        }
    }
}
