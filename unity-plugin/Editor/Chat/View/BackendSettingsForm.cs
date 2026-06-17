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

        private static readonly (string name, string label)[] _allowedTypeRows =
        {
            ("GameObject",     "GameObject (Prefabs)"),
            ("Material",       "Material"),
            ("Texture",        "Texture (Texture2D, Cubemap...)"),
            ("AnimationClip",  "Animation Clip"),
            ("MonoScript",     "Script (MonoScript)"),
            ("Mesh",           "Mesh (.fbx, .obj)"),
            ("AudioClip",      "Audio Clip"),
            ("ScriptableObject", "ScriptableObject"),
        };

        /// <summary>Registry-driven chip display form: one row per registered kind.</summary>
        internal static void BuildChipDisplayForm(
            VisualElement parent,
            ChipConfig config,
            Action onSave)
        {
            // Allowed asset types section
            var header = new Label("Allowed Chip Types");
            header.AddToClassList("settings-section-header");
            parent.Add(header);

            foreach (var (name, lbl) in _allowedTypeRows)
            {
                var toggle = new Toggle(lbl) { value = ChatChipPolicy.IsTypeEnabled(name) };
                var capturedName = name;
                toggle.RegisterValueChangedCallback(evt =>
                    EditorPrefs.SetBool(ChatChipPolicy.PrefKey(capturedName), evt.newValue));
                parent.Add(toggle);
            }

            var separator = new VisualElement();
            separator.style.height      = 1;
            separator.style.marginTop   = 4;
            separator.style.marginBottom = 4;
            separator.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            parent.Add(separator);

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

        /// <summary>
        /// Shared: "Auto: path" hint + optional install hint + binary path EditorPrefs field.
        /// Used by Gemini, Kimi, and OpenCode forms.
        /// </summary>
        private static void BuildBinarySection(
            VisualElement parent,
            string binaryName,
            string prefKey,
            string installHint)
        {
            var autoPath = ChatBinaryResolver.Resolve(binaryName);
            var hint = new Label($"Auto: {autoPath ?? "not found"}");
            hint.style.fontSize = 10;
            hint.style.color    = new StyleColor(autoPath != null
                ? new Color(0.5f, 0.8f, 0.5f) : new Color(0.8f, 0.4f, 0.4f));
            parent.Add(hint);

            if (autoPath == null && !string.IsNullOrEmpty(installHint))
            {
                var install = new Label(installHint);
                install.style.fontSize   = 9;
                install.style.color      = new StyleColor(new Color(0.9f, 0.7f, 0.3f));
                install.style.whiteSpace = WhiteSpace.Normal;
                parent.Add(install);
            }

            var pathField = new TextField("Binary Path")
                { value = EditorPrefs.GetString(prefKey, "") };
            pathField.RegisterValueChangedCallback(e =>
            {
                if (string.IsNullOrEmpty(e.newValue))
                    EditorPrefs.DeleteKey(prefKey);
                else
                    EditorPrefs.SetString(prefKey, e.newValue);
            });
            parent.Add(pathField);
        }

        internal static void BuildGeminiForm(
            VisualElement parent,
            GeminiBackendConfig config,
            Action onSave)
        {
            BuildBinarySection(parent, "gemini",
                ChatBinaryResolver.GeminiPrefKey,
                "Install: npm install -g @google/gemini-cli");

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

        internal static void BuildKimiForm(
            VisualElement parent,
            KimiBackendConfig config,
            Action onSave)
        {
            BuildBinarySection(parent, "kimi",
                ChatBinaryResolver.KimiPrefKey,
                "Install: curl -fsSL https://code.kimi.com/kimi-code/install.sh | bash");

            var modelField = new TextField("Model") { value = config.Model };
            modelField.RegisterValueChangedCallback(e => { config.Model = e.newValue; onSave(); });
            parent.Add(modelField);

            var approvalField = new DropdownField(
                "Approval Mode",
                new List<string> { "default", "yolo", "plan" },
                config.ApprovalMode == "yolo" ? 1 : config.ApprovalMode == "plan" ? 2 : 0);
            approvalField.RegisterValueChangedCallback(e =>
            {
                config.ApprovalMode = e.newValue == "default" ? "" : e.newValue;
                onSave();
            });
            parent.Add(approvalField);

            var extraField = new TextField("Extra Args") { value = config.ExtraArgs };
            extraField.RegisterValueChangedCallback(e => { config.ExtraArgs = e.newValue; onSave(); });
            parent.Add(extraField);
        }

        internal static void BuildOpenCodeForm(
            VisualElement parent,
            OpenCodeBackendConfig config,
            Action onSave)
        {
            BuildBinarySection(parent, "opencode",
                ChatBinaryResolver.OpenCodePrefKey,
                "Install: curl -fsSL https://opencode.sh | bash");

            var fmtHint = new Label("Model: provider/modelId  e.g. anthropic/claude-sonnet-4");
            fmtHint.style.fontSize   = 9;
            fmtHint.style.color      = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            fmtHint.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(fmtHint);

            var modelField = new TextField("Model") { value = config.Model };
            modelField.RegisterValueChangedCallback(e => { config.Model = e.newValue; onSave(); });
            parent.Add(modelField);

            var skipToggle = new Toggle("Skip Permissions") { value = config.SkipPermissions };
            skipToggle.RegisterValueChangedCallback(e => { config.SkipPermissions = e.newValue; onSave(); });
            parent.Add(skipToggle);

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
