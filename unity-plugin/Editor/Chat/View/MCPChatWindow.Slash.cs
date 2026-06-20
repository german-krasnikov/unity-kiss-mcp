// Slash-command partial: registers the composer field change event and manages SlashPopup.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private SlashPopup _slashPopup;

        private void SetupSlash()
        {
            _slashPopup = new SlashPopup(_inputArea, _input);

            _input.RegisterCallback<ChangeEvent<string>>(ev =>
            {
                var val = ev.newValue ?? "";
                if (!val.StartsWith("/")) { _slashPopup.Dismiss(); return; }

                var prefix = val.Substring(1);
                var matches = SlashRegistry.Match(prefix);
                if (matches.Count == 0) { _slashPopup.Dismiss(); return; }
                _slashPopup.Show(matches);
            });

            // Registered on the PARENT (_inputArea) at TrickleDown so it fires before
            // EnterKeySend's handler on the child _input — parent precedes child in
            // trickle-down regardless of registration order; StopPropagation here prevents
            // EnterKeySend from sending the raw slash text.
            _inputArea.RegisterCallback<KeyDownEvent>(ev =>
            {
                if (!_slashPopup.IsVisible) return;
                if (_mentionPopup != null && _mentionPopup.IsVisible) return;
                switch (ev.keyCode)
                {
                    case UnityEngine.KeyCode.DownArrow:
                        _slashPopup.MoveDown();   ev.StopPropagation(); break;
                    case UnityEngine.KeyCode.UpArrow:
                        _slashPopup.MoveUp();     ev.StopPropagation(); break;
                    case UnityEngine.KeyCode.Escape:
                        _slashPopup.Dismiss();    ev.StopPropagation(); break;
                    case UnityEngine.KeyCode.Return:
                    case UnityEngine.KeyCode.KeypadEnter:
                        _slashPopup.ApplySelected(); ev.StopPropagation(); break;
                }
            }, TrickleDown.TrickleDown);

            _input.RegisterCallback<BlurEvent>(_ =>
            {
                // Brief delay so click events on popup items fire before blur dismisses.
                _inputArea.schedule.Execute(_slashPopup.OnBlur).StartingIn(150);
            });
        }
    }
}
