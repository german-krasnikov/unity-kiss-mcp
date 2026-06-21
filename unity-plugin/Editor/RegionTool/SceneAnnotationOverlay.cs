using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Dockable overlay for the Annotation tool.
    /// Shows type buttons, label input, grid snap toggle, live stats, and "Add to Chat".
    /// </summary>
    [Overlay(typeof(SceneView), "MCP Annotations", defaultDisplay: false)]
    internal sealed class SceneAnnotationOverlay : Overlay
    {
        TextField _labelField;
        Label     _statsLabel;
        Button    _addButton;

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            root.style.minWidth    = 210;
            root.style.paddingLeft = root.style.paddingRight  = 6;
            root.style.paddingTop  = root.style.paddingBottom = 4;

            root.Add(new Label("MCP Annotations")
                { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            root.Add(BuildTypeRow());
            root.Add(BuildLabelField());
            root.Add(BuildSnapToggle());

            _statsLabel = new Label("Shift+A to activate")
                { style = { marginTop = 6, whiteSpace = WhiteSpace.Normal } };
            root.Add(_statsLabel);

            _addButton = new Button { text = "+ Add to Chat", tooltip = "Commit annotation (Enter)" };
            _addButton.style.marginTop = 4;
            _addButton.clicked += () => SceneAnnotationTool.CommitAction?.Invoke();
            root.Add(_addButton);

            root.schedule.Execute(UpdateStats).Every(200);
            return root;
        }

        VisualElement BuildTypeRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };

            void TypeBtn(string label, string tip, AnnotationModeId id)
            {
                var btn = new Button { text = label, tooltip = tip };
                btn.style.flexGrow = 1;
                btn.clicked += () =>
                {
                    if (ToolManager.activeToolType != typeof(SceneAnnotationTool))
                        ToolManager.SetActiveTool<SceneAnnotationTool>();
                    SceneAnnotationTool.SetModeAction?.Invoke(id);
                };
                row.Add(btn);
            }

            TypeBtn("⊙ Point",   "Point annotation (1)", AnnotationModeId.Point);
            TypeBtn("─ Path",    "Polyline path (2)",     AnnotationModeId.Polyline);
            TypeBtn("↔ Ruler",   "Measurement (3)",       AnnotationModeId.Measurement);
            return row;
        }

        VisualElement BuildLabelField()
        {
            _labelField = new TextField("Label");
            _labelField.style.marginTop = 4;
            _labelField.RegisterValueChangedCallback(evt =>
                SceneAnnotationTool.PendingLabel = evt.newValue);
            return _labelField;
        }

        VisualElement BuildSnapToggle()
        {
            var toggle = new Toggle("Grid Snap");
            toggle.style.marginTop = 2;
            toggle.value = SceneAnnotationTool.GridSnap;
            toggle.RegisterValueChangedCallback(evt =>
            {
                // Sync via EditorPrefs; tool reads on activate, UI syncs on schedule
                EditorPrefs.SetBool("MCP_AnnotSnap", evt.newValue);
            });
            toggle.schedule.Execute(() => toggle.value = SceneAnnotationTool.GridSnap).Every(300);
            return toggle;
        }

        void UpdateStats()
        {
            var mode = SceneAnnotationTool.CurrentModeId;
            _statsLabel.text = mode switch
            {
                AnnotationModeId.Point       => "Mode: Point | Click to place",
                AnnotationModeId.Polyline    => "Mode: Path | Click vertices, Enter=Commit",
                AnnotationModeId.Measurement => "Mode: Ruler | Click A then B",
                _                            => "Shift+A to activate"
            };
        }
    }
}
