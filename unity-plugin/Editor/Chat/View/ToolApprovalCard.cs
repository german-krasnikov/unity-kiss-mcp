// Interactive card shown when Claude requests tool permission.
// Wires 4 buttons → ApprovalDecision callback. Inline styles only (no USS file dependency).
using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ToolApprovalCard : VisualElement
    {
        private bool _resolved;
        private readonly Button[] _buttons = new Button[4];
        private readonly Label _status;

        internal ToolApprovalCard(
            string requestId,
            string toolName,
            string toolInputJson,
            RiskLevel risk,
            Action<ApprovalDecision> onDecision)
        {
            AddToClassList("approval-card");
            style.borderLeftWidth = 3;
            style.borderLeftColor = RiskColor(risk);
            style.marginTop = 4; style.marginBottom = 4;
            style.paddingTop = 6; style.paddingBottom = 6;
            style.paddingLeft = 8; style.paddingRight = 8;
            style.backgroundColor = new UnityEngine.Color(0.23f, 0.23f, 0.23f, 0.3f);
            style.borderTopLeftRadius = style.borderTopRightRadius =
            style.borderBottomLeftRadius = style.borderBottomRightRadius = 4;

            // Header
            var header = new Label($"Allow tool: {toolName}");
            header.AddToClassList("approval-card__header");
            header.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            header.style.fontSize = 12;
            Add(header);

            // Risk badge
            var badge = new Label(RiskLabel(risk));
            badge.AddToClassList("approval-card__risk");
            badge.style.fontSize = 10;
            badge.style.paddingTop = 2; badge.style.paddingBottom = 2;
            badge.style.paddingLeft = 6; badge.style.paddingRight = 6;
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius =
            badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 3;
            badge.style.alignSelf = Align.FlexStart;
            badge.style.marginTop = 2;
            badge.style.color = RiskColor(risk);
            Add(badge);

            // Tool input preview
            if (!string.IsNullOrEmpty(toolInputJson))
            {
                var preview = toolInputJson.Length > 200
                    ? toolInputJson.Substring(0, 200) + "…"
                    : toolInputJson;
                var detail = new Label(preview);
                detail.AddToClassList("approval-card__detail");
                detail.style.fontSize = 10;
                detail.style.color = new UnityEngine.Color(0.53f, 0.53f, 0.53f);
                detail.style.whiteSpace = WhiteSpace.Normal;
                detail.style.maxHeight = 80;
                detail.style.overflow = Overflow.Hidden;
                detail.style.marginTop = 4;
                Add(detail);
            }

            // Button row
            var row = new VisualElement();
            row.AddToClassList("approval-card__buttons");
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 6;
            row.style.flexWrap = Wrap.Wrap;

            _buttons[0] = MakeBtn("Allow",   () => Resolve(ApprovalDecision.Allow,        onDecision));
            _buttons[1] = MakeBtn("Deny",    () => Resolve(ApprovalDecision.Deny,          onDecision));
            _buttons[2] = MakeBtn("Session", () => Resolve(ApprovalDecision.AllowSession,  onDecision));
            _buttons[3] = MakeBtn("Always",  () => Resolve(ApprovalDecision.AlwaysAllow,   onDecision));

            var green = new UnityEngine.Color(0.29f, 0.87f, 0.50f, 0.3f);
            _buttons[0].style.backgroundColor = green;
            _buttons[1].style.backgroundColor = new UnityEngine.Color(0.97f, 0.44f, 0.44f, 0.3f);
            _buttons[2].style.backgroundColor = green;
            _buttons[3].style.backgroundColor = green;

            foreach (var b in _buttons) row.Add(b);
            Add(row);

            // Status label (hidden until resolved)
            _status = new Label();
            _status.AddToClassList("approval-card__status");
            _status.style.display = DisplayStyle.None;
            _status.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Italic;
            _status.style.color = new UnityEngine.Color(0.67f, 0.67f, 0.67f);
            _status.style.marginTop = 4;
            Add(_status);
        }

        private void Resolve(ApprovalDecision decision, Action<ApprovalDecision> onDecision)
        {
            if (_resolved) return;
            _resolved = true;
            foreach (var b in _buttons) b.SetEnabled(false);
            var denied = decision == ApprovalDecision.Deny;
            _status.text = denied ? "Denied" : "Approved";
            var statusColor = denied
                ? new UnityEngine.Color(0.97f, 0.44f, 0.44f)
                : new UnityEngine.Color(0.29f, 0.87f, 0.50f);
            _status.style.color = statusColor;
            style.borderLeftColor = statusColor;
            _status.style.display = DisplayStyle.Flex;
            AddToClassList("approval-card--resolved");
            style.opacity = 0.6f;
            onDecision?.Invoke(decision);
        }

        private static Button MakeBtn(string label, Action onClick)
        {
            var b = new Button(onClick) { text = label };
            b.userData = onClick; // expose for tests
            b.AddToClassList("approval-card__btn");
            b.style.minWidth = 60;
            b.style.marginRight = 4;
            return b;
        }

        private static string RiskLabel(RiskLevel r) => r switch
        {
            RiskLevel.High   => "HIGH RISK",
            RiskLevel.Low    => "LOW",
            _                => "MEDIUM",
        };

        private static UnityEngine.Color RiskColor(RiskLevel r) => r switch
        {
            RiskLevel.High   => new UnityEngine.Color(0.97f, 0.44f, 0.44f),
            RiskLevel.Low    => new UnityEngine.Color(0.29f, 0.87f, 0.50f),
            _                => new UnityEngine.Color(0.98f, 0.75f, 0.14f),
        };
    }
}
