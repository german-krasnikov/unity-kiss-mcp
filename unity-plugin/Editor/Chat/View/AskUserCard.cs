// Interactive card for elicitation (AskUser). Parses questions[] and renders
// pill buttons (single-select), checkboxes (multi-select), or TextField (free text).
// Single-question single-select auto-submits on pill click (no Submit button shown).
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class AskUserCard : VisualElement
    {
        private bool _resolved;
        private readonly Button    _submit;
        private readonly Label     _answer;
        private readonly TextField _otherField;
        private readonly List<AskUserQuestionRow> _rows = new List<AskUserQuestionRow>();
        private readonly string _rawQuestionsJson;
        private readonly bool   _directAnswer;

        internal AskUserCard(string requestId, string rawRequestJson, Action<string> onSubmit,
            bool directAnswer = false)
        {
            AddToClassList("ask-card");
            style.marginTop = 4; style.marginBottom = 4;
            style.paddingTop = 8; style.paddingBottom = 8;
            style.paddingLeft = 8; style.paddingRight = 8;
            style.borderTopLeftRadius = style.borderTopRightRadius =
            style.borderBottomLeftRadius = style.borderBottomRightRadius = 4;
            style.backgroundColor = new UnityEngine.Color(0.39f, 0.51f, 0.78f, 0.10f);
            style.borderLeftWidth = 3;
            style.borderLeftColor = new UnityEngine.Color(0.376f, 0.647f, 0.98f);

            _directAnswer = directAnswer;
            _rawQuestionsJson = JsonHelper.ExtractArray(rawRequestJson, "questions") ?? "[]";

            System.Action submitAction = () => Submit(requestId, onSubmit);

            // Parse questions first (no autoSubmit callback yet) to detect layout
            ParseRows();
            bool autoSubmit = _rows.Count == 1 && _rows[0].IsSingleSelect;

            // If auto-submit, rebuild the single row with the submit callback wired
            if (autoSubmit)
            {
                Remove(_rows[0].Root);
                _rows.Clear();
                int pos = 0;
                var q = JsonArrayScan.ExtractNextObject(_rawQuestionsJson, ref pos);
                var row = new AskUserQuestionRow(0, q, submitAction);
                _rows.Add(row);
                Add(row.Root);
            }

            var otherLbl = new Label("Other:");
            otherLbl.style.marginTop = 8;
            otherLbl.style.color = new UnityEngine.Color(0.56f, 0.78f, 0.56f);
            otherLbl.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Italic;
            Add(otherLbl);
            _otherField = new TextField { multiline = true };
            _otherField.style.marginTop = 2;
            _otherField.style.minHeight = 40;
            _otherField.style.whiteSpace = WhiteSpace.Normal;
            Add(_otherField);

            _submit = new Button(submitAction) { text = "Submit" };
            _submit.userData = submitAction;
            _submit.AddToClassList("ask-card__submit");
            _submit.style.alignSelf = Align.FlexEnd;
            _submit.style.marginTop = 6;
            _submit.style.minWidth  = 80;
            if (autoSubmit) _submit.style.display = DisplayStyle.None;
            Add(_submit);

            // When auto-submit active, show Submit only when Other field has content
            if (autoSubmit)
            {
                _otherField.RegisterValueChangedCallback(evt =>
                    _submit.style.display = string.IsNullOrEmpty(evt.newValue)
                        ? DisplayStyle.None : DisplayStyle.Flex);
            }

            _answer = new Label();
            _answer.AddToClassList("ask-card__answer");
            _answer.style.display = DisplayStyle.None;
            _answer.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Italic;
            _answer.style.color = new UnityEngine.Color(0.67f, 0.67f, 0.67f);
            _answer.style.marginTop = 4;
            Add(_answer);
        }

        // ── parsing ──────────────────────────────────────────────────────────────

        private void ParseRows()
        {
            int pos = 0; int qIndex = 0; string qJson;
            while ((qJson = JsonArrayScan.ExtractNextObject(_rawQuestionsJson, ref pos)) != null)
            {
                var row = new AskUserQuestionRow(qIndex++, qJson);
                _rows.Add(row);
                Add(row.Root);
            }

            if (_rows.Count == 0)
            {
                var row = new AskUserQuestionRow(-1, null);
                _rows.Add(row);
                Add(row.Root);
            }
        }

        // ── submit ───────────────────────────────────────────────────────────────

        private void Submit(string requestId, Action<string> onSubmit)
        {
            if (_resolved) return;
            _resolved = true;

            var otherText = _otherField?.value;
            DisableAllInputs();
            _otherField?.SetEnabled(false);
            _submit.style.display = DisplayStyle.None;
            _answer.text = "Submitted";
            _answer.style.display = DisplayStyle.Flex;
            AddToClassList("ask-card--resolved");
            style.opacity = 0.7f;

            string responseJson;
            if (requestId?.StartsWith("codex:") == true)
            {
                var answersArray = !string.IsNullOrEmpty(otherText)
                    ? BuildCodexOtherAnswer(otherText)
                    : BuildCodexAnswersArray();
                responseJson = ControlResponseBuilder.CodexUserInputResponse(requestId, answersArray);
            }
            else if (_directAnswer)
                responseJson = !string.IsNullOrEmpty(otherText) ? BuildOtherAnswerJson(otherText) : BuildAnswersMapJson();
            else
            {
                if (!string.IsNullOrEmpty(otherText))
                    responseJson = ControlResponseBuilder.ElicitationHook(requestId, _rawQuestionsJson, freeResponse: otherText);
                else
                    responseJson = ControlResponseBuilder.ElicitationHook(requestId, _rawQuestionsJson, answersJson: BuildAnswersMapJson());
            }

            onSubmit?.Invoke(responseJson);
        }

        private string BuildAnswersMapJson()
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var row in _rows)
            {
                if (!first) sb.Append(',');
                first = false;
                var key = JsonHelper.EscapeJson(row.QuestionText ?? $"q{row.Index}");
                var val = JsonHelper.EscapeJson(row.GetValue());
                sb.Append($"\"{key}\":\"{val}\"");
            }
            sb.Append('}');
            return sb.ToString();
        }

        private string BuildOtherAnswerJson(string otherText)
        {
            var key = _rows.Count > 0 ? (_rows[0].QuestionText ?? "q0") : "q0";
            return $"{{\"{JsonHelper.EscapeJson(key)}\":\"{JsonHelper.EscapeJson(otherText)}\"}}";
        }

        private string BuildCodexAnswersArray()
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var row in _rows)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append($"{{\"answer\":\"{JsonHelper.EscapeJson(row.GetValue())}\"}}");
            }
            sb.Append(']');
            return sb.ToString();
        }

        private string BuildCodexOtherAnswer(string text) =>
            $"[{{\"answer\":\"{JsonHelper.EscapeJson(text)}\"}}]";

        private void DisableAllInputs() { foreach (var row in _rows) row.Disable(); }
    }
}
