// Wave 3 — Positioning probe (wave0_resolved). ActivePath: "public"|"none".
// "public" = ITextSelection.GetCursorPositionFromStringIndex (preferred, 6000.3.0b7 confirmed).
// "none" = H10 row-layout degradation.
// Forward positioning is public-only by design per f11_wave0_resolved.md.
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class UitkCharRect
    {
        // ── Public surface ────────────────────────────────────────────────────

        internal static bool   IsAvailable { get; private set; }
        internal static string ActivePath  { get; private set; } = "none";

        // ── Static ctor — fires at domain-reload before any window opens ──────

        static UitkCharRect()
        {
            try { ProbeApi(); }
            catch { ActivePath = "none"; IsAvailable = false; }
        }

        // ── Entry: re-callable from tests ────────────────────────────────────

        internal static string ProbeApi()
        {
            string path = Detect();
            ActivePath  = path;
            IsAvailable = path == "public";
            Debug.Log($"[UitkCharRect] ActivePath={path}  IsAvailable={IsAvailable}");
            return path;
        }

        // ── Detection ─────────────────────────────────────────────────────────

        private static string Detect()
        {
            if (HasPublicPath()) return "public";
            return "none";
        }

        // Public path: TextField.textSelection (ITextSelection) has
        // GetCursorPositionFromStringIndex(int stringIndex) -> Vector2.
        private static bool HasPublicPath()
        {
            var prop = typeof(TextField).GetProperty(
                "textSelection",
                BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return false;

            Type iface = prop.PropertyType;
            foreach (var m in iface.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.Name != "GetCursorPositionFromStringIndex") continue;
                var parms = m.GetParameters();
                if (parms.Length >= 1 && parms[0].ParameterType == typeof(int))
                    return true;
            }
            return false;
        }

        // Reverse drag hit-test (pixel->string index) will need TextElement.uitkTextHandle.GetCursorIndexFromPosition via reflection — re-add when drag-to-caret lands. See git history / f11_wave0_resolved.

        // ── TryGetCharRect — public path (preferred) ──────────────────────────

        /// <summary>
        /// Get the content-relative rect for the character at <paramref name="charIndex"/>
        /// using the public ITextSelection path. Returns false when unavailable or on error.
        /// H10: always gate call sites with UitkCharRect.IsAvailable first.
        /// </summary>
        internal static bool TryGetCharRect(TextField field, int charIndex, out Rect rect)
        {
            rect = Rect.zero;
            if (field == null || !IsAvailable) return false;

            try
            {
                var sel = field.textSelection;
                if (sel == null) return false;

                // Get caret pixel pos at charIndex via public API (no reflection)
                var pos = sel.GetCursorPositionFromStringIndex(charIndex);

                // lineHeightAtCursorPosition is an inaccessible explicit-impl on this runtime;
                // derive row height from font size instead.
                float lineH = field.resolvedStyle.fontSize > 0
                    ? field.resolvedStyle.fontSize * 1.2f
                    : 14f;

                rect = new Rect(pos.x, pos.y, 0f, lineH);
                return float.IsFinite(pos.x) && float.IsFinite(pos.y);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UitkCharRect] TryGetCharRect failed: {ex.Message}");
                IsAvailable = false;
                return false;
            }
        }

        // ── MeasureNbspAdvance — H13 ──────────────────────────────────────────

        /// <summary>
        /// Measure the pixel advance of one U+00A0 (NBSP) in <paramref name="field"/>.
        /// H13: call after first GeometryChangedEvent. Returns 0 on failure; caller uses
        /// default N=4 until a real measurement is available.
        /// </summary>
        internal static float MeasureNbspAdvance(TextField field)
        {
            if (field == null) return 0f;
            try
            {
                // MeasureTextSize is public on TextElement (inherited by TextField)
                var size = field.MeasureTextSize(
                    NbspReservation.NBSP,
                    0, VisualElement.MeasureMode.Undefined,
                    0, VisualElement.MeasureMode.Undefined);
                return size.x;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UitkCharRect] MeasureNbspAdvance failed: {ex.Message}");
                return 0f;
            }
        }
    }
}
