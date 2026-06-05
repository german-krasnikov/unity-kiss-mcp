// InlineChipField: composed VisualElement for chip-at-front input.
// Layout: flex-row of [Pill0, Pill1, ..., TextField(flexGrow 1)].
// Pills are real layout children — no overlay, no pixel positioning.
// Eliminates P1 (misalignment) + P2 (vanish-on-type) by construction.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Composed input control: leading chip pills followed by a text field.
    /// Public API: AddChip, RemoveChipAt, ClearChips, Text, Model.
    /// </summary>
    internal sealed class InlineChipField : VisualElement
    {
        private readonly InlineChipModel _model = new InlineChipModel();
        private readonly TextField       _textField;

        internal InlineChipModel Model => _model;

        internal string Text
        {
            get => _textField.value;
            set => _textField.value = value;
        }

        internal TextField TextField => _textField;

        internal InlineChipField()
        {
            style.flexDirection = FlexDirection.Row;
            style.flexWrap      = Wrap.Wrap;
            style.alignItems    = Align.Center;

            _textField = new TextField { multiline = true };
            _textField.style.flexGrow   = 1;
            _textField.style.flexShrink = 1;
            Add(_textField);

            _textField.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
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
            _model.RemoveAt(index);
            RebuildPills();
        }

        internal void ClearChips()
        {
            _model.Clear();
            RemoveAllPills();
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
                Insert(childCount - 1, pill); // insert before TextField
            }
        }

        private void RemoveAllPills()
        {
            // TextField is always the last child; remove everything before it.
            while (childCount > 1)
                RemoveAt(0);
        }

        private void AttachContextMenu(VisualElement pill)
        {
            pill.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                int liveIndex = pill.userData is int idx ? idx : IndexOf(pill); // model index, not VE child order

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
