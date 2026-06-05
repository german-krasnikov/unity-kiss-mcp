// InlineChipField: composed VisualElement for chip-at-top input.
// Layout: flex-column of [PillRow (hidden when empty), TextField(flexGrow 1)].
// TextField is always full-width; chips appear in a row above it.
// Eliminates P1 (misalignment) + P2 (vanish-on-type) + P3 (TextField shrink on chips).
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

        internal InlineChipModel Model => _model;
        internal int LastCursorPos => _lastCursorPos;

        internal string Text
        {
            get => _textField.value;
            set => _textField.value = value;
        }

        internal TextField TextField => _textField;

        private const string FocusedClass = "chat-input--focused";

        internal InlineChipField()
        {
            style.flexDirection = FlexDirection.Column;
            style.alignItems    = Align.Stretch;  // full-width children in column

            // Pill row: chips live here, above the text field. Hidden when empty.
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

            // Focus ring: toggle class on this outer container, not on __input.
            // FocusInEvent bubbles from any child (including the inner text element).
            _textField.RegisterCallback<FocusInEvent>(_ => AddToClassList(FocusedClass));
            _textField.RegisterCallback<FocusOutEvent>(_ =>
            {
                _lastCursorPos = System.Math.Clamp(
                    _textField.cursorIndex, 0, (_textField.value ?? "").Length);
                RemoveFromClassList(FocusedClass);
            });
        }

        // ── Public API ────────────────────────────────────────────────────────

        internal void AddChip(ChipData chip)
        {
            _model.Add(chip);
            RebuildPills(); // rebuild ensures all captured indices are correct
        }

        internal void RemoveChipAt(int index)
        {
            if (index < 0 || index >= _model.Count) return;
            RemoveMentionFromText("@" + _model.Chips[index].DisplayName);
            _model.RemoveAt(index);
            RebuildPills();
        }

        internal void ClearChips()
        {
            _model.Clear();
            _lastCursorPos = 0;
            RebuildPills(); // also hides _pillRow
        }

        /// <summary>Rebuild pill VEs from model — used after domain-reload chip restore.</summary>
        internal void RebuildFromModel()
        {
            RebuildPills();
        }

        // ── private ───────────────────────────────────────────────────────────

        /// <summary>Remove pill VEs (keep TextField), re-add from model with fresh closures.</summary>
        private void RebuildPills()
        {
            RemoveAllPills();
            for (int i = 0; i < _model.Count; i++)
            {
                int captured = i;
                var chip = _model.Chips[i];
                var pill = ChipPillFactory.Build(chip, onRemove: () => RemoveChipAt(captured));
                pill.userData = captured; // pin model index; refreshed on every rebuild
                AttachContextMenu(pill);
                _pillRow.Add(pill);
            }
            _pillRow.style.display = _model.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RemoveAllPills()
        {
            _pillRow.Clear();
        }

        private void AttachContextMenu(VisualElement pill)
        {
            pill.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                int liveIndex = pill.userData is int idx ? idx : _pillRow.IndexOf(pill); // model index, not VE child order

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

        private void RemoveMentionFromText(string mention)
        {
            var text = _textField.value ?? "";
            var withSpace = mention + " ";
            var idx = text.IndexOf(withSpace, System.StringComparison.Ordinal);
            if (idx >= 0) { _textField.value = text.Remove(idx, withSpace.Length); return; }
            idx = text.IndexOf(mention, System.StringComparison.Ordinal);
            if (idx >= 0) _textField.value = text.Remove(idx, mention.Length);
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
