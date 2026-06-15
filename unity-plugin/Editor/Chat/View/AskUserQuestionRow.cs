// One parsed question row: single-select (pill buttons), multi-select (toggles),
// or free-text (TextField). Auto-submits on pill click when _onAutoSubmit != null.
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class AskUserQuestionRow
    {
        internal readonly int    Index;
        internal readonly string QuestionText;
        internal readonly VisualElement Root;
        internal bool IsSingleSelect => _mode == Mode.SingleSelect;

        private readonly List<Button> _pillButtons  = new List<Button>();
        private int _selectedIndex = -1;

        private readonly List<Toggle> _checkToggles = new List<Toggle>();
        private TextField _textField;

        private enum Mode { SingleSelect, MultiSelect, FreeText }
        private readonly Mode _mode;

        private readonly Action _onAutoSubmit;

        internal AskUserQuestionRow(int index, string qJson, Action onAutoSubmit = null)
        {
            Index         = index < 0 ? 0 : index;
            Root          = new VisualElement();
            Root.style.marginBottom = 8;
            _onAutoSubmit = onAutoSubmit;

            if (string.IsNullOrEmpty(qJson))
            {
                QuestionText = null;
                _mode = Mode.FreeText;
                BuildFreeText();
                return;
            }

            var question = JsonHelper.ExtractString(qJson, "question");
            QuestionText = question;
            var header   = JsonHelper.ExtractString(qJson, "header");
            var multiStr = JsonHelper.ExtractString(qJson, "multiSelect");
            bool multi   = string.Equals(multiStr, "true", StringComparison.OrdinalIgnoreCase);
            var optArray = JsonHelper.ExtractArray(qJson, "options");
            bool hasOpts = !string.IsNullOrEmpty(optArray) && optArray != "[]";

            if (!string.IsNullOrEmpty(header))
            {
                var hdr = new Label(header);
                hdr.AddToClassList("ask-card__header");
                hdr.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                hdr.style.fontSize = 12;
                hdr.style.marginBottom = 2;
                Root.Add(hdr);
            }

            if (!string.IsNullOrEmpty(question))
            {
                var ql = new Label(question);
                ql.AddToClassList("ask-card__question");
                ql.style.whiteSpace = WhiteSpace.Normal;
                ql.style.marginBottom = 6;
                Root.Add(ql);
            }

            if (!hasOpts)        { _mode = Mode.FreeText;     BuildFreeText(); }
            else if (multi)      { _mode = Mode.MultiSelect;  BuildCheckPills(optArray); }
            else                 { _mode = Mode.SingleSelect; BuildPillButtons(optArray); }
        }
        private void BuildFreeText()
        {
            _textField = new TextField();
            _textField.AddToClassList("ask-card__textfield");
            _textField.style.marginTop = 4;
            Root.Add(_textField);
        }

        private void BuildPillButtons(string optArray)
        {
            var group = new VisualElement();
            group.AddToClassList("ask-card__radio-group");
            group.style.flexDirection = FlexDirection.Column;
            group.style.marginLeft   = 8;

            int optPos = 0; string optJson; int i = 0;
            while ((optJson = JsonArrayScan.ExtractNextObject(optArray, ref optPos)) != null)
            {
                var label = JsonHelper.ExtractString(optJson, "label");
                var desc  = JsonHelper.ExtractString(optJson, "description");
                int captured = i;
                var pill = new Button { text = label ?? $"Option {i}" };
                pill.AddToClassList("ask-pill");
                pill.style.alignSelf = Align.Stretch;
                if (!string.IsNullOrEmpty(desc)) pill.tooltip = desc;
                StyleAsPill(pill);
                ApplyTransitions(pill);
                AddHoverEffect(pill, captured);
                // userData exposes click for tests (avoids RegisterCallback indirection)
                Action clickAction = () => OnPillClicked(captured);
                pill.userData = clickAction;
                pill.clicked += clickAction;
                _pillButtons.Add(pill);
                group.Add(pill);
                i++;
            }
            Root.Add(group);
        }

        private static void ApplyTransitions(Button btn)
        {
            btn.style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> {
                new StylePropertyName("background-color"), new StylePropertyName("border-color"), new StylePropertyName("scale") });
            btn.style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> {
                new TimeValue(200, TimeUnit.Millisecond), new TimeValue(200, TimeUnit.Millisecond), new TimeValue(150, TimeUnit.Millisecond) });
        }

        private void AddHoverEffect(Button pill, int captured)
        {
            pill.RegisterCallback<MouseEnterEvent>(e =>
            {
                if (captured != _selectedIndex)
                {
                    pill.style.backgroundColor = new UnityEngine.Color(0.35f, 0.48f, 0.72f, 0.35f);
                    pill.style.scale = new Scale(new UnityEngine.Vector3(1.03f, 1.03f, 1f));
                }
            });
            pill.RegisterCallback<MouseLeaveEvent>(e =>
            {
                if (captured != _selectedIndex)
                {
                    StyleAsPill(pill);
                    pill.style.scale = new Scale(UnityEngine.Vector3.one);
                }
            });
        }

        private void BuildCheckPills(string optArray)
        {
            var group = new VisualElement();
            group.AddToClassList("ask-card__checkbox-group");
            group.style.flexDirection = FlexDirection.Row;
            group.style.flexWrap     = Wrap.Wrap;
            group.style.marginLeft   = 8;

            int optPos = 0; string optJson; int i = 0;
            while ((optJson = JsonArrayScan.ExtractNextObject(optArray, ref optPos)) != null)
            {
                var label  = JsonHelper.ExtractString(optJson, "label");
                var desc   = JsonHelper.ExtractString(optJson, "description");
                var toggle = new Toggle(label ?? $"Option {i}");
                toggle.style.marginBottom = 4;
                toggle.style.marginRight  = 6;
                if (!string.IsNullOrEmpty(desc)) toggle.tooltip = desc;
                _checkToggles.Add(toggle);
                group.Add(toggle);
                i++;
            }
            Root.Add(group);
        }
        private void OnPillClicked(int index)
        {
            _selectedIndex = index;
            for (int k = 0; k < _pillButtons.Count; k++)
                StylePillSelected(_pillButtons[k], k == index);
            _onAutoSubmit?.Invoke();
        }
        internal string GetValue()
        {
            if (_mode == Mode.FreeText)     return _textField?.value ?? "";
            if (_mode == Mode.SingleSelect) return (_selectedIndex < 0 || _selectedIndex >= _pillButtons.Count) ? "" : _pillButtons[_selectedIndex].text ?? "";
            var parts = new List<string>();
            foreach (var t in _checkToggles) if (t.value) parts.Add(t.label ?? "");
            return string.Join(", ", parts);
        }

        internal void Disable()
        {
            _textField?.SetEnabled(false);
            foreach (var b in _pillButtons)  b.SetEnabled(false);
            foreach (var t in _checkToggles) t.SetEnabled(false);
        }
        private static void StyleAsPill(Button btn)
        {
            btn.style.paddingLeft = 12; btn.style.paddingRight  = 12;
            btn.style.paddingTop  = 6;  btn.style.paddingBottom = 6;
            btn.style.marginRight = 6;  btn.style.marginBottom  = 4;
            btn.style.borderTopLeftRadius    = btn.style.borderTopRightRadius    =
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 12;
            btn.style.borderTopWidth    = btn.style.borderBottomWidth =
            btn.style.borderLeftWidth   = btn.style.borderRightWidth  = 1;
            var bc = new UnityEngine.Color(0.45f, 0.60f, 0.85f, 0.6f);
            btn.style.borderTopColor    = btn.style.borderBottomColor =
            btn.style.borderLeftColor   = btn.style.borderRightColor  = bc;
            btn.style.backgroundColor   = new UnityEngine.Color(0.25f, 0.35f, 0.55f, 0.15f);
            btn.style.color             = new UnityEngine.Color(0.85f, 0.90f, 1.0f);
            btn.style.fontSize          = 12;
            btn.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Normal;
            btn.style.scale = new Scale(UnityEngine.Vector3.one);
        }

        private static void StylePillSelected(Button btn, bool selected)
        {
            if (selected)
            {
                btn.style.backgroundColor = new UnityEngine.Color(0.29f, 0.56f, 0.89f, 0.5f);
                var sc = new UnityEngine.Color(0.376f, 0.647f, 0.98f);
                btn.style.borderTopColor    = btn.style.borderBottomColor =
                btn.style.borderLeftColor   = btn.style.borderRightColor  = sc;
                btn.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            }
            else StyleAsPill(btn);
        }
    }
}
