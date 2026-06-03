// Pure activity-state machine — ZERO UnityEngine deps. Tested by ChatActivityStateTests.
namespace UnityMCP.Editor.Chat
{
    public enum ActivityPhase { Idle, Sending, Receiving }

    public sealed class ChatActivityState
    {
        public ActivityPhase Phase { get; private set; } = ActivityPhase.Idle;

        // Returns true if state changed (Idle -> Sending). Idempotent: Sending -> Sending = false.
        public bool Send()
        {
            if (Phase == ActivityPhase.Sending || Phase == ActivityPhase.Receiving) return false;
            Phase = ActivityPhase.Sending;
            return true;
        }

        // Sending -> Receiving. Ignored from any other phase (stale delta = no-op).
        public bool FirstToken()
        {
            if (Phase != ActivityPhase.Sending) return false;
            Phase = ActivityPhase.Receiving;
            return true;
        }

        // Any active phase -> Idle. Returns false if already Idle.
        public bool Done()
        {
            if (Phase == ActivityPhase.Idle) return false;
            Phase = ActivityPhase.Idle;
            return true;
        }

        // Errors always reset to Idle, same as Done.
        public bool Fail() => Done();
    }
}
