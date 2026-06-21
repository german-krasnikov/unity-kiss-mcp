using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class UpdatesPage
    {
        internal static VisualElement Build(Action onBack)
        {
            var page = new VisualElement();
            page.AddToClassList("nav-page");
            page.Add(SettingsPageFactory.BackHeader("Updates", onBack));

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            var bannerSlot = new VisualElement();
            bannerSlot.AddToClassList("updates-banner-slot");
            var levelUp = LevelUpPanel.Build(scheduleHost: scroll);
            if (levelUp != null) bannerSlot.Add(levelUp);
            scroll.Add(bannerSlot);

            var checkBtn = new Button() { text = "Check for Updates" };
            checkBtn.AddToClassList("updates-check-btn");
            checkBtn.clicked += () =>
            {
                checkBtn.SetEnabled(false);
                checkBtn.text = "Checking...";
                UpdateChecker.ForceCheckAsync();
                checkBtn.schedule.Execute(() =>
                {
                    checkBtn.SetEnabled(true);
                    checkBtn.text = "Check for Updates";
                    bannerSlot.Clear();
                    var newLevelUp = LevelUpPanel.Build(scheduleHost: scroll);
                    if (newLevelUp != null) bannerSlot.Add(newLevelUp);
                }).StartingIn(3000);
            };
            scroll.Add(checkBtn);

            var changelogArea = new VisualElement();
            changelogArea.AddToClassList("updates-changelog");
            BuildChangelogEntries(changelogArea);
            scroll.Add(changelogArea);

            page.Add(scroll);
            return page;
        }

        private static void BuildChangelogEntries(VisualElement parent)
        {
            var path = ChangelogReader.LocatePath();
            if (path == null) { parent.Add(new Label("Changelog not found.")); return; }

            string content;
            try { content = File.ReadAllText(path); }
            catch { parent.Add(new Label("Could not read changelog.")); return; }

            var current = UpdateChecker.GetCurrentVersion();
            var entries = ChangelogReader.Parse(content, current);

            var foldouts = new List<VisualElement>();
            foreach (var entry in entries)
            {
                var header = string.IsNullOrEmpty(entry.Date)
                    ? entry.Version
                    : $"{entry.Version} — {entry.Date}";
                var foldout = new Foldout { text = header, value = entry.IsNewer };
                if (entry.IsNewer) foldout.AddToClassList("updates-entry-newer");

                var body = new Label(MarkdownInlineFormatter.ToRichText(entry.Content));
                body.enableRichText = true;
                body.AddToClassList("updates-entry-body");
                body.style.whiteSpace = WhiteSpace.Normal;
                foldout.Add(body);
                parent.Add(foldout);
                foldouts.Add(foldout);
            }
            ArcadeAnim.StaggerFadeIn(foldouts, 60);
        }
    }
}
