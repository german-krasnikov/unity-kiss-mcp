// UIToolkit forms for per-backend settings. Pure UI wiring — no persistence logic.
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class BackendSettingsForm
    {
        private static readonly List<string> _depthOptions =
            new List<string> { "path", "summary", "full", "none" };

        internal static void BuildChipConfigForm(
            VisualElement parent,
            ChipConfig config,
            Action onSave)
        {
            var hierarchyField = new DropdownField("Hierarchy Depth", _depthOptions,
                System.Math.Max(0, _depthOptions.IndexOf(config.HierarchyDepth)));
            hierarchyField.RegisterValueChangedCallback(e => { config.HierarchyDepth = e.newValue; onSave(); });
            parent.Add(hierarchyField);

            var scriptField = new DropdownField("Script Depth", _depthOptions,
                System.Math.Max(0, _depthOptions.IndexOf(config.ScriptDepth)));
            scriptField.RegisterValueChangedCallback(e => { config.ScriptDepth = e.newValue; onSave(); });
            parent.Add(scriptField);

            var sceneField = new DropdownField("Scene Depth", _depthOptions,
                System.Math.Max(0, _depthOptions.IndexOf(config.SceneDepth)));
            sceneField.RegisterValueChangedCallback(e => { config.SceneDepth = e.newValue; onSave(); });
            parent.Add(sceneField);

            var prefabField = new DropdownField("Prefab Depth", _depthOptions,
                System.Math.Max(0, _depthOptions.IndexOf(config.PrefabDepth)));
            prefabField.RegisterValueChangedCallback(e => { config.PrefabDepth = e.newValue; onSave(); });
            parent.Add(prefabField);

            var assetField = new DropdownField("Asset Depth", _depthOptions,
                System.Math.Max(0, _depthOptions.IndexOf(config.AssetDepth)));
            assetField.RegisterValueChangedCallback(e => { config.AssetDepth = e.newValue; onSave(); });
            parent.Add(assetField);
        }

        internal static void BuildClaudeForm(
            VisualElement parent,
            ClaudeBackendConfig config,
            Action onSave)
        {
            var modelField = new TextField("Model") { value = config.Model };
            modelField.RegisterValueChangedCallback(e => { config.Model = e.newValue; onSave(); });
            parent.Add(modelField);

            var permField = new DropdownField(
                "Permission Mode",
                new List<string> { "plan", "acceptEdits" },
                config.PermissionMode == "acceptEdits" ? 1 : 0);
            permField.RegisterValueChangedCallback(e => { config.PermissionMode = e.newValue; onSave(); });
            parent.Add(permField);

            var extraField = new TextField("Extra Args") { value = config.ExtraArgs };
            extraField.RegisterValueChangedCallback(e => { config.ExtraArgs = e.newValue; onSave(); });
            parent.Add(extraField);
        }

        internal static void BuildCodexForm(
            VisualElement parent,
            CodexBackendConfig config,
            Action onSave)
        {
            var modelField = new TextField("Model") { value = config.Model };
            modelField.RegisterValueChangedCallback(e => { config.Model = e.newValue; onSave(); });
            parent.Add(modelField);

            var permField = new DropdownField(
                "Permission Mode",
                new List<string> { "danger-full-access" },
                0);
            permField.RegisterValueChangedCallback(e => { config.PermissionMode = e.newValue; onSave(); });
            parent.Add(permField);

            var timeoutField = new IntegerField("Startup Timeout (s)") { value = config.StartupTimeoutSec };
            timeoutField.RegisterValueChangedCallback(e => { config.StartupTimeoutSec = e.newValue; onSave(); });
            parent.Add(timeoutField);

            var extraField = new TextField("Extra Args") { value = config.ExtraArgs };
            extraField.RegisterValueChangedCallback(e => { config.ExtraArgs = e.newValue; onSave(); });
            parent.Add(extraField);
        }
    }
}
