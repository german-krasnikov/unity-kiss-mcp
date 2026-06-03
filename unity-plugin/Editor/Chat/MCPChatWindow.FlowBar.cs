// FlowBar partial: fixed track with inner chip sweep — fixes the ±100% glitch.
// Animation: track fades in/out; chip sweeps left↔right via CSS translate classes.
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
    }
}
