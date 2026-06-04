// UIToolkit forms for per-backend settings. Pure UI wiring — no persistence logic.
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class BackendSettingsForm
    {
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
