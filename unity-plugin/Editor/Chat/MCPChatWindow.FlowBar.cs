// FlowBar partial: fixed track with inner chip sweep — fixes the ±100% glitch.
// Animation: track fades in/out; chip sweeps left↔right via CSS translate classes.
// Also owns BuildFooterBar / MakeModeBtn (footer is tightly coupled to mode-toggle state).
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private ChatActivityState _activity = new ChatActivityState();
        private VisualElement _flowBar;
        private VisualElement _flowFill;
        private bool _sweepPhase;

        private VisualElement BuildFlowBar()
        {
            _flowBar = new VisualElement();
            _flowBar.AddToClassList("flowbar");
            _flowFill = new VisualElement();
            _flowFill.AddToClassList("flowbar__fill");
            _flowBar.Add(_flowFill);
            return _flowBar;
        }

        private void OnActivityChanged()
        {
            if (_flowBar == null) return; // defense-in-depth: guard against pre-CreateGUI calls
            switch (_activity.Phase)
            {
                case ActivityPhase.Sending:
                    _flowBar.AddToClassList("flowbar--active");
                    _flowFill.RemoveFromClassList("flowbar__fill--receiving");
                    _flowFill.AddToClassList("flowbar__fill--sending");
                    // Kick chip to --a immediately so motion starts now, not ~950ms later
                    _flowFill.RemoveFromClassList("flowbar__fill--b");
                    _flowFill.AddToClassList("flowbar__fill--a");
                    _sweepPhase = false; // first tick flips to --b → chip sweeps --a→--b, no dead hold
                    break;
                case ActivityPhase.Receiving:
                    _flowBar.AddToClassList("flowbar--active");
                    _flowFill.RemoveFromClassList("flowbar__fill--sending");
                    _flowFill.AddToClassList("flowbar__fill--receiving");
                    // Kick chip to --a immediately so motion starts now, not ~950ms later
                    _flowFill.RemoveFromClassList("flowbar__fill--b");
                    _flowFill.AddToClassList("flowbar__fill--a");
                    _sweepPhase = false; // first tick flips to --b → chip sweeps --a→--b, no dead hold
                    break;
                default:
                    _flowBar.RemoveFromClassList("flowbar--active");
                    _flowFill.RemoveFromClassList("flowbar__fill--sending");
                    _flowFill.RemoveFromClassList("flowbar__fill--receiving");
                    _flowFill.RemoveFromClassList("flowbar__fill--a");
                    _flowFill.RemoveFromClassList("flowbar__fill--b");
                    _sweepPhase = false;
                    break;
            }
        }

        private void TickFlowBarSweep()
        {
            if (_activity.Phase == ActivityPhase.Idle) return;
            _sweepPhase = !_sweepPhase;
            _flowFill.EnableInClassList("flowbar__fill--a", !_sweepPhase);
            _flowFill.EnableInClassList("flowbar__fill--b",  _sweepPhase);
        }

        // ── Footer bar (moved here from MCPChatWindow.cs to stay under 200 lines) ─

        private VisualElement BuildFooterBar()
        {
            var bar = new VisualElement(); bar.AddToClassList("footer-bar");

            var sel = BuildAgentSelector();
            sel.AddToClassList("footer-selector");
            bar.Add(sel);

            var seg = new VisualElement(); seg.AddToClassList("mode-segment");
            _askBtn   = MakeModeBtn("Ask",   false);
            _agentBtn = MakeModeBtn("Agent", true);
            seg.Add(_askBtn); seg.Add(_agentBtn);
            bar.Add(seg);

            var spacer = new VisualElement(); spacer.AddToClassList("footer-spacer");
            bar.Add(spacer);

            var autoScrollToggle = new Toggle("Auto-scroll") { value = _autoScrollEnabled };
            autoScrollToggle.AddToClassList("autoscroll-toggle");
            autoScrollToggle.RegisterValueChangedCallback(evt =>
            {
                _autoScrollEnabled = evt.newValue;
                EditorPrefs.SetBool("MCPChat.AutoScroll", evt.newValue);
            });
            bar.Add(autoScrollToggle);
            bar.Add(BuildSessionMenuButton());

            _tokenReadout = new Label(""); _tokenReadout.AddToClassList("token-readout");
            bar.Add(_tokenReadout);

            var ssBtn   = new Button(AttachScreenshot) { text = "SS", tooltip = "Attach 4-panel screenshot" };
            ssBtn.AddToClassList("chat-btn"); ssBtn.AddToClassList("chat-btn--screenshot");
            var sendBtn = new Button(OnSend) { text = "Send" };
            sendBtn.AddToClassList("chat-btn"); sendBtn.AddToClassList("chat-btn--send");
            bar.Add(ssBtn); bar.Add(sendBtn);
            return bar;
        }

        private Button MakeModeBtn(string label, bool isAgent)
        {
            var btn = new Button(() => SetMode(isAgent)) { text = label };
            btn.AddToClassList("mode-toggle-btn");
            if (_agentMode == isAgent) btn.AddToClassList("mode-toggle-btn--active");
            return btn;
        }
    }
}
