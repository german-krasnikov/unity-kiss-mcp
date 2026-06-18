// ChipPillFactory: shared static pill builder for input field and response rendering.
// All display/color routed through ChipKindRegistry — zero hardcoded per-kind logic.
// P4: ColorResolver seam allows per-kind color overrides from BackendConfigStore.
// F14b: AddToContextAction seam for right-click "Add to context" menu.
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
    public static class ChipPillFactory
    {
        /// <summary>
        /// Seam: when non-null, overrides provider.HexColor. Set from config on window open.
        /// Set to null in tests TearDown to prevent leakage.
        /// </summary>
        internal static Func<string, string> ColorResolver;

        /// <summary>
        /// Seam: when non-null, invoked with ChipData when user selects "Add to context".
        /// Set in MCPChatWindow.OnEnable; cleared in OnDisable.
        /// </summary>
        internal static Action<ChipData> AddToContextAction;

        /// <summary>
        /// Attach right-click menu to a pill.
        /// Items: per-kind items first, then "Add to context", then "Show Preview" (if onPreview set).
        /// </summary>
        internal static void AttachContextMenu(VisualElement pill, ChipData chip,
            Action onPreview = null, Action onNavigate = null)
        {
            pill.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                ChipKindRegistry.ForKey(chip.KindKey)
                    ?.AppendContextMenuItems(evt.menu, chip.Path);
                evt.menu.AppendAction("Add to context", _ =>
                    AddToContextAction?.Invoke(chip));
                if (onPreview != null)
                    evt.menu.AppendAction("Show Preview", _ => onPreview());
            }));
        }

        /// <summary>
        /// Attach full read-only behaviour to a pill: left-click = navigate, right-click = Navigate +
        /// "Add to context" + per-kind items. Use for ALL pills in user and assistant bubbles.
        /// Does not include a preview panel — that's assistant-only via MixedParagraphRenderer.BuildPill.
        /// </summary>
        internal static void AttachReadOnlyBehavior(VisualElement pill, ChipData chip)
        {
            Action navigateAction = () => ChipKindRegistry.ForKey(chip.KindKey)?.Navigate(chip.Path);
            ChipClickRouter.Register(pill, previewPanel: null, navigateAction);
            AttachContextMenu(pill, chip, onPreview: null, onNavigate: navigateAction);
        }

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

            ApplyColor(pill, kindKey, provider);

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

        // ── internal helpers ──────────────────────────────────────────────────

        /// <summary>Try to parse a hex color string. Returns false and Color.gray on failure.</summary>
        internal static bool TryParseHex(string hex, out Color col)
        {
            col = Color.gray;
            if (string.IsNullOrEmpty(hex) || hex[0] != '#') return false;
            return ColorUtility.TryParseHtmlString(hex, out col);
        }

        // ── private ───────────────────────────────────────────────────────────

        private static void ApplyColor(VisualElement pill, string kindKey, IChipKindProvider provider)
        {
            // Resolution: ColorResolver → provider.HexColor → gray fallback
            var hex = ColorResolver?.Invoke(kindKey) ?? provider?.HexColor ?? "#94a3b8";
            if (!TryParseHex(hex, out var col)) col = new Color(0.58f, 0.64f, 0.72f); // #94a3b8
            col.a = 0.85f;
            pill.style.backgroundColor = col;
        }
    }
}
