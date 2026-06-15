// UIToolkit forms for per-backend settings. Pure UI wiring — no persistence logic.
// P4: BuildChipDisplayForm replaces hardcoded BuildChipConfigForm — registry-driven.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public static class BackendSettingsForm
    {
        private static readonly List<string> _depthOptions =
            new List<string> { "path", "summary", "full", "none" };

        /// <summary>Registry-driven chip display form: one row per registered kind.</summary>
        internal static void BuildChipDisplayForm(
            VisualElement parent,
            ChipConfig config,
            Action onSave)
        {
            foreach (var kindKey in ChipKindRegistry.AllKeys)
            {
                var provider = ChipKindRegistry.ForKey(kindKey);
                if (provider == null) continue;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems   = Align.Center;
                row.style.marginBottom = 2;

                var label = new Label(kindKey);
                label.style.width    = 90;
                label.style.fontSize = 11;
                row.Add(label);

                var currentDepth = config.DepthFor(kindKey);
                var depthField = new DropdownField(_depthOptions,
                    System.Math.Max(0, _depthOptions.IndexOf(currentDepth)));
                depthField.style.width = 80;
                var capturedKey = kindKey;
                depthField.RegisterValueChangedCallback(e =>
                {
                    config.SetDepthOverride(capturedKey, e.newValue);
                    onSave();
                });
                row.Add(depthField);

                var currentColor = config.ResolveColor(kindKey);
                ChipPillFactory.TryParseHex(currentColor, out var col);
                var colorField = new ColorField { value = col, showAlpha = false };
                colorField.style.width = 50;
                colorField.RegisterValueChangedCallback(e =>
                {
                    config.SetColorOverride(capturedKey,
                        "#" + ColorUtility.ToHtmlStringRGB(e.newValue));
                    onSave();
                });
                row.Add(colorField);

                var resetBtn = new Button(() =>
                {
                    config.SetDepthOverride(capturedKey, provider.DefaultDepth); // explicit default wins over legacy
                    config.SetColorOverride(capturedKey, null);                  // null → provider color
                    depthField.value = provider.DefaultDepth;
                    ChipPillFactory.TryParseHex(provider.HexColor, out var defaultCol);
                    colorField.value = defaultCol;
                    onSave();
                }) { text = "Reset" };
                resetBtn.style.fontSize   = 9;
                resetBtn.style.marginLeft = 4;
                row.Add(resetBtn);

                parent.Add(row);
            }
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

        internal static void BuildGeminiForm(
            VisualElement parent,
            GeminiBackendConfig config,
            Action onSave)
        {
            var autoGeminiPath = ChatBinaryResolver.Resolve("gemini");
            var hint = new Label($"Auto: {autoGeminiPath ?? "not found"}");
            hint.style.fontSize = 10;
            hint.style.color    = new StyleColor(autoGeminiPath != null
                ? new Color(0.5f, 0.8f, 0.5f) : new Color(0.8f, 0.4f, 0.4f));
            parent.Add(hint);

            if (autoGeminiPath == null)
            {
                var installHint = new Label("Install: npm install -g @google/gemini-cli");
                installHint.style.fontSize   = 9;
                installHint.style.color      = new StyleColor(new Color(0.9f, 0.7f, 0.3f));
                installHint.style.whiteSpace = WhiteSpace.Normal;
                parent.Add(installHint);
            }

            var geminiPathField = new TextField("Binary Path")
                { value = EditorPrefs.GetString(ChatBinaryResolver.GeminiPrefKey, "") };
            geminiPathField.RegisterValueChangedCallback(e =>
            {
                if (string.IsNullOrEmpty(e.newValue))
                    EditorPrefs.DeleteKey(ChatBinaryResolver.GeminiPrefKey);
                else
                    EditorPrefs.SetString(ChatBinaryResolver.GeminiPrefKey, e.newValue);
            });
            parent.Add(geminiPathField);

            var modelField = new TextField("Model") { value = config.Model };
            modelField.RegisterValueChangedCallback(e => { config.Model = e.newValue; onSave(); });
            parent.Add(modelField);

            var approvalField = new DropdownField(
                "Approval Mode",
                new List<string> { "default", "yolo" },
                config.ApprovalMode == "yolo" ? 1 : 0);
            approvalField.RegisterValueChangedCallback(e => { config.ApprovalMode = e.newValue == "default" ? "" : e.newValue; onSave(); });
            parent.Add(approvalField);

            var sandboxToggle = new Toggle("Sandbox") { value = config.Sandbox };
            sandboxToggle.RegisterValueChangedCallback(e => { config.Sandbox = e.newValue; onSave(); });
            parent.Add(sandboxToggle);

            var extraField = new TextField("Extra Args") { value = config.ExtraArgs };
            extraField.RegisterValueChangedCallback(e => { config.ExtraArgs = e.newValue; onSave(); });
            parent.Add(extraField);
        }

        internal static void BuildCodexForm(
            VisualElement parent,
            CodexBackendConfig config,
            Action onSave)
        {
            // Binary path override (R1 — escape hatch when where.exe/which can't find codex)
            var autoCodexPath = ChatBinaryResolver.Resolve("codex");
            var codexPathHint = new Label($"Auto: {autoCodexPath ?? "not found"}");
            codexPathHint.style.fontSize = 10;
            codexPathHint.style.color    = new StyleColor(autoCodexPath != null
                ? new Color(0.5f, 0.8f, 0.5f) : new Color(0.8f, 0.4f, 0.4f));
            parent.Add(codexPathHint);

            var codexPathField = new TextField("Binary Path")
                { value = EditorPrefs.GetString(ChatBinaryResolver.CodexPrefKey, "") };
            codexPathField.RegisterValueChangedCallback(e =>
            {
                if (string.IsNullOrEmpty(e.newValue))
                    EditorPrefs.DeleteKey(ChatBinaryResolver.CodexPrefKey);
                else
                    EditorPrefs.SetString(ChatBinaryResolver.CodexPrefKey, e.newValue);
            });
            parent.Add(codexPathField);

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
