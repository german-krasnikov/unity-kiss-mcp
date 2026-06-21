using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat.Annotation
{
    internal sealed class AnnotationToolbar
    {
        private readonly AnnotationToolState _state;
        private readonly AnnotationHistory _history;

        internal bool SendClicked    { get; private set; }
        internal bool ClearClicked   { get; private set; }
        internal bool CoordsEnabled  { get; set; } = true;

        internal AnnotationToolbar(AnnotationToolState state, AnnotationHistory history)
        {
            _state = state;
            _history = history;
        }

        internal void Draw()
        {
            SendClicked = ClearClicked = false;

            // Row 1: Tools + undo/redo/clear
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawToolButton(AnnotationIcons.Pen,     AnnotationTool.Pen,     "Pen (P)");
            DrawToolButton(AnnotationIcons.Line,    AnnotationTool.Line,    "Line (L)");
            DrawToolButton(AnnotationIcons.Arrow,   AnnotationTool.Arrow,   "Arrow (A)");
            DrawToolButton(AnnotationIcons.Rect,    AnnotationTool.Rect,    "Rectangle (R)");
            DrawToolButton(AnnotationIcons.Ellipse, AnnotationTool.Ellipse, "Ellipse (E)");
            DrawToolButton(AnnotationIcons.Text,    AnnotationTool.Text,    "Text (T)");
            DrawToolButton(AnnotationIcons.Erase,   AnnotationTool.Erase,   "Eraser (X)");
            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(!_history.CanUndo);
            if (GUILayout.Button(new GUIContent(AnnotationIcons.Undo, "Undo (Ctrl+Z)"), EditorStyles.toolbarButton, GUILayout.Width(24)))
                _history.Undo();
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!_history.CanRedo);
            if (GUILayout.Button(new GUIContent(AnnotationIcons.Redo, "Redo (Ctrl+Y)"), EditorStyles.toolbarButton, GUILayout.Width(24)))
                _history.Redo();
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button(new GUIContent(AnnotationIcons.Clear, "Clear all"), EditorStyles.toolbarButton))
                ClearClicked = true;

            EditorGUILayout.EndHorizontal();

            // Row 2: Color palette + width + fill + send
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            for (int i = 0; i < AnnotationToolState.Palette.Length; i++)
            {
                var c = AnnotationToolState.Palette[i];
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = c;
                bool selected = ColorsEqual(_state.ActiveColor, c);
                if (GUILayout.Toggle(selected, "", EditorStyles.toolbarButton, GUILayout.Width(20)) && !selected)
                    _state.ActiveColor = c;
                GUI.backgroundColor = prev;
            }

            GUILayout.Space(8);
            GUILayout.Label("Width:", EditorStyles.miniLabel, GUILayout.Width(36));
            for (int i = 0; i < AnnotationToolState.WidthPresets.Length; i++)
            {
                var w = AnnotationToolState.WidthPresets[i];
                bool sel = Mathf.Approximately(_state.StrokeWidth, w);
                var icon = i == 0 ? AnnotationIcons.WidthS : i == 1 ? AnnotationIcons.WidthM : AnnotationIcons.WidthL;
                var tip  = i == 0 ? "Thin" : i == 1 ? "Medium" : "Thick";
                if (GUILayout.Toggle(sel, new GUIContent(icon, tip), EditorStyles.toolbarButton, GUILayout.Width(22)) && !sel)
                    _state.StrokeWidth = w;
            }

            GUILayout.Space(8);
            if (_state.ActiveTool == AnnotationTool.Rect || _state.ActiveTool == AnnotationTool.Ellipse)
            {
                var fillOptions = new[] { "None", "Solid", "Semi" };
                int idx = (int)_state.FillMode;
                int newIdx = EditorGUILayout.Popup(idx, fillOptions, EditorStyles.toolbarPopup, GUILayout.Width(55));
                if (newIdx != idx) _state.FillMode = (AnnotationFill)newIdx;
            }

            GUILayout.FlexibleSpace();
            CoordsEnabled = GUILayout.Toggle(CoordsEnabled, new GUIContent(AnnotationIcons.Cube3D, "Show 3D coordinates"), EditorStyles.toolbarButton, GUILayout.Width(28));
            if (GUILayout.Button(new GUIContent(AnnotationIcons.Send, "Send to Chat (Ctrl+Enter)"), EditorStyles.toolbarButton))
                SendClicked = true;

            EditorGUILayout.EndHorizontal();
        }

        internal void HandleHotkeys(Event e)
        {
            if (e.type != EventType.KeyDown) return;
            switch (e.keyCode)
            {
                case KeyCode.P: _state.ActiveTool = AnnotationTool.Pen; e.Use(); break;
                case KeyCode.L: _state.ActiveTool = AnnotationTool.Line; e.Use(); break;
                case KeyCode.A: _state.ActiveTool = AnnotationTool.Arrow; e.Use(); break;
                case KeyCode.R: _state.ActiveTool = AnnotationTool.Rect; e.Use(); break;
                case KeyCode.E: _state.ActiveTool = AnnotationTool.Ellipse; e.Use(); break;
                case KeyCode.T: _state.ActiveTool = AnnotationTool.Text; e.Use(); break;
                case KeyCode.X: _state.ActiveTool = AnnotationTool.Erase; e.Use(); break;
                case KeyCode.Z when e.command || e.control:
                    if (e.shift) _history.Redo(); else _history.Undo();
                    e.Use(); break;
                case KeyCode.Y when e.command || e.control:
                    _history.Redo(); e.Use(); break;
                case KeyCode.Return when e.command || e.control:
                    SendClicked = true; e.Use(); break;
            }
        }

        private void DrawToolButton(Texture2D icon, AnnotationTool tool, string tooltip)
        {
            bool selected = _state.ActiveTool == tool;
            bool clicked = GUILayout.Toggle(selected, new GUIContent(icon, tooltip),
                EditorStyles.toolbarButton, GUILayout.Width(24));
            if (clicked && !selected) _state.ActiveTool = tool;
        }

        private static bool ColorsEqual(Color32 a, Color32 b)
            => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
