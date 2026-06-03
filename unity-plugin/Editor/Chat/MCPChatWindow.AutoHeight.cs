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
                _objChipStrip.childCount > 0);
            // flex-grow on .chat-input needs a DEFINITE parent height; minHeight is a floor
            // and makes flex-grow a no-op — use height instead so the input fills to footer.
            _inputArea.style.height    = h;
            _inputArea.style.minHeight = StyleKeyword.Null;
            _inputArea.style.maxHeight = _heightCalc.ComputeMax(position.height);
        }
    }
}
