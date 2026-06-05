// Builds/updates expandable args+result detail inside a tool chip.
// Click on chip toggles detail visibility.
using System.Text;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class ToolDetailBuilder
    {
        private const string DetailClass  = "tool-detail";
        private const string ArgsClass    = "tool-detail-args";
        private const string ResultClass  = "tool-detail-result";

        internal static void AttachOrUpdate(VisualElement chip, ToolCallRecord rec)
        {
            var detail = chip.Q<VisualElement>(className: DetailClass);
            if (detail == null)
            {
                detail = new VisualElement();
                detail.AddToClassList(DetailClass);
                detail.style.display = UnityEngine.UIElements.DisplayStyle.None;
                chip.Add(detail);
                chip.AddToClassList("tool-chip--expandable");
                chip.RegisterCallback<ClickEvent>(OnChipClick);
            }

            if (!string.IsNullOrEmpty(rec.ArgsJson))
            {
                var argsEl = detail.Q<Label>(className: ArgsClass);
                if (argsEl == null)
                {
                    detail.Add(MakeHeader("Input"));
                    argsEl = ChatLabel.Selectable(FormatArgs(rec.ArgsJson));
                    argsEl.AddToClassList(ArgsClass);
                    detail.Add(argsEl);
                }
                else argsEl.text = FormatArgs(rec.ArgsJson);
            }

            if (rec.HasResult)
            {
                var resultEl = detail.Q<Label>(className: ResultClass);
                if (resultEl == null)
                {
                    detail.Add(MakeHeader(rec.IsOk ? "Result" : "Error"));
                    resultEl = ChatLabel.Selectable(rec.ResultText);
                    resultEl.AddToClassList(ResultClass);
                    if (!rec.IsOk) resultEl.AddToClassList("tool-detail-result--error");
                    detail.Add(resultEl);
                }
                else resultEl.text = rec.ResultText;
            }
        }

        private static void OnChipClick(ClickEvent e)
        {
            var chip   = e.currentTarget as VisualElement;
            var detail = chip?.Q<VisualElement>(className: DetailClass);
            if (detail == null) return;
            bool show  = detail.resolvedStyle.display != UnityEngine.UIElements.DisplayStyle.Flex;
            detail.style.display = show ? UnityEngine.UIElements.DisplayStyle.Flex
                                        : UnityEngine.UIElements.DisplayStyle.None;
            chip.EnableInClassList("tool-chip--expanded", show);
            e.StopPropagation();
        }

        private static Label MakeHeader(string text)
        {
            var lbl = new Label(text);
            lbl.AddToClassList("tool-detail-header");
            return lbl;
        }

        // Minimal pretty-print: newlines after { [ and before } ]
        private static string FormatArgs(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length < 3) return json;
            var sb = new StringBuilder(json.Length + 20);
            int depth = 0; bool inStr = false, esc = false;
            foreach (var c in json)
            {
                if (esc) { sb.Append(c); esc = false; continue; }
                if (inStr)
                {
                    sb.Append(c);
                    if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inStr = true; sb.Append(c); break;
                    case '{': case '[':
                        sb.Append(c); depth++;
                        sb.Append('\n').Append(' ', depth * 2); break;
                    case '}': case ']':
                        depth--;
                        sb.Append('\n').Append(' ', System.Math.Max(0, depth) * 2).Append(c); break;
                    case ',':
                        sb.Append(c).Append('\n').Append(' ', depth * 2); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
