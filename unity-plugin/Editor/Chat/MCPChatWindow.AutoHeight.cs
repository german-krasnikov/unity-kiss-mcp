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
            var minH = _heightCalc.Compute(
                InputHeightCalc.CountLines(_input.value),
                position.height,
                _objChipStrip.childCount > 0);
            _inputArea.style.height    = StyleKeyword.Null;
            _inputArea.style.minHeight = minH;
            _inputArea.style.maxHeight = _heightCalc.ComputeMax(position.height);
        }
    }
}
