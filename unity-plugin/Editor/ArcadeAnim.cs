using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>Shared arcade animation primitives using USS class toggles.</summary>
    internal static class ArcadeAnim
    {
        // ── Generic Class Toggle ──────────────────────────────────────────────

        /// <summary>Adds <paramref name="hiddenClass"/>, then after <paramref name="delayMs"/> swaps to <paramref name="visibleClass"/>.</summary>
        public static void AnimateClass(VisualElement el, string hiddenClass, string visibleClass, int delayMs = 0)
        {
            el.AddToClassList(hiddenClass);
            el.schedule.Execute(() =>
            {
                el.RemoveFromClassList(hiddenClass);
                el.AddToClassList(visibleClass);
            }).StartingIn(delayMs);
        }

        // ── Fade ─────────────────────────────────────────────────────────────

        public static void FadeIn(VisualElement el, int delayMs = 0) =>
            AnimateClass(el, "arcade-fade-hidden", "arcade-fade-visible", delayMs);

        // ── Slide ─────────────────────────────────────────────────────────────

        public static void SlideInRight(VisualElement el, int delayMs = 0) =>
            AnimateClass(el, "arcade-slide-hidden", "arcade-slide-visible", delayMs);

        // ── Shake ─────────────────────────────────────────────────────────────

        public static void ShakeX(VisualElement el)
        {
            el.RemoveFromClassList("arcade-shake");
            el.schedule.Execute(() => el.AddToClassList("arcade-shake")).StartingIn(0);
            el.schedule.Execute(() => el.RemoveFromClassList("arcade-shake")).StartingIn(300);
        }

        // ── Pulse ─────────────────────────────────────────────────────────────

        public static void PulseOnce(VisualElement el)
        {
            el.AddToClassList("arcade-pulse");
            el.schedule.Execute(() => el.RemoveFromClassList("arcade-pulse")).StartingIn(400);
        }

        // ── Flash ─────────────────────────────────────────────────────────────

        public static void FlashClass(VisualElement el, string cls, int ms)
        {
            el.AddToClassList(cls);
            el.schedule.Execute(() => el.RemoveFromClassList(cls)).StartingIn(ms);
        }

        // ── Glow Pulse ────────────────────────────────────────────────────────

        /// <summary>Toggles "arcade-glow" and state class every <paramref name="intervalMs"/> ms.</summary>
        public static void GlowPulse(VisualElement el, string stateKey, int intervalMs = 900)
        {
            el.AddToClassList("arcade-glow");
            el.AddToClassList(StateClassFor(stateKey));

            bool on = true;
            el.schedule.Execute(() =>
            {
                el.EnableInClassList("arcade-glow", on);
                on = !on;
            }).Every(intervalMs);
        }

        // ── Count Up ──────────────────────────────────────────────────────────

        /// <summary>Animates label text from <paramref name="from"/> to <paramref name="to"/> over <paramref name="durationMs"/>.</summary>
        public static void CountUp(Label el, int from, int to, int durationMs = 600)
        {
            el.text = from.ToString();
            int steps = System.Math.Abs(to - from);
            if (steps == 0) return;

            int stepMs = durationMs / steps;
            int current = from;
            el.schedule.Execute(() =>
            {
                current += current < to ? 1 : -1;
                el.text = current.ToString();
            }).Every(stepMs).Until(() => current == to);
        }

        // ── Stagger Fade ──────────────────────────────────────────────────────

        /// <summary>Fades in each element with staggered delay.</summary>
        public static void StaggerFadeIn(IList<VisualElement> els, int stepMs = 80)
        {
            for (int i = 0; i < els.Count; i++)
                FadeIn(els[i], i * stepMs);
        }

        // ── Typewriter ────────────────────────────────────────────────────────

        /// <summary>Reveals <paramref name="text"/> character by character.</summary>
        public static void Typewriter(Label el, string text, int msPerChar = 35)
        {
            el.text = "";
            int idx = 0;
            el.schedule.Execute(() =>
            {
                idx++;
                el.text = text[..idx];
            }).Every(msPerChar).Until(() => idx >= text.Length);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string StateClassFor(string stateKey) => stateKey switch
        {
            "up"     => "conn-up",
            "listen" => "conn-listen",
            _        => "conn-down"
        };
    }
}
