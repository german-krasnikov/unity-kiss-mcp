using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>Renders a dismissible update-available banner.</summary>
    internal static class UpdateBanner
    {
        public static VisualElement Build()
        {
            if (!UpdateChecker.HasUpdate) return null;

            var banner = new VisualElement();
            banner.AddToClassList("wiz-card");
            banner.style.borderLeftColor = new Color(0.23f, 0.82f, 0.62f);
            banner.style.borderLeftWidth = 3;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems    = Align.Center;

            var text = new Label($"Update available: v{UpdateChecker.AvailableVersion}");
            text.style.flexGrow = 1;
            row.Add(text);

            row.Add(new Button(DoUpdate) { text = "Update" });

            row.Add(new Button(() =>
            {
                UpdateChecker.SkipVersion();
                banner.RemoveFromHierarchy();
            }) { text = "Skip" });

            banner.Add(row);

            banner.AddToClassList("wiz-fade-hidden");
            banner.schedule.Execute(() =>
            {
                banner.RemoveFromClassList("wiz-fade-hidden");
                banner.AddToClassList("wiz-fade-visible");
            }).StartingIn(300);

            return banner;
        }

        static void DoUpdate() => UpdateDispatcher.DoUpdate();
    }
}
