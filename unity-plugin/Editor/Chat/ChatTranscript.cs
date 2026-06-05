// Transcript rendering. F13: AppendUserBubble(UserMessage) + AtMentionNormalizer wired.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ChatTranscript
    {
        private readonly VisualElement             _container;
        private readonly ChatBlockRendererRegistry _registry;
        private readonly StringBuilder             _assistantRaw = new StringBuilder();
        private readonly ToolChipGrouper           _grouper;
        private VisualElement _assistantBubble, _assistantRow, _liveTail;
        private string        _liveTailSrc;
        private int           _committed, _msgCount;
        private bool          _dirty;
        private IReadOnlyList<ChipData> _lastTurnChips;
        private const int MaxMessages = 200;

        internal ChatTranscript(VisualElement container, ChatBlockRendererRegistry registry)
        {
            _container = container;
            _registry  = registry;
            _grouper   = new ToolChipGrouper(Append, () => _msgCount--);
        }

        /// <summary>F13 Bug 2: set chips for @mention normalization in response.</summary>
        internal void SetLastTurnChips(IReadOnlyList<ChipData> chips) => _lastTurnChips = chips;

        /// <summary>F13: render an interleaved UserMessage (fixes Bug 1 double display).</summary>
        internal void AppendUserBubble(UserMessage msg, string imagePath = null)
        {
            FinalizeAssistant();
            var bubble = MakeBubble(ChipTextInterleaver.ToDisplayText(msg));
            var row    = Row("msg-user");
            var wrap   = new VisualElement(); wrap.AddToClassList("msg-user-content");
            wrap.style.flexDirection = FlexDirection.Row;
            wrap.style.flexWrap = Wrap.Wrap; wrap.style.alignItems = Align.Center;
            bool any = false;
            foreach (var seg in msg.Segments)
            {
                if (seg.IsChip)
                {
                    wrap.Add(ChipPillFactory.Build(seg.Chip));
                    any = true;
                }
                else if (!string.IsNullOrWhiteSpace(seg.Text))
                {
                    var lbl = ChatLabel.Selectable(seg.Text);
                    lbl.AddToClassList("msg-text");
                    wrap.Add(lbl);
                    any = true;
                }
            }
            if (any) bubble.Add(wrap);
            AppendImage(bubble, imagePath);
            row.Add(bubble); Append(row);
        }

        /// <summary>Legacy overload — kept for TryResumePendingTurn and existing tests.</summary>
        internal void AppendUserBubble(string text,
            IReadOnlyList<ChipData> chips = null, string imagePath = null)
        {
            FinalizeAssistant();
            var bubble    = MakeBubble(text ?? "");
            var row       = Row("msg-user");
            bool hasChips = chips != null && chips.Count > 0;
            if (hasChips)
            {
                var strip = new VisualElement(); strip.AddToClassList("user-chip-strip");
                foreach (var c in chips) strip.Add(ChipPillFactory.Build(c));
                bubble.Add(strip);
            }
            var dt = hasChips ? UserTextCleaner.Strip(text) : text;
            if (!string.IsNullOrEmpty(dt))
                bubble.Add(MixedParagraphRenderer.InlineElement(dt, "msg-text"));
            AppendImage(bubble, imagePath);
            row.Add(bubble); Append(row);
        }

        internal void AppendOrExtendAssistant(string token)
        { if (_assistantBubble == null) BeginAssistant(); _assistantRaw.Append(token); _dirty = true; }

        internal void FlushStreaming()
        { if (!_dirty || _assistantBubble == null) return; _dirty = false; RenderProgressive(final: false); }

        internal void FinalizeAssistant() { _grouper.Close(); FreezeAssistantBubble(); }

        internal void AppendToolChip(string toolName, bool ok, string toolId = null)
        {
            FreezeAssistantBubble();
            _grouper.Add(BuildChip(toolName, ok, toolId), isError: !ok);
        }

        private const string CopyAttachedClass = "copy-attached";
        internal void UpdateToolDetail(string toolId, ToolCallRecord rec)
        {
            if (string.IsNullOrEmpty(toolId)) return;
            var chip = FindChipById(toolId);
            if (chip == null) return;
            chip.userData = rec;
            if (!chip.ClassListContains(CopyAttachedClass))
            { CopyableText.Attach(chip); chip.AddToClassList(CopyAttachedClass); }
            ToolDetailBuilder.AttachOrUpdate(chip, rec);
        }

        private void FreezeAssistantBubble()
        {
            if (_assistantBubble == null) return;
            // F13 Bug 2: normalize @Name → [kind:ref] before final render.
            if (_lastTurnChips != null && _lastTurnChips.Count > 0)
            {
                var raw        = _assistantRaw.ToString();
                var normalized = AtMentionNormalizer.Normalize(raw, _lastTurnChips);
                if (normalized != raw) { _assistantRaw.Clear(); _assistantRaw.Append(normalized); }
            }
            RenderProgressive(final: true);
            _assistantBubble.userData = _assistantRaw.ToString();
            CopyableText.Attach(_assistantBubble);
            _assistantBubble = null; _assistantRow = null; _liveTail = null; _liveTailSrc = null;
            _committed = 0; _assistantRaw.Clear(); _dirty = false;
        }

        private static VisualElement BuildChip(string toolName, bool ok, string toolId = null)
        {
            var chip = new VisualElement(); chip.AddToClassList("tool-chip");
            if (!ok) chip.AddToClassList("tool-chip--error");
            var verb = ToolVerbMap.Humanize(toolName);
            var lbl  = new Label(ok ? $"⚙ {verb}" : $"✕ {verb}"); lbl.AddToClassList("tool-chip-label");
            chip.Add(lbl); chip.userData = toolId; return chip;
        }

        private void BeginAssistant()
        {
            _grouper.Close(); _assistantRaw.Clear();
            _committed = 0; _liveTail = null; _liveTailSrc = null;
            _assistantRow    = Row(null);
            _assistantBubble = new VisualElement();
            _assistantBubble.AddToClassList("msg-bubble");
            _assistantBubble.AddToClassList("msg-bubble--assistant");
            _assistantBubble.AddToClassList("md-bubble");
            _assistantRow.Add(_assistantBubble); Append(_assistantRow);
        }

        private void RenderProgressive(bool final)
        {
            var blocks   = MarkdownParser.Parse(_assistantRaw.ToString());
            bool hasTail = !final && blocks.Count > 0;
            var  tail    = hasTail ? blocks[blocks.Count - 1] : default(MdBlock);
            bool reuse   = _liveTail != null && hasTail && tail.Kind == MdBlockKind.Image
                           && _liveTailSrc != null && _liveTailSrc == tail.Src;
            if (_liveTail != null && !reuse)
            { _liveTail.RemoveFromHierarchy(); _liveTail = null; _liveTailSrc = null; }
            int commitUpTo = final ? blocks.Count : blocks.Count - 1;
            for (int idx = _committed; idx < commitUpTo; idx++)
            { var blk = blocks[idx]; _assistantBubble.Add(_registry.RenderBlock(in blk)); }
            if (commitUpTo > _committed) _committed = commitUpTo;
            if (hasTail && !reuse)
            {
                _liveTail = _registry.RenderBlock(in tail);
                _liveTailSrc = tail.Kind == MdBlockKind.Image ? tail.Src : null;
                _assistantBubble.Add(_liveTail);
            }
        }

        private VisualElement FindChipById(string toolId)
            => FindDescendant(_container, ve =>
                (ve.userData is string s && s == toolId) ||
                (ve.userData is ToolCallRecord r && r.Id == toolId));

        private static VisualElement FindDescendant(VisualElement root, Func<VisualElement, bool> pred)
        {
            foreach (var child in root.Children())
            {
                if (pred(child)) return child;
                var found = FindDescendant(child, pred);
                if (found != null) return found;
            }
            return null;
        }

        internal void Clear() { FinalizeAssistant(); _container.Clear(); _msgCount = 0; }

        private static VisualElement Row(string cls)
        { var r = new VisualElement(); r.AddToClassList("msg-row"); if (cls != null) r.AddToClassList(cls); return r; }

        internal void Append(VisualElement el)
        {
            _container.Add(el); _msgCount++;
            if (_msgCount > MaxMessages) { _container.RemoveAt(0); _msgCount--; }
        }

        private static VisualElement MakeBubble(string userData)
        {
            var b = new VisualElement();
            b.AddToClassList("msg-bubble"); b.AddToClassList("msg-bubble--user");
            b.userData = userData; CopyableText.Attach(b); return b;
        }

        private void AppendImage(VisualElement bubble, string imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath))
            { var img = MdBlock.Image(imagePath, ""); bubble.Add(_registry.RenderBlock(in img)); }
        }
    }
}
