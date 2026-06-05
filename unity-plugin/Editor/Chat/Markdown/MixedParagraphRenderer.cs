// Renders a paragraph containing [kind:ref] tags as a flex-wrap container.
// Text runs -> Labels via MarkdownInline.ToRichText.
// Tag runs -> ChipPillFactory pills (response mode: no remove button, click-to-navigate).
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class MixedParagraphRenderer
    {
        /// <summary>
        /// Render a paragraph with mixed text+tag content.
        /// Returns a flex-row/wrap VisualElement container marked with md-para--mixed;
        /// caller adds the contextual class (md-para / md-list-content).
        /// </summary>
        internal static VisualElement Render(string rawText)
        {
            var container = new VisualElement();
            container.AddToClassList("md-para--mixed");
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexWrap      = Wrap.Wrap;
            container.style.alignItems    = Align.Center;

            var segments = ResponseTagInliner.Split(rawText);
            foreach (var seg in segments)
            {
                if (!seg.IsTag)
                {
                    var lbl = ChatLabel.Selectable(MarkdownInline.ToRichText(seg.Text), richText: true);
                    container.Add(lbl);
                }
                else
                {
                    container.Add(BuildPill(seg.KindKey, seg.Text));
                }
            }
            return container;
        }

        /// <summary>
        /// Returns either a mixed-pill container or a plain selectable label, then adds cssClass.
        /// Used by both RenderParagraph and RenderList so the HasTags branch isn't duplicated.
        /// </summary>
        internal static VisualElement InlineElement(string text, string cssClass)
        {
            VisualElement ve = ResponseTagInliner.HasTags(text)
                ? Render(text)
                : ChatLabel.Selectable(MarkdownInline.ToRichText(text), richText: true);
            ve.AddToClassList(cssClass);
            return ve;
        }

        // ── private ───────────────────────────────────────────────────────────

        private static VisualElement BuildPill(string kindKey, string rawRef)
        {
            var chip = RefParser.Parse(kindKey, rawRef);
            var pill = ChipPillFactory.Build(chip.KindKey, chip.DisplayName);
            pill.tooltip = rawRef; // full ref for "reveal"

            var capturedRef = rawRef;
            var capturedKey = kindKey;
            pill.RegisterCallback<ClickEvent>(_ =>
            {
                var provider = ChipKindRegistry.ForKey(capturedKey);
                provider?.Navigate(capturedRef);
            });

            ChipPillFactory.AttachAddToContextMenu(pill, chip);
            return pill;
        }
    }
}
