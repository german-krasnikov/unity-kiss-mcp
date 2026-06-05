// InlineChipField: composed VisualElement for chip-at-top input.
// Layout: flex-column of [PillRow (hidden when empty), TextField(flexGrow 1)].
// TextField is always full-width; chips appear in a row above it.
// Eliminates P1 (misalignment) + P2 (vanish-on-type) + P3 (TextField shrink on chips).
// Chips are position-tracked; @mention injected into TextField for visual display.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Composed input control: chip pills row above a full-width text field.
    /// TextField always occupies full width regardless of chip count.
    /// Public API: AddChip, RemoveChipAt, ClearChips, Text, Model.
    /// </summary>
    internal sealed class InlineChipField : VisualElement
    {
        private readonly InlineChipModel _model    = new InlineChipModel();
        private readonly VisualElement   _pillRow;
        private readonly TextField       _textField;
        private int _lastCursorPos;
        private bool _suppressOffsetUpdate;

        internal InlineChipModel Model => _model;
        internal int LastCursorPos => _lastCursorPos;

        internal string Text
        {
            get => _textField.value;
            set
            {
                _suppressOffsetUpdate = true;
                _textField.value = value;
                _suppressOffsetUpdate = false;
            }
        }

        internal TextField TextField => _textField;

        private const string FocusedClass = "chat-input--focused";

        internal InlineChipField()
        {
            style.flexDirection = FlexDirection.Column;
            style.alignItems    = Align.Stretch;

            _pillRow = new VisualElement();
            _pillRow.style.flexDirection = FlexDirection.Row;
            _pillRow.style.flexWrap      = Wrap.Wrap;
            _pillRow.style.paddingBottom = 3;
            _pillRow.style.display       = DisplayStyle.None;
            Add(_pillRow);

            _textField = new TextField { multiline = true };
            _textField.style.flexGrow   = 1;
            _textField.style.flexShrink = 1;
            Add(_textField);

            _textField.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            _textField.RegisterCallback<FocusInEvent>(_ => AddToClassList(FocusedClass));
            _textField.RegisterCallback<FocusOutEvent>(_ =>
            {
                _lastCursorPos = System.Math.Clamp(
                    _textField.cursorIndex, 0, (_textField.value ?? "").Length);
                RemoveFromClassList(FocusedClass);
            });

            // Track text changes to keep chip positions valid.
            _textField.RegisterValueChangedCallback(evt =>
            {
                if (_suppressOffsetUpdate) return;
                var prev  = evt.previousValue ?? "";
                var next  = evt.newValue      ?? "";
                int delta = next.Length - prev.Length;
                if (delta == 0) return;
                // Heuristic: cursor after change reflects insertion point.
                int changeAt = delta > 0
                    ? _textField.cursorIndex - delta
                    : _textField.cursorIndex;
                changeAt = System.Math.Max(0, changeAt);
                _model.AdjustOffsetsAfterTextChange(changeAt, delta);
            });
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Add chip at current cursor position (or last known cursor if field is unfocused).</summary>
        internal void AddChip(ChipData chip)
        {
            var tf     = _textField;
            int raw    = tf.cursorIndex;
            int len    = (tf.value ?? "").Length;
            int cursor = (raw == 0 && len > 0)
                ? System.Math.Clamp(_lastCursorPos, 0, len)
                : System.Math.Clamp(raw, 0, len);

            string val = tf.value ?? "";
            bool needsLeadingSpace = cursor > 0 && val.Length > 0 && val[cursor - 1] != ' ';
            // Shift existing chips at >= cursor to make room for @mention text.
            int mentionLen = 1 + chip.DisplayName.Length + 1 + (needsLeadingSpace ? 1 : 0);
            _model.AdjustOffsetsAfterTextChangeInclusive(cursor, mentionLen);
            _model.InsertAt(cursor + (needsLeadingSpace ? 1 : 0), chip);
            InjectMentionAt(cursor, chip, needsLeadingSpace);
            // Advance last-known cursor past the injected mention so the next
            // chip insertion appends after this one rather than prepending again.
            _lastCursorPos = cursor + mentionLen;
            RebuildPills();
        }

        /// <summary>Insert chip at an explicit text offset.</summary>
        internal void InsertChipAt(int textOffset, ChipData chip)
        {
            string val = _textField.value ?? "";
            bool needsLeadingSpace = textOffset > 0 && val.Length > 0 && val[textOffset - 1] != ' ';
            int mentionLen = 1 + chip.DisplayName.Length + 1 + (needsLeadingSpace ? 1 : 0);
            _model.AdjustOffsetsAfterTextChangeInclusive(textOffset, mentionLen);
            _model.InsertAt(textOffset + (needsLeadingSpace ? 1 : 0), chip);
            InjectMentionAt(textOffset, chip, needsLeadingSpace);
            RebuildPills();
        }

        internal void RemoveChipAt(int index)
        {
            if (index < 0 || index >= _model.Count) return;
            var pc         = _model.PositionedChips[index];
            int offset     = pc.TextOffset;
            string mention = "@" + pc.Chip.DisplayName + " ";
            RemoveMentionText(offset, mention);
            _model.RemoveAt(index);
            // Shift remaining chips that follow the removed mention.
            _model.AdjustOffsetsAfterTextChange(offset + mention.Length - 1, -mention.Length);
            RebuildPills();
        }

        internal void ClearChips()
        {
            _model.Clear();
            _lastCursorPos = 0;
            RebuildPills();
        }

        /// <summary>Rebuild pill VEs from model — used after domain-reload chip restore.</summary>
        internal void RebuildFromModel()
        {
            RebuildPills();
        }

        // ── private ───────────────────────────────────────────────────────────

        private void RebuildPills()
        {
            RemoveAllPills();
            for (int i = 0; i < _model.Count; i++)
            {
                int captured = i;
                var chip = _model.PositionedChips[i].Chip;
                var pill = ChipPillFactory.Build(chip, onRemove: () => RemoveChipAt(captured));
                pill.userData = captured;
                AttachContextMenu(pill);
                _pillRow.Add(pill);
            }
            _pillRow.style.display = _model.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RemoveAllPills()
        {
            _pillRow.Clear();
        }

        private void InjectMentionAt(int insertAt, ChipData chip, bool prependSpace)
        {
            string mention = "@" + chip.DisplayName + " ";
            if (prependSpace) mention = " " + mention;
            _suppressOffsetUpdate = true;
            string val = _textField.value ?? "";
            insertAt   = System.Math.Clamp(insertAt, 0, val.Length);
            _textField.value = val.Substring(0, insertAt) + mention + val.Substring(insertAt);
            _suppressOffsetUpdate = false;
        }

        private void RemoveMentionText(int offset, string mention)
        {
            string val = _textField.value ?? "";
            int clamped = System.Math.Clamp(offset, 0, val.Length);
            if (clamped + mention.Length <= val.Length
                && val.Substring(clamped, mention.Length) == mention)
            {
                _suppressOffsetUpdate = true;
                _textField.value = val.Substring(0, clamped)
                    + val.Substring(clamped + mention.Length);
                _suppressOffsetUpdate = false;
            }
        }

        private void AttachContextMenu(VisualElement pill)
        {
            pill.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                int liveIndex = pill.userData is int idx ? idx : _pillRow.IndexOf(pill);

                evt.menu.AppendAction("Show LLM payload", _ =>
                {
                    if (liveIndex < 0 || liveIndex >= _model.Count) return;
                    var chip    = _model.Chips[liveIndex];
                    var cfg     = BackendConfigStore.Load().Chips;
                    var payload = ChipContextResolver.ResolveAllTyped(new List<ChipData> { chip }, cfg);
                    Debug.Log($"[MCP Chat] LLM payload for chip [{chip.KindKey}:{chip.Path}]:\n{payload}");
                });

                evt.menu.AppendAction("Copy path", _ =>
                {
                    if (liveIndex < 0 || liveIndex >= _model.Count) return;
                    EditorGUIUtility.systemCopyBuffer = _model.Chips[liveIndex].Path;
                });

                evt.menu.AppendAction("Remove", _ =>
                {
                    if (liveIndex >= 0 && liveIndex < _model.Count)
                        RemoveChipAt(liveIndex);
                });
            }));
        }

        // Backspace at TextField caret position 0 removes the last chip.
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Backspace || _model.Count == 0) return;
            var sel = _textField.textSelection;
            if ((sel?.cursorIndex ?? -1) != 0) return;
            RemoveChipAt(_model.Count - 1);
            evt.StopPropagation();
        }
    }
}
