// Transcript rendering helpers — extracted to keep MCPChatWindow under 200 lines.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>Manages the scroll + transcript VisualElement tree.</summary>
    internal sealed class ChatTranscript
    {
        private readonly VisualElement _container;
        private Label _lastAssistantLabel;
        private int _msgCount;
        private const int MaxMessages = 200;

        internal ChatTranscript(VisualElement container)
        {
            _container = container;
        }

        internal void AppendUserBubble(string text)
        {
            _lastAssistantLabel = null;
            var row = Row("msg-user");
            var lbl = new Label(text);
            lbl.AddToClassList("msg-bubble");
            lbl.AddToClassList("msg-bubble--user");
            row.Add(lbl);
            Append(row);
        }

        internal void AppendOrExtendAssistant(string token)
        {
            if (_lastAssistantLabel == null)
            {
                var row = Row(null);
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
        }

        internal void AppendToolChip(string toolName, bool ok)
        {
            _lastAssistantLabel = null;
            var chip = new VisualElement(); chip.AddToClassList("tool-chip");
            if (!ok) chip.AddToClassList("tool-chip--error");
            var verb = ToolVerbMap.Humanize(toolName);
            var lbl  = new Label(ok ? $"⚙ {verb}" : $"✕ {verb}");
            lbl.AddToClassList("tool-chip-label");
            chip.Add(lbl);
            Append(chip);
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
