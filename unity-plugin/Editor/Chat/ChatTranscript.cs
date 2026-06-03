// Transcript rendering helpers — extracted to keep MCPChatWindow under 200 lines.
using System.Text;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>Manages the scroll + transcript VisualElement tree.</summary>
    internal sealed class ChatTranscript
    {
        private readonly VisualElement           _container;
        private readonly ChatBlockRendererRegistry _registry;
        private readonly StringBuilder           _assistantRaw = new StringBuilder();
        private Label         _lastAssistantLabel;
        private VisualElement _lastAssistantRow;
        private int           _msgCount;
        private const int MaxMessages = 200;

        internal ChatTranscript(VisualElement container, ChatBlockRendererRegistry registry)
        {
            _container = container;
            _registry  = registry;
        }

        internal void AppendUserBubble(string text, string imagePath = null)
        {
            FinalizeAssistant();
            var row    = Row("msg-user");
            var bubble = new VisualElement();
            bubble.AddToClassList("msg-bubble");
            bubble.AddToClassList("msg-bubble--user");
            if (!string.IsNullOrEmpty(text))
            {
                var lbl = new Label(text); lbl.AddToClassList("msg-text");
                bubble.Add(lbl);
            }
            if (!string.IsNullOrEmpty(imagePath))
            {
                var img = MdBlock.Image(imagePath, "");
                bubble.Add(_registry.RenderBlock(in img));
            }
            row.Add(bubble);
            Append(row);
        }

        internal void AppendOrExtendAssistant(string token)
        {
            if (_lastAssistantLabel == null)
            {
                _assistantRaw.Clear();                 // raw is cleared exactly when a new live label begins
                var row = Row(null); _lastAssistantRow = row;
                _lastAssistantLabel = new Label(token);
                _lastAssistantLabel.AddToClassList("msg-bubble");
                _lastAssistantLabel.AddToClassList("msg-bubble--assistant");
                row.Add(_lastAssistantLabel);
                Append(row);
            }
            else
            {
                _lastAssistantLabel.text += token;
            }
            _assistantRaw.Append(token);               // BOTH branches
        }

        internal void AppendToolChip(string toolName, bool ok)
        {
            FinalizeAssistant();
            var chip = new VisualElement(); chip.AddToClassList("tool-chip");
            if (!ok) chip.AddToClassList("tool-chip--error");
            var verb = ToolVerbMap.Humanize(toolName);
            var lbl  = new Label(ok ? $"⚙ {verb}" : $"✕ {verb}");
            lbl.AddToClassList("tool-chip-label");
            chip.Add(lbl);
            Append(chip);
        }

        internal void FinalizeAssistant()
        {
            if (_lastAssistantLabel == null) return;
            var raw = _assistantRaw.ToString();
            _lastAssistantRow.Clear();
            var bubble = new VisualElement();
            bubble.AddToClassList("msg-bubble");
            bubble.AddToClassList("msg-bubble--assistant");
            bubble.AddToClassList("md-bubble");
            foreach (var b in MarkdownParser.Parse(raw)) { var blk = b; bubble.Add(_registry.RenderBlock(in blk)); }
            _lastAssistantRow.Add(bubble);
            _lastAssistantLabel = null; _lastAssistantRow = null;
        }

        private static VisualElement Row(string extraClass)
        {
            var row = new VisualElement(); row.AddToClassList("msg-row");
            if (extraClass != null) row.AddToClassList(extraClass);
            return row;
        }

        private void Append(VisualElement el)
        {
            _container.Add(el);
            _msgCount++;
            if (_msgCount > MaxMessages)
            {
                _container.RemoveAt(0);
                _msgCount--;
            }
        }
    }
}
