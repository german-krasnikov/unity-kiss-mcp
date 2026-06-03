// FlowBar partial: builds the 2px sweep bar + wires activity state to CSS classes.
// Animation pattern mirrors MCPStatusWindow.BeatFast() — schedule.Every + class toggle.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private ChatActivityState _activity = new ChatActivityState();
        private VisualElement _flowBar;
        private bool _sweepPhase;

        private static readonly string[] PhaseClasses =
            { "flowbar--idle", "flowbar--sending", "flowbar--receiving" };

        private VisualElement BuildFlowBar()
        {
            _flowBar = new VisualElement();
            _flowBar.AddToClassList("flowbar");
            _flowBar.AddToClassList("flowbar--idle");
            return _flowBar;
        }

        private void OnActivityChanged()
        {
            var target = _activity.Phase switch
            {
                ActivityPhase.Sending   => "flowbar--sending",
                ActivityPhase.Receiving => "flowbar--receiving",
                _                       => "flowbar--idle"
            };
            foreach (var cls in PhaseClasses) _flowBar.RemoveFromClassList(cls);
            _flowBar.AddToClassList(target);
            // Reset sweep position on every phase transition
            _flowBar.RemoveFromClassList("flowbar--sweep-a");
            _flowBar.RemoveFromClassList("flowbar--sweep-b");
            _sweepPhase = false;
        }

        private void TickFlowBarSweep()
        {
            if (_activity.Phase == ActivityPhase.Idle) return;
            _sweepPhase = !_sweepPhase;
            _flowBar.RemoveFromClassList(_sweepPhase ? "flowbar--sweep-b" : "flowbar--sweep-a");
            _flowBar.AddToClassList(_sweepPhase ? "flowbar--sweep-a" : "flowbar--sweep-b");
        }
    }
}
