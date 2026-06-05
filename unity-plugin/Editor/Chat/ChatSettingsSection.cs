// Injects the "Agent Chat" section into MCPSettings via ChatSettingsHook (event, no reverse dep).
// The enable toggle + UNITY_MCP_CHAT define live in core ChatSettingsHook (always compiled);
// this section adds the details visible only once the module is ON (path, auth, backend).
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal static class ChatSettingsSection
    {
        static ChatSettingsSection()
        {
            ChatSettingsHook.OnBuild += AppendSection;
        }

        private static void AppendSection(VisualElement root)
        {
            var foldout = new Foldout { text = "Agent Chat", value = false };  // F8: removed "(Beta)"
            foldout.style.marginTop = 8;

            // Binary path (auto + override)
            var autoPath = ChatBinaryResolver.Resolve();
            var pathHint = new Label($"Auto: {autoPath ?? "not found"}");
            pathHint.style.fontSize = 10;
            pathHint.style.color    = new StyleColor(autoPath != null ? new Color(0.5f, 0.8f, 0.5f) : new Color(0.8f, 0.4f, 0.4f));
            foldout.Add(pathHint);

            var pathField = new TextField("Override Path")
                { value = EditorPrefs.GetString(ChatBinaryResolver.PrefKey, "") };
            pathField.RegisterValueChangedCallback(e =>
            {
                if (string.IsNullOrEmpty(e.newValue))
                    EditorPrefs.DeleteKey(ChatBinaryResolver.PrefKey);
                else
                    EditorPrefs.SetString(ChatBinaryResolver.PrefKey, e.newValue);
                ChatBinaryResolver.Resolve(forceRefresh: true);
            });
            foldout.Add(pathField);

            // Auth status probe
            var authLabel = new Label("Auth: checking...");
            authLabel.style.fontSize = 10;
            foldout.Add(authLabel);
            ProbeAuthAsync(authLabel);

            // ANTHROPIC_API_KEY warning
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
            {
                var warn = new Label("Warning: ANTHROPIC_API_KEY is set — it will be stripped from the chat process to use subscription auth.");
                warn.style.fontSize = 9;
                warn.style.color    = new StyleColor(new Color(0.9f, 0.7f, 0.3f));
                warn.style.whiteSpace = WhiteSpace.Normal;
                foldout.Add(warn);
            }

            // Per-backend settings — F9
            var store = BackendConfigStore.Load();

            var claudeFoldout = new Foldout { text = "Claude Settings", value = false };
            BackendSettingsForm.BuildClaudeForm(claudeFoldout, store.Claude, () => store.Save());
            foldout.Add(claudeFoldout);

            var codexFoldout = new Foldout { text = "Codex Settings", value = false };
            BackendSettingsForm.BuildCodexForm(codexFoldout, store.Codex, () => store.Save());
            foldout.Add(codexFoldout);

            // P4: Context Chips — per-kind depth + color overrides; refresh all open chat windows live.
            var chipFoldout = new Foldout { text = "Context Chips", value = false };
            BackendSettingsForm.BuildChipDisplayForm(chipFoldout, store.Chips, () =>
            {
                store.Save();
                foreach (var w in Resources.FindObjectsOfTypeAll<MCPChatWindow>())
                {
                    w.RefreshColorResolver();
                    w.RefreshChipDisplay();
                }
            });
            foldout.Add(chipFoldout);

            root.Add(foldout);
        }

        private static void ProbeAuthAsync(Label label)
        {
            var binary = ChatBinaryResolver.Resolve();
            if (binary == null) { label.text = "Auth: binary not found"; return; }
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                bool ok = false;
                try
                {
                    var psi = LoginShellCommand.Create("\"$1\" auth status", binary);
                    using var p = Process.Start(psi);
                    p?.StandardOutput.ReadToEnd();
                    if (p != null && !p.WaitForExit(2000)) { try { p.Kill(); } catch { } }
                    ok = p != null && p.HasExited && p.ExitCode == 0;
                }
                catch { }
                // Marshal to main thread; guard against label destroyed (Settings window closed).
                EditorApplication.delayCall += () =>
                {
                    if (label?.panel == null) return;
                    label.text = ok ? "Auth: logged in" : "Auth: not logged in";
                    label.style.color = new StyleColor(ok ? new Color(0.5f, 0.8f, 0.5f) : new Color(0.8f, 0.4f, 0.4f));
                };
            });
        }
    }
}
