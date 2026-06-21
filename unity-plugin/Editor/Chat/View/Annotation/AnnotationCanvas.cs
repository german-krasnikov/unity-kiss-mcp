using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace UnityMCP.Editor.Chat.Annotation
{
    internal sealed class AnnotationCanvas
    {
        private readonly AnnotationToolState _state;
        private readonly AnnotationHistory _history;
        private Texture2D _backgroundTex;
        private bool _isDragging;
        private Vector2 _dragStart;
        private List<Vector2> _penPoints;
        private string _textInput = "";
        private bool _waitingForText;
        private Vector2 _textPosition;

        internal AnnotationCanvas(AnnotationToolState state, AnnotationHistory history)
        {
            _state = state;
            _history = history;
        }

        internal void SetBackground(Texture2D tex) => _backgroundTex = tex;

        internal void Draw(Rect canvasRect)
        {
            if (_backgroundTex != null)
                GUI.DrawTexture(canvasRect, _backgroundTex, ScaleMode.ScaleToFit);

            DrawCommands(canvasRect, _history.Active);

            if (_isDragging && _state.ActiveTool != AnnotationTool.Pen && _state.ActiveTool != AnnotationTool.Erase)
            {
                var mouseNorm = AnnotationDrawer.PixelToNormalized(Event.current.mousePosition, canvasRect);
                DrawLivePreview(canvasRect, _dragStart, mouseNorm);
            }

            if (_isDragging && (_state.ActiveTool == AnnotationTool.Pen || _state.ActiveTool == AnnotationTool.Erase)
                && _penPoints != null)
            {
                Handles.BeginGUI();
                Handles.color = _state.ActiveColor;
                AnnotationDrawer.DrawPenTrail(canvasRect, _penPoints, _state.StrokeWidth);
                Handles.EndGUI();
            }

            if (_waitingForText)
                DrawTextInputPopup(canvasRect);

            HandleInput(canvasRect);
        }

        private void HandleInput(Rect canvasRect)
        {
            var e = Event.current;
            if (!canvasRect.Contains(e.mousePosition)) return;

            var norm = AnnotationDrawer.PixelToNormalized(e.mousePosition, canvasRect);

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    if (_state.ActiveTool == AnnotationTool.Text)
                    {
                        _waitingForText = true;
                        _textPosition = norm;
                        _textInput = "";
                    }
                    else
                    {
                        _isDragging = true;
                        _dragStart = norm;
                        if (_state.ActiveTool == AnnotationTool.Pen || _state.ActiveTool == AnnotationTool.Erase)
                            _penPoints = new List<Vector2> { norm };
                    }
                    e.Use();
                    break;

                case EventType.MouseDrag when _isDragging && e.button == 0:
                    if ((_state.ActiveTool == AnnotationTool.Pen || _state.ActiveTool == AnnotationTool.Erase)
                        && _penPoints != null)
                        _penPoints.Add(norm);
                    e.Use();
                    break;

                case EventType.MouseUp when _isDragging && e.button == 0:
                    _isDragging = false;
                    CommitCommand(norm);
                    e.Use();
                    break;
            }
        }

        private void CommitCommand(Vector2 endNorm)
        {
            IAnnotationCommand cmd = _state.ActiveTool switch
            {
                AnnotationTool.Pen => _penPoints != null && _penPoints.Count > 1
                    ? new PenCommand(_state.ActiveColor, _state.StrokeWidth, _penPoints) : null,
                AnnotationTool.Erase => _penPoints != null && _penPoints.Count > 0
                    ? new PenCommand(new Color32(0, 0, 0, 0), _state.StrokeWidth * 3, _penPoints) : null,
                AnnotationTool.Line    => new LineCommand(_state.ActiveColor, _state.StrokeWidth, _dragStart, endNorm),
                AnnotationTool.Arrow   => new ArrowCommand(_state.ActiveColor, _state.StrokeWidth, _dragStart, endNorm),
                AnnotationTool.Rect    => new RectCommand(_state.ActiveColor, _state.StrokeWidth, _state.FillMode, _dragStart, endNorm),
                AnnotationTool.Ellipse => new EllipseCommand(_state.ActiveColor, _state.StrokeWidth, _state.FillMode, _dragStart, endNorm),
                _ => null
            };
            _penPoints = null;
            if (cmd != null) _history.Add(cmd);
        }

        private void DrawTextInputPopup(Rect canvasRect)
        {
            var pos = AnnotationDrawer.NormalizedToPixel(_textPosition, canvasRect);
            _textInput = GUI.TextField(new Rect(pos.x, pos.y, 150, 20), _textInput);

            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return && !string.IsNullOrEmpty(_textInput))
            {
                _history.Add(new TextCommand(_state.ActiveColor, _textPosition, _textInput));
                _waitingForText = false;
                _textInput = "";
                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _waitingForText = false;
                _textInput = "";
                e.Use();
            }
        }

        private void DrawCommands(Rect canvasRect, IReadOnlyList<IAnnotationCommand> commands)
        {
            Handles.BeginGUI();
            for (int i = 0; i < commands.Count; i++)
                DrawCommand(canvasRect, commands[i]);
            Handles.EndGUI();
        }

        private void DrawCommand(Rect canvasRect, IAnnotationCommand cmd)
        {
            Handles.color = cmd.Color;
            switch (cmd.Tool)
            {
                case AnnotationTool.Pen:
                case AnnotationTool.Erase:
                    AnnotationDrawer.DrawPenTrail(canvasRect, cmd.Points, cmd.StrokeWidth);
                    break;
                case AnnotationTool.Line:
                    AnnotationDrawer.DrawLine(canvasRect, cmd.Points[0], cmd.Points[1], cmd.StrokeWidth);
                    break;
                case AnnotationTool.Arrow:
                    AnnotationDrawer.DrawLine(canvasRect, cmd.Points[0], cmd.Points[1], cmd.StrokeWidth);
                    AnnotationDrawer.DrawArrowhead(canvasRect, cmd.Points[0], cmd.Points[1], cmd.StrokeWidth);
                    break;
                case AnnotationTool.Rect:
                    AnnotationDrawer.DrawRect(canvasRect, cmd);
                    break;
                case AnnotationTool.Ellipse:
                    AnnotationDrawer.DrawEllipseOutline(canvasRect, cmd.Points[0], cmd.Points[1], cmd.StrokeWidth);
                    break;
                case AnnotationTool.Text:
                    var pos = AnnotationDrawer.NormalizedToPixel(cmd.Points[0], canvasRect);
                    var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = cmd.Color } };
                    GUI.Label(new Rect(pos.x, pos.y, 200, 20), cmd.Text, style);
                    break;
            }
        }

        private void DrawLivePreview(Rect canvasRect, Vector2 start, Vector2 end)
        {
            Handles.BeginGUI();
            Handles.color = _state.ActiveColor;
            switch (_state.ActiveTool)
            {
                case AnnotationTool.Line:
                    AnnotationDrawer.DrawLine(canvasRect, start, end, _state.StrokeWidth); break;
                case AnnotationTool.Arrow:
                    AnnotationDrawer.DrawLine(canvasRect, start, end, _state.StrokeWidth);
                    AnnotationDrawer.DrawArrowhead(canvasRect, start, end, _state.StrokeWidth); break;
                case AnnotationTool.Rect:
                    AnnotationDrawer.DrawRectOutline(canvasRect, start, end, _state.StrokeWidth); break;
                case AnnotationTool.Ellipse:
                    AnnotationDrawer.DrawEllipseOutline(canvasRect, start, end, _state.StrokeWidth); break;
            }
            Handles.EndGUI();
        }

        internal void Dispose()
        {
            if (_backgroundTex != null)
            {
                Object.DestroyImmediate(_backgroundTex);
                _backgroundTex = null;
            }
        }
    }
}
