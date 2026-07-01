using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.RegionTool
{
    internal sealed partial class SceneMcpOverlay
    {
        VisualElement BuildRegionModeRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            ModeBtn(row, RegionIcons.Lasso,  "Lasso",  "Freehand lasso (Q)",         DrawingModeId.Lasso);
            ModeBtn(row, RegionIcons.Rect,   "Rect",   "Rectangle selection (W)",    DrawingModeId.Rectangle);
            ModeBtn(row, RegionIcons.Circle, "Circle", "Circle selection (E)",        DrawingModeId.Circle);
            ModeBtn(row, RegionIcons.PbP,    "PbP",    "Point-by-point polygon (R)", DrawingModeId.PointByPoint);
            return row;
        }

        static void ModeBtn(VisualElement row, Texture2D icon, string label, string tip, DrawingModeId id)
        {
            var btn = new Button { tooltip = tip };
            btn.style.flexGrow       = 1;
            btn.style.flexDirection  = FlexDirection.Row;
            btn.style.alignItems     = Align.Center;
            btn.style.justifyContent = Justify.Center;

            var img = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
            img.style.width = img.style.height = 14;
            img.style.marginRight = 2;
            btn.Add(img);
            btn.Add(new Label(label) { style = { fontSize = 10 } });

            btn.clicked += () =>
            {
                if (ToolManager.activeToolType != typeof(SceneRegionTool))
                    ToolManager.SetActiveTool<SceneRegionTool>();
                SceneRegionTool.SetModeAction?.Invoke(id);
            };
            row.Add(btn);
        }

        VisualElement BuildAnnotationTypeRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            AnnotBtn(row, "Point", "Point annotation (1)", AnnotationModeId.Point);
            AnnotBtn(row, "Path",  "Polyline path (2)",    AnnotationModeId.Polyline);
            AnnotBtn(row, "Ruler", "Measurement (3)",      AnnotationModeId.Measurement);
            return row;
        }

        static void AnnotBtn(VisualElement row, string label, string tip, AnnotationModeId id)
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

        VisualElement BuildDetailRow()
        {
            var f = new EnumField("Detail", PolygonDetailSettings.Default);
            f.style.marginTop = 4;
            f.RegisterValueChangedCallback(evt =>
            {
                PolygonDetailSettings.Default = (PolygonDetailLevel)evt.newValue;
                SceneRegionTool.RequestResimplify?.Invoke();
            });
            return f;
        }

        VisualElement BuildSnapRow()
        {
            var toggle = new Toggle("Grid Snap");
            toggle.style.marginTop = 2;
            toggle.schedule.Execute(() =>
            {
                toggle.value = ActiveTool() == typeof(SceneAnnotationTool)
                    ? SceneAnnotationTool.GridSnap
                    : SceneRegionTool.GridSnap;
            }).Every(300);
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (ActiveTool() == typeof(SceneRegionTool))
                    SceneRegionTool.SetGridSnapAction?.Invoke(evt.newValue);
                else if (ActiveTool() == typeof(SceneAnnotationTool))
                    EditorPrefs.SetBool(PrefKeys.AnnotationGridSnap, evt.newValue);
            });
            return toggle;
        }

        TextField BuildLabelField()
        {
            var f = new TextField("Label");
            f.style.marginTop = 4;
            f.RegisterValueChangedCallback(evt => SceneAnnotationTool.PendingLabel = evt.newValue);
            return f;
        }

        void UpdateStats()
        {
            bool regionActive = ActiveTool() == typeof(SceneRegionTool);
            bool annotActive  = ActiveTool() == typeof(SceneAnnotationTool);

            bool canConfirm = regionActive
                ? (SceneRegionTool.CanConfirmQuery?.Invoke() ?? false)
                : annotActive && (SceneAnnotationTool.CanConfirmQuery?.Invoke() ?? false);

            bool canCommit = regionActive
                ? (SceneRegionTool.CanCommitQuery?.Invoke() ?? false)
                : annotActive && (SceneAnnotationTool.CanCommitQuery?.Invoke() ?? false);

            _confirmBtn.SetEnabled(canConfirm);
            _commitBtn.SetEnabled(canCommit);
            _cancelBtn.SetEnabled(regionActive || annotActive);

            if (regionActive)
            {
                var state = SceneRegionTool.PreviewState;
                if (state == null)
                {
                    _statsLabel.text         = $"Region | {SceneRegionTool.CurrentModeId} | Shift+R";
                    _warnLabel.style.display = DisplayStyle.None;
                }
                else
                {
                    int tokens = state.VertexCount * PolygonDetailConfig.TokensPerVertex;
                    _statsLabel.text = $"Area: {state.Area:F1}m²  Objects: {state.ObjectCount}\n" +
                                       $"Raw: {state.RawVertexCount} → Simplified: {state.VertexCount}  ~{tokens} tokens";
                    _warnLabel.style.display = state.VertexCount > PolygonDetailConfig.WarnVertexCount
                        ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
            else if (annotActive)
            {
                _statsLabel.text = SceneAnnotationTool.CurrentModeId switch
                {
                    AnnotationModeId.Point       => "Annotation | Point | Click to place",
                    AnnotationModeId.Polyline    => "Annotation | Path | Click vertices, Enter=Commit",
                    AnnotationModeId.Measurement => "Annotation | Ruler | Click A then B",
                    _                            => "Annotation | Shift+A"
                };
                _warnLabel.style.display = DisplayStyle.None;
            }
            else
            {
                _statsLabel.text         = "Shift+R = Region  |  Shift+A = Annotation";
                _warnLabel.style.display = DisplayStyle.None;
            }
        }
    }
}
