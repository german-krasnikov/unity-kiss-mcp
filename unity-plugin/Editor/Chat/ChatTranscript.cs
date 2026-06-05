// Transcript rendering helpers — extracted to keep MCPChatWindow under 200 lines.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>Manages the scroll + transcript VisualElement tree.
    /// Assistant messages render markdown progressively: a block is committed (frozen)
    /// once a later block follows it; only the trailing in-progress block re-renders.</summary>
    internal sealed class ChatTranscript
    {
        private readonly VisualElement             _container;
        private readonly ChatBlockRendererRegistry _registry;
        private readonly StringBuilder             _assistantRaw = new StringBuilder();
        private readonly ToolChipGrouper           _grouper;
        private VisualElement _assistantBubble;
        private VisualElement _assistantRow;
        private VisualElement _liveTail;
        private string        _liveTailSrc;
        private int           _committed;
        private bool          _dirty;
        private int           _msgCount;
        private const int MaxMessages = 200;

        internal ChatTranscript(VisualElement container, ChatBlockRendererRegistry registry)
        {
            _container = container;
            _registry  = registry;
            _grouper   = new ToolChipGrouper(Append, () => _msgCount--);
        }

        internal void AppendUserBubble(string text,
            IReadOnlyList<ChipData> chips = null, string imagePath = null)
        {
            FinalizeAssistant();
            var row = Row("msg-user");
            var bubble = new VisualElement();
            bubble.AddToClassList("msg-bubble"); bubble.AddToClassList("msg-bubble--user");
            bubble.userData = text ?? "";
            CopyableText.Attach(bubble);
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
            if (!string.IsNullOrEmpty(imagePath))
            { var img = MdBlock.Image(imagePath, ""); bubble.Add(_registry.RenderBlock(in img)); }
            row.Add(bubble); Append(row);
        }

        internal void AppendOrExtendAssistant(string token)
        {
            if (_assistantBubble == null) BeginAssistant();
            _assistantRaw.Append(token);
            _dirty = true;
        }

        /// <summary>Renders newly-completed blocks + refreshes the trailing block.
        /// Cheap: only the tail re-renders per tick. Call once per drain tick.</summary>
        internal void FlushStreaming()
        {
            if (!_dirty || _assistantBubble == null) return;
            _dirty = false;
            RenderProgressive(final: false);
        }

        internal void FinalizeAssistant()
        {
            _grouper.Close();           // TurnDone breaks any open tool group
            FreezeAssistantBubble();
        }

        internal void AppendToolChip(string toolName, bool ok, string toolId = null)
        {
            FreezeAssistantBubble();    // flush streaming text, but keep tool group open
            _grouper.Add(BuildChip(toolName, ok, toolId), isError: !ok);
        }

        private const string CopyAttachedClass = "copy-attached";
        internal void UpdateToolDetail(string toolId, ToolCallRecord rec)
        {
            if (string.IsNullOrEmpty(toolId)) return;
            var chip = FindChipById(toolId);
            if (chip == null) return;
            chip.userData = rec;    // upgrade from string id to full record
            // Attach copy handler once — after userData is a meaningful record.
            if (!chip.ClassListContains(CopyAttachedClass))
            {
                CopyableText.Attach(chip);
                chip.AddToClassList(CopyAttachedClass);
            }
            ToolDetailBuilder.AttachOrUpdate(chip, rec);
        }

        // Freezes the streaming assistant bubble without touching the tool group.
        private void FreezeAssistantBubble()
        {
            if (_assistantBubble == null) return;
            RenderProgressive(final: true);
            // Snapshot raw markdown for right-click copy BEFORE clearing
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
            var lbl  = new Label(ok ? $"⚙ {verb}" : $"✕ {verb}");
            lbl.AddToClassList("tool-chip-label");
            chip.Add(lbl);
            chip.userData = toolId;     // string id initially; upgraded to ToolCallRecord on UpdateToolDetail
            // Do NOT attach copy here — raw toolId string is meaningless.
            // CopyableText.Attach is deferred to UpdateToolDetail after record is available.
            return chip;
        }

        private void BeginAssistant()
        {
            _grouper.Close();
            _assistantRaw.Clear();
            _committed = 0; _liveTail = null; _liveTailSrc = null;
            _assistantRow    = Row(null);
            _assistantBubble = new VisualElement();
            _assistantBubble.AddToClassList("msg-bubble");
            _assistantBubble.AddToClassList("msg-bubble--assistant");
            _assistantBubble.AddToClassList("md-bubble");
            _assistantRow.Add(_assistantBubble);
            Append(_assistantRow);
        }

        // Commits blocks [_committed .. tail) and (re)renders the trailing block.
        // Image tail with unchanged Src is REUSED to avoid texture churn.
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
                _liveTail    = _registry.RenderBlock(in tail);
                _liveTailSrc = tail.Kind == MdBlockKind.Image ? tail.Src : null;
                _assistantBubble.Add(_liveTail);
            }
        }

        private VisualElement FindChipById(string toolId)
        {
            return FindDescendant(_container, ve =>
                (ve.userData is string s && s == toolId) ||
                (ve.userData is ToolCallRecord r && r.Id == toolId));
        }

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

        internal void Clear()
        {
            FinalizeAssistant(); _container.Clear(); _msgCount = 0;
        }

        private static VisualElement Row(string cls)
        { var r = new VisualElement(); r.AddToClassList("msg-row"); if (cls != null) r.AddToClassList(cls); return r; }

        internal void Append(VisualElement el)
        {
            _container.Add(el); _msgCount++;
            if (_msgCount > MaxMessages) { _container.RemoveAt(0); _msgCount--; }
        }
    }
}
