using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard
{
    /// <summary>Shared animation primitives using USS class toggles.</summary>
    public static class WizardAnimUtils
    {
        // ── Fade ─────────────────────────────────────────────────────────────

        public static void FadeIn(VisualElement el, int delayMs = 0)
        {
            el.AddToClassList("wiz-fade-hidden");
            el.schedule.Execute(() =>
            {
                el.RemoveFromClassList("wiz-fade-hidden");
                el.AddToClassList("wiz-fade-visible");
            }).StartingIn(delayMs);
        }

        // ── Slide ─────────────────────────────────────────────────────────────

        public static void SlideInRight(VisualElement el, int delayMs = 0)
        {
            el.AddToClassList("wiz-slide-hidden");
            el.schedule.Execute(() =>
            {
                el.RemoveFromClassList("wiz-slide-hidden");
                el.AddToClassList("wiz-slide-visible");
            }).StartingIn(delayMs);
        }

        // ── Shake ─────────────────────────────────────────────────────────────

        /// <summary>Horizontal shake for error feedback.</summary>
        public static void ShakeX(VisualElement el)
        {
            el.RemoveFromClassList("wiz-shake");
            el.schedule.Execute(() => el.AddToClassList("wiz-shake")).StartingIn(0);
            el.schedule.Execute(() => el.RemoveFromClassList("wiz-shake")).StartingIn(300);
        }

        // ── Pulse ─────────────────────────────────────────────────────────────

        /// <summary>Single pulse using border-width expansion (proven Unity 6 pattern).</summary>
        public static void PulseOnce(VisualElement el)
        {
            el.AddToClassList("wiz-pulse");
            el.schedule.Execute(() => el.RemoveFromClassList("wiz-pulse")).StartingIn(400);
        }

        // ── Flash ─────────────────────────────────────────────────────────────

        /// <summary>Adds a USS class for <paramref name="ms"/> milliseconds, then removes it.</summary>
        public static void FlashClass(VisualElement el, string cls, int ms)
        {
            el.AddToClassList(cls);
            el.schedule.Execute(() => el.RemoveFromClassList(cls)).StartingIn(ms);
        }
    }
}
