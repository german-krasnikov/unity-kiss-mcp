using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class SettingsPageFactory
    {
        internal static VisualElement BuildToolsPage(Action onBack)
        {
            var page = new VisualElement();
            page.AddToClassList("nav-page");
            page.Add(BackHeader("Tools", onBack));
            page.Add(ToolsHeaderAnim.Build(page));
            var scroll = new ScrollView();
            scroll.AddToClassList("tool-scroll");
            MCPSettingsUI.BuildToolsSection(scroll);
            page.Add(scroll);
            return page;
        }

        internal static VisualElement BuildPermissionsPage(Action onBack)
        {
            var page = new VisualElement();
            page.AddToClassList("nav-page");
            page.Add(BackHeader("Permissions", onBack));
            page.Add(PermissionsHeaderAnim.Build(page));
            var info = new Label("Controls which MCP tools the in-Unity Chat agent may call. " +
                                 "Tool toggles are in the Settings hub.");
            info.AddToClassList("info-label");
            info.style.whiteSpace = WhiteSpace.Normal;
            info.style.marginBottom = 8;
            page.Add(info);
            page.Add(MCPSettingsPermUI.BuildContent(new PermissionConfig()));
            return page;
        }

        internal static VisualElement BuildChatPage(Action onBack)
        {
            var page = new VisualElement();
            page.AddToClassList("nav-page");
            page.Add(BackHeader("Chat Settings", onBack));
            page.Add(ChatHeaderAnim.Build(page));
            if (ChatSettingsHook.HasConnectionSubscribers)
            {
                ChatSettingsHook.InvokeConnection(page);
            }
            else
            {
                var msg = new Label("Agent Chat is disabled.\nEnable it in MCP/Settings.");
                msg.style.whiteSpace = WhiteSpace.Normal;
                msg.style.marginBottom = 8;
                page.Add(msg);
                page.Add(new Button(() => ChatSettingsHook.SetChatEnabled(true)) { text = "Enable Agent Chat" });
            }
            return page;
        }

        internal static VisualElement BuildSamplingPage(Action onBack)
        {
            var page = new VisualElement();
            page.AddToClassList("nav-page");
            page.Add(BackHeader("LLM Sampling", onBack));
            var store = LlmConfigStore.Load();
            page.Add(BuildSamplingForm(store));
            return page;
        }

        private static VisualElement BackHeader(string title, Action onBack)
        {
            var header = new VisualElement();
            header.AddToClassList("nav-back-header");
            var btn = new Button(onBack) { text = "← Back" };
            btn.AddToClassList("nav-back-btn");
            var lbl = new Label(title);
            lbl.AddToClassList("nav-back-title");
            header.Add(btn);
            header.Add(lbl);
            return header;
        }

        private static VisualElement BuildSamplingForm(LlmConfigStore store)
        {
            var scroll = new ScrollView();
            var cfg = store.Config;

            var presetRow = new VisualElement();
            presetRow.AddToClassList("preset-row");

            var modelFields = new Dictionary<string, TextField>();
            modelFields["visual_verify"]       = AddSamplingRow(scroll, "Visual Verify",       "visual_verify",       cfg.VisualVerify,       store, "screenshot");
            modelFields["screenshot_describe"] = AddSamplingRow(scroll, "Screenshot Describe", "screenshot_describe", cfg.ScreenshotDescribe, store, "screenshot");
            modelFields["visual_diff"]         = AddSamplingRow(scroll, "Visual Diff",         "visual_diff",         cfg.VisualDiff,         store, "screenshot");
            modelFields["summarize"]           = AddSamplingRow(scroll, "Summarize",           "summarize",           cfg.Summarize,          store);
            modelFields["do_intent"]           = AddSamplingRow(scroll, "Do Intent",           "do_intent",           cfg.DoIntent,           store);
            modelFields["distiller"]           = AddSamplingRow(scroll, "Distiller",           "distiller",           cfg.Distiller,          store);

            foreach (var kv in SamplingPresets.All)
            {
                var preset = kv.Value;
                var btn = new Button(() =>
                {
                    foreach (var p in preset)
                        if (modelFields.TryGetValue(p.Key, out var f))
                            f.value = p.Value;
                }) { text = kv.Key };
                btn.AddToClassList("preset-btn");
                presetRow.Add(btn);
            }

            scroll.Insert(0, presetRow);
            return scroll;
        }

        private static TextField AddSamplingRow(VisualElement parent, string label, string featureKey,
            SamplingConfig cfg, LlmConfigStore store, string toolDep = null)
        {
            var row = new VisualElement();
            row.AddToClassList("sampling-row");

            if (toolDep != null && !MCPSettings.IsToolEnabled(toolDep))
                row.style.display = DisplayStyle.None;

            var lbl2 = new Label(label);
            lbl2.AddToClassList("sampling-label");
            row.Add(lbl2);

            var modelField = new TextField("Model") { value = cfg.Model };
            modelField.RegisterValueChangedCallback(e => { cfg.Model = e.newValue; store.Save(); });
            row.Add(modelField);

            var maxTurns = new IntegerField("Max Turns") { value = cfg.MaxTurns };
            maxTurns.RegisterValueChangedCallback(e => { cfg.MaxTurns = e.newValue; store.Save(); });
            row.Add(maxTurns);

            var timeout = new FloatField("Timeout") { value = cfg.Timeout };
            timeout.RegisterValueChangedCallback(e => { cfg.Timeout = e.newValue; store.Save(); });
            row.Add(timeout);

            parent.Add(row);
            return modelField;
        }
    }
}
