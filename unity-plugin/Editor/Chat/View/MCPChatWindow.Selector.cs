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

        // Model selector
        private string        _selectedModel = "";
        private DropdownField _modelDropdown;
        private TextField     _customModelField;
        private const string  CustomModelSentinel = "__custom__";

        internal static readonly Dictionary<BackendKind, (string label, string modelId)[]> ModelPresetsPerKind
            = new Dictionary<BackendKind, (string, string)[]>
        {
            [BackendKind.Claude] = new[]
            {
                ("Default",   ""),
                ("Sonnet",    "claude-sonnet-4-6"),
                ("Opus",      "claude-opus-4-8"),
                ("Haiku",     "claude-haiku-4-5"),
                ("Fable",     "claude-fable-5"),
                ("Custom...", CustomModelSentinel),
            },
            [BackendKind.Codex] = new[]
            {
                ("Default",   ""),
                ("o3",        "o3"),
                ("o4-mini",   "o4-mini"),
                ("o3-pro",    "o3-pro"),
                ("gpt-4.1",   "gpt-4.1"),
                ("Custom...", CustomModelSentinel),
            },
            [BackendKind.Gemini] = new[]
            {
                ("Default",   ""),
                ("2.5 Pro",   "gemini-2.5-pro"),
                ("2.5 Flash", "gemini-2.5-flash"),
                ("2.0 Flash", "gemini-2.0-flash"),
                ("Custom...", CustomModelSentinel),
            },
        };

        // Backward-compat alias for existing tests
        internal static (string label, string modelId)[] ModelPresets
            => ModelPresetsPerKind[BackendKind.Claude];

        private static string ModelPrefKeyFor(BackendKind kind)
            => $"MCPChat.SelectedModel.{kind}";

        private static (string label, string modelId)[] PresetsForKind(BackendKind kind)
            => ModelPresetsPerKind.TryGetValue(kind, out var p) ? p
               : new[] { ("Default", "") };

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
                RebuildModelDropdown();
            });

            return _agentDropdown;
        }

        private VisualElement BuildModelSelector()
        {
            var presets = PresetsForKind(_selectedKind);
            var labels  = new List<string>();
            foreach (var p in presets) labels.Add(p.label);

            _modelDropdown = new DropdownField(labels, 0) { tooltip = "Model" };
            _modelDropdown.AddToClassList("agent-selector");

            _customModelField = new TextField { tooltip = "Custom model ID", value = "" };
            _customModelField.AddToClassList("agent-selector");
            _customModelField.style.display  = DisplayStyle.None;
            _customModelField.style.minWidth = 120;

            // Restore saved selection
            var saved = EditorPrefs.GetString(ModelPrefKeyFor(_selectedKind), "");
            var idx   = System.Array.FindIndex(presets, p => p.label == saved);
            if (idx >= 0)
            {
                _modelDropdown.SetValueWithoutNotify(presets[idx].label);
                if (presets[idx].modelId == CustomModelSentinel)
                {
                    var customVal = EditorPrefs.GetString(ModelPrefKeyFor(_selectedKind) + ".custom", "");
                    _customModelField.value          = customVal;
                    _customModelField.style.display  = DisplayStyle.Flex;
                    _selectedModel                   = customVal;
                }
                else
                {
                    _selectedModel = presets[idx].modelId;
                }
            }

            _modelDropdown.RegisterValueChangedCallback(evt =>
            {
                var presetArr = PresetsForKind(_selectedKind);
                var p = System.Array.Find(presetArr, x => x.label == evt.newValue);
                EditorPrefs.SetString(ModelPrefKeyFor(_selectedKind), evt.newValue);

                if (p.modelId == CustomModelSentinel)
                {
                    _customModelField.style.display = DisplayStyle.Flex;
                    _selectedModel = _customModelField.value;
                    return; // don't restart backend until user types a value
                }

                _customModelField.style.display = DisplayStyle.None;
                _selectedModel = p.modelId;
                _backend?.Stop();
                ResetTokenCounters();
                CreateBackend();
            });

            _customModelField.RegisterCallback<FocusOutEvent>(_ => ApplyCustomModel());
            _customModelField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    ApplyCustomModel();
            });

            _modelDropdown.SetEnabled(presets.Length > 1);

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.Add(_modelDropdown);
            container.Add(_customModelField);
            return container;
        }

        private void ApplyCustomModel()
        {
            var val = _customModelField?.value ?? "";
            EditorPrefs.SetString(ModelPrefKeyFor(_selectedKind) + ".custom", val);
            _selectedModel = val;
            _backend?.Stop();
            ResetTokenCounters();
            CreateBackend();
        }

        private void RebuildModelDropdown()
        {
            if (_modelDropdown == null) return;
            var presets = PresetsForKind(_selectedKind);
            var labels  = new List<string>();
            foreach (var p in presets) labels.Add(p.label);

            _modelDropdown.choices = labels;

            var saved = EditorPrefs.GetString(ModelPrefKeyFor(_selectedKind), "");
            var idx   = System.Array.FindIndex(presets, p => p.label == saved);
            if (idx < 0) idx = 0;

            _modelDropdown.SetValueWithoutNotify(presets[idx].label);
            _modelDropdown.SetEnabled(presets.Length > 1);

            if (presets[idx].modelId == CustomModelSentinel && _customModelField != null)
            {
                var customVal = EditorPrefs.GetString(ModelPrefKeyFor(_selectedKind) + ".custom", "");
                _customModelField.value         = customVal;
                _customModelField.style.display = DisplayStyle.Flex;
                _selectedModel                  = customVal;
            }
            else
            {
                _selectedModel = presets[idx].modelId;
                if (_customModelField != null) _customModelField.style.display = DisplayStyle.None;
            }
        }
    }
}
