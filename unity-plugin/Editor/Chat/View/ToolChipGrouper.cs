// Grouping for consecutive tool calls — extracted to keep ChatTranscript <200 lines.
// State transitions live in the pure ToolGroupState; this class only wires VisualElements.
using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>Lazy-promote: lone chip stays bare; 2nd consecutive chip promotes both into a
    /// collapsed Foldout; 3rd+ appends to the open Foldout.</summary>
    internal sealed class ToolChipGrouper
    {
        private readonly Action<VisualElement> _append;
        private readonly Action                _decrementCount;
        private readonly ToolGroupState        _state = new ToolGroupState();

        private VisualElement _pendingChip;
        private Foldout       _activeGroup;
        private VisualElement _groupBody;

        internal ToolChipGrouper(Action<VisualElement> append, Action decrementCount)
        {
            _append         = append;
            _decrementCount = decrementCount;
        }

        /// <summary>Call on every ToolStart / Error event.</summary>
        internal void Add(VisualElement chip, bool isError)
        {
            bool pendingAlive = _pendingChip != null && _pendingChip.parent != null;
            switch (_state.OnTool(isError, pendingAlive))
            {
                case ToolGroupAction.Append:
                    _groupBody.Add(chip);
                    RefreshHeader();
                    break;

                case ToolGroupAction.Promote:
                    // pendingAlive==false => the 200-msg cap evicted the bare chip before the
                    // 2nd tool arrived; group holds only the new chip (Count==1, '⚙ 1 tool').
                    if (pendingAlive) { _pendingChip.RemoveFromHierarchy(); _decrementCount(); }
                    _activeGroup = MakeFoldout();
                    _groupBody   = new VisualElement(); _groupBody.AddToClassList("tool-group-body");
                    _activeGroup.contentContainer.Add(_groupBody);
                    if (pendingAlive) _groupBody.Add(_pendingChip);
                    _groupBody.Add(chip);
                    _pendingChip = null;
                    _append(_activeGroup);
                    RefreshHeader();
                    break;

                default: // SetPending
                    _pendingChip = chip;
                    _append(chip);
                    break;
            }
        }

        /// <summary>Call when a non-tool event breaks the run (BeginAssistant, AppendUserBubble, FinalizeAssistant).</summary>
        internal void Close()
        {
            if (_activeGroup != null)
            {
                _activeGroup.text = ToolGroupSummary.Format(_state.Count, _state.AnyError, running: false);
                CopyableText.AttachToGroup(_activeGroup, _groupBody);
            }
            _activeGroup = null; _groupBody = null; _pendingChip = null;
            _state.Reset();
        }

        private void RefreshHeader()
        {
            _activeGroup.text = ToolGroupSummary.Format(_state.Count, _state.AnyError, running: true);
            if (_state.AnyError) _activeGroup.AddToClassList("tool-group--has-error");
            else                 _activeGroup.RemoveFromClassList("tool-group--has-error");
        }

        private static Foldout MakeFoldout()
        {
            var f = new Foldout(); f.value = false;
            f.AddToClassList("tool-group");
            return f;
        }
    }
}
