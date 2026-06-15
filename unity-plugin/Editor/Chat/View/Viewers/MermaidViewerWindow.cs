// EditorWindow for viewing Mermaid diagrams with zoom/pan support.
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class MermaidViewerWindow : EditorWindow
    {
        [SerializeField] private string[] _lines;
        private ZoomPanManipulator _zoomPan;

        internal static void Show(MermaidGraph graph, string[] lines)
        {
            if (graph == null) return;
            var w = GetWindow<MermaidViewerWindow>();
            w.titleContent = new GUIContent("Mermaid Viewer");
            w._lines = lines;
            w.BuildUI(graph);
        }

        private void OnEnable()
        {
            if (_lines == null || _lines.Length == 0) return;
            var graph = MermaidParser.TryParse(_lines);
            if (graph != null) BuildUI(graph);
        }

        private void BuildUI(MermaidGraph graph)
        {
            rootVisualElement.Clear();

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 24;

            var zoomLabel = new Label("100%");
            zoomLabel.style.width = 50;
            toolbar.Add(zoomLabel);

            toolbar.Add(new Button(() => { _zoomPan?.Reset(); zoomLabel.text = "100%"; }) { text = "1:1 Reset" });

            rootVisualElement.Add(toolbar);

            var viewport = new VisualElement();
            viewport.style.flexGrow = 1;
            viewport.style.overflow = Overflow.Hidden;

            var layout = MermaidLayout.Compute(graph);
            var content = new VisualElement();
            content.Add(new MermaidView(graph, layout));

            _zoomPan = new ZoomPanManipulator(content);
            viewport.AddManipulator(_zoomPan);
            viewport.Add(content);

            viewport.RegisterCallback<WheelEvent>(_ =>
                zoomLabel.text = $"{Mathf.RoundToInt(_zoomPan.Zoom * 100)}%");

            rootVisualElement.Add(viewport);
        }
    }
}
