using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class ChatSettingsSection
    {
        /// <summary>Builds connection settings content — called by ChatConnectionSection via OnBuildConnection.</summary>
        internal static void BuildContent(VisualElement parent)
        {
            // F22: Auto-scroll toggle — at the top, outside any foldout.
            var autoScrollToggle = new Toggle("Auto-scroll") { value = EditorPrefs.GetBool("MCPChat.AutoScroll", true) };
            autoScrollToggle.RegisterValueChangedCallback(evt => EditorPrefs.SetBool("MCPChat.AutoScroll", evt.newValue));
            parent.Add(autoScrollToggle);

            // Per-backend settings — Claude foldout is expanded by default (contains primary connection info)
            var store = BackendConfigStore.Load();
            var claudeFoldout = new Foldout { text = "Claude Settings", value = true };

            // Binary path (auto + override) — inside Claude foldout
            var autoPath = ChatBinaryResolver.Resolve();
            var pathHint = new Label($"Auto: {autoPath ?? "not found"}");
            pathHint.style.fontSize = 10;
            pathHint.style.color    = new StyleColor(autoPath != null ? new Color(0.5f, 0.8f, 0.5f) : new Color(0.8f, 0.4f, 0.4f));
            claudeFoldout.Add(pathHint);

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
            claudeFoldout.Add(pathField);

            // Auth status probe
            var authLabel = new Label("Auth: checking...");
            authLabel.style.fontSize = 10;
            claudeFoldout.Add(authLabel);
            ProbeAuthAsync(authLabel);

            // ANTHROPIC_API_KEY warning
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
            {
                var warn = new Label("Warning: ANTHROPIC_API_KEY is set — it will be stripped from the chat process to use subscription auth.");
                warn.style.fontSize = 9;
                warn.style.color    = new StyleColor(new Color(0.9f, 0.7f, 0.3f));
                warn.style.whiteSpace = WhiteSpace.Normal;
                claudeFoldout.Add(warn);
            }

            BackendSettingsForm.BuildClaudeForm(claudeFoldout, store.Claude, () => store.Save());
            parent.Add(claudeFoldout);

            var codexFoldout = new Foldout { text = "Codex Settings", value = false };
            BackendSettingsForm.BuildCodexForm(codexFoldout, store.Codex, () => store.Save());
            parent.Add(codexFoldout);

            // Context Chips — per-kind depth + color overrides
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
            parent.Add(chipFoldout);
        }

        private static void ProbeAuthAsync(Label label)
        {
            var binary = ChatBinaryResolver.Resolve();
            if (binary == null) { label.text = "Auth: binary not found"; return; }

            // Build PSI on main thread (Unity APIs like SystemInfo require it)
            ProcessStartInfo psi;
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                psi = new ProcessStartInfo(binary, "auth status")
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };
            }
            else
            {
                psi = LoginShellCommand.Create("\"$1\" auth status", binary);
                psi.RedirectStandardError = true;
            }

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                bool ok = false;
                try
                {
                    using var p = Process.Start(psi);
                    if (p != null)
                    {
                        p.BeginErrorReadLine();
                        p.StandardOutput.ReadToEnd();
                        if (!p.WaitForExit(2000)) { try { p.Kill(); } catch { } }
                    }
                    ok = p != null && p.HasExited && p.ExitCode == 0;
                }
                catch { }
                EditorApplication.delayCall += () =>
                {
                    if (label?.panel == null) return;
                    label.text = ok ? "Auth: logged in" : "Auth: not logged in";
                    label.style.color = new StyleColor(ok ? new Color(0.5f, 0.8f, 0.5f) : new Color(0.8f, 0.4f, 0.4f));
                    EditorPrefs.SetString("UnityMCP_Chat_AuthStatus", ok ? "ok" : "fail");
                };
            });
        }
    }
}
