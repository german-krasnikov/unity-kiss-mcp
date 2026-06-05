// Auto-growing input area: starts compact, grows with content, resets on send.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private void SetupAutoHeight()
        {
            _input.RegisterValueChangedCallback<string>(OnInputValueChanged);
        }

        private void OnInputValueChanged(ChangeEvent<string> evt)
        {
            UpdateAutoHeight();
        }

        internal void UpdateAutoHeight()
        {
            if (_heightCalc.ManualOverride) return;
            var h = _heightCalc.Compute(
                InputHeightCalc.CountLines(_input.value),
                position.height,
                (_chipField?.Model?.Count ?? 0) > 0);
            _inputArea.style.height    = h;
            _inputArea.style.minHeight = StyleKeyword.Null;
            _inputArea.style.maxHeight = _heightCalc.ComputeMax(position.height);
        }
    }
}
