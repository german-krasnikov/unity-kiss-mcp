// Renders a paragraph containing [kind:ref] tags as a flex-wrap container.
// Text runs -> Labels via MarkdownInline.ToRichText. Image path tokens -> thumbnails.
// Tag runs -> ChipPillFactory pills (response mode: no remove button, click-to-navigate).
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public static class MixedParagraphRenderer
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
                    var lines = seg.Text.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (i > 0)
                        {
                            var br = new VisualElement();
                            br.style.flexBasis = new StyleLength(Length.Percent(100));
                            br.style.height = 0;
                            container.Add(br);
                        }
                        var stripped = StripOrphanBold(lines[i]);
                        foreach (var ve in SplitLineWithThumbs(stripped))
                            container.Add(ve);
                    }
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

        /// <summary>Strip orphan leading/trailing ** from text segments adjacent to pills.</summary>
        internal static string StripOrphanBold(string text)
        {
            var t = text.Trim();
            bool startsDouble = t.StartsWith("**");
            bool endsDouble   = t.EndsWith("**") && t.Length >= 4;
            if (startsDouble && !endsDouble) t = t.Substring(2).TrimStart();
            if (endsDouble   && !startsDouble) t = t.Substring(0, t.Length - 2).TrimEnd();
            return t;
        }

        /// <summary>
        /// Splits a line on whitespace. Image-path tokens become InlineImageThumbnail elements;
        /// remaining tokens are joined into a single Label each run. Returns empty if line is empty.
        /// </summary>
        internal static IEnumerable<VisualElement> SplitLineWithThumbs(string line)
        {
            if (string.IsNullOrEmpty(line)) yield break;

            var tokens = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            var textRun = new List<string>();

            foreach (var token in tokens)
            {
                if (InlineImageThumbnail.IsImagePath(token))
                {
                    if (textRun.Count > 0)
                    {
                        yield return ChatLabel.Selectable(
                            MarkdownInline.ToRichText(string.Join(" ", textRun)), richText: true);
                        textRun.Clear();
                    }
                    var resolved = ImageBlockRenderer.ResolvePath(token.Trim('`'));
                    yield return InlineImageThumbnail.Build(resolved);
                }
                else
                {
                    textRun.Add(token);
                }
            }

            if (textRun.Count > 0)
                yield return ChatLabel.Selectable(
                    MarkdownInline.ToRichText(string.Join(" ", textRun)), richText: true);
        }

        private static VisualElement BuildPill(string kindKey, string rawRef)
        {
            var chip = RefParser.Parse(kindKey, rawRef);
            var pill = ChipPillFactory.Build(chip.KindKey, chip.DisplayName);
            pill.tooltip = rawRef; // full ref for "reveal"

            var capturedRef = rawRef;
            var capturedKey = kindKey;

            var previewPanel = new ChipInlinePreviewPanel(capturedKey, capturedRef,
                () =>
                {
                    var provider = ChipKindRegistry.ForKey(capturedKey);
                    provider?.Navigate(capturedRef);
                });

            pill.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount == 1)
                    previewPanel.Toggle();
                else if (evt.clickCount >= 2)
                {
                    var provider = ChipKindRegistry.ForKey(capturedKey);
                    provider?.Navigate(capturedRef);
                }
            });

            ChipPillFactory.AttachAddToContextMenu(pill, chip,
                () => previewPanel.Toggle());

            // Wrap pill + panel in a column container so panel sits below pill
            var wrapper = new VisualElement();
            wrapper.AddToClassList("chip-pill-wrapper");
            wrapper.style.flexDirection = FlexDirection.Column;
            wrapper.Add(pill);
            wrapper.Add(previewPanel);
            return wrapper;
        }
    }
}
