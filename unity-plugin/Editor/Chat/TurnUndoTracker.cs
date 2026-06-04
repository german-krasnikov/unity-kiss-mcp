// Per-turn Undo rollback tracker (F6). Chat-only (Chat asmdef, defineConstraint UNITY_MCP_CHAT).
using System.Collections.Generic;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    public sealed class TurnUndoTracker
    {
        private readonly struct TurnRecord
        {
            public readonly int    GroupId;
            public readonly string Label;
            public TurnRecord(int id, string label) { GroupId = id; Label = label; }
        }

        private readonly List<TurnRecord> _turns = new List<TurnRecord>();
        private int  _inflightGroupId = -1;
        private bool _turnInFlight;

        // Increments each time OnTurnStart is called — lets RestoreButton know
        // that the captured generation is stale and must disable itself.
        public int CurrentGeneration { get; private set; }

        public bool HasRestorableGroup => _turns.Count > 0;

        // Exposes the in-flight group id so SaveStateBeforeReload can persist it (#12).
        // -1 when no turn is in flight.
        public int InflightGroupId => _inflightGroupId;

        public void OnTurnStart(string label)
        {
            CurrentGeneration++;
            _inflightGroupId = UndoGroupHelper.OpenNamedGroup("Chat: " + label);
            _turnInFlight    = true;
        }

        public void OnTurnEnd()
        {
            if (!_turnInFlight) return;
            UndoGroupHelper.CloseNamedGroup(_inflightGroupId);
            _turns.Add(new TurnRecord(_inflightGroupId, ""));
            _inflightGroupId = -1;
            _turnInFlight    = false;
        }

        // Partial mutations are still restorable — treat same as OnTurnEnd.
        public void OnTurnFailed() => OnTurnEnd();

        public void RestoreLastTurn()
        {
            if (_turns.Count == 0) return;
            var top = _turns[_turns.Count - 1];
            _turns.RemoveAt(_turns.Count - 1);
            UndoGroupHelper.RevertToBeforeGroup(top.GroupId);
        }

        // Called on domain reload — in-memory group indices are stale.
        public void Invalidate()
        {
            _turns.Clear();
            _inflightGroupId = -1;
            _turnInFlight    = false;
        }
    }
}
