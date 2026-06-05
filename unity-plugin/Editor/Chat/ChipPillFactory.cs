// ChipPillFactory: shared static pill builder for input field and response rendering.
// All display/color routed through ChipKindRegistry — zero hardcoded per-kind logic.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Builds a pill VisualElement for a chip.
    /// onRemove != null → input mode: includes a ✕ button.
    /// onRemove == null → response mode: no remove button.
    /// All styling sourced from ChipKindRegistry.ForKey — no switch/if on KindKey.
    /// </summary>
    internal static class ChipPillFactory
    {
        /// <summary>Build from explicit kindKey and display name.</summary>
        internal static VisualElement Build(string kindKey, string displayName, Action onRemove = null)
        {
            var provider = ChipKindRegistry.ForKey(kindKey);

            var pill = new VisualElement();
            pill.AddToClassList("inline-chip-pill");
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems    = Align.Center;
            pill.style.paddingLeft   = pill.style.paddingRight  = 4f;
            pill.style.marginRight   = 2f;
            pill.style.borderTopLeftRadius     = pill.style.borderTopRightRadius     = 4f;
            pill.style.borderBottomLeftRadius  = pill.style.borderBottomRightRadius  = 4f;
            pill.pickingMode = PickingMode.Position;

            ApplyColor(pill, provider);

            var kindLbl = new Label(kindKey + ":");
            kindLbl.AddToClassList("inline-chip-kind");
            kindLbl.style.fontSize = 9f;

            var nameLbl = new Label(displayName);
            nameLbl.AddToClassList("inline-chip-label");
            nameLbl.style.fontSize = 10f;

            pill.Add(kindLbl);
            pill.Add(nameLbl);

            if (onRemove != null)
            {
                var btn = new Button(onRemove) { text = "✕" };
                btn.AddToClassList("inline-chip-remove");
                btn.style.fontSize    = 9f;
                btn.style.marginLeft  = 2f;
                btn.style.paddingLeft = btn.style.paddingRight  = 2f;
                btn.style.paddingTop  = btn.style.paddingBottom = 0f;
                pill.Add(btn);
            }

            return pill;
        }

        /// <summary>Build from a ChipData struct (uses DisplayName as label).</summary>
        internal static VisualElement Build(ChipData chip, Action onRemove = null)
            => Build(chip.KindKey, chip.DisplayName, onRemove);

        // ── private ───────────────────────────────────────────────────────────

        private static void ApplyColor(VisualElement pill, IChipKindProvider provider)
        {
            if (provider == null) return;
            if (!TryParseHex(provider.HexColor, out var col)) return;
            col.a = 0.85f;
            pill.style.backgroundColor = col;
        }

        private static bool TryParseHex(string hex, out Color col)
        {
            col = Color.gray;
            if (string.IsNullOrEmpty(hex) || hex[0] != '#') return false;
            return ColorUtility.TryParseHtmlString(hex, out col);
        }
    }
}
