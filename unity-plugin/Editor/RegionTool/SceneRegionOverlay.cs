using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Dockable overlay for Region Selection tool.
    /// Shows mode buttons, detail dropdown, grid snap, stats, and "Add to Chat".
    /// </summary>
    [Overlay(typeof(SceneView), "MCP Region", defaultDisplay: false)]
    internal sealed class SceneRegionOverlay : Overlay
    {
        Label  _statsLabel;
        Label  _warnLabel;
        Button _addButton;

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            root.style.minWidth    = 200;
            root.style.paddingLeft = root.style.paddingRight  = 6;
            root.style.paddingTop  = root.style.paddingBottom = 4;

            root.Add(new Label("MCP Region Select")
                { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            root.Add(BuildModeRow());
            root.Add(BuildDetailRow());
            root.Add(BuildSnapToggle());

            _statsLabel = new Label("Shift+R to activate")
                { style = { marginTop = 6, whiteSpace = WhiteSpace.Normal } };
            root.Add(_statsLabel);

            _warnLabel = new Label("Warning: >128 verts — high token cost")
                { style = { color = Color.red, display = DisplayStyle.None, whiteSpace = WhiteSpace.Normal } };
            root.Add(_warnLabel);

            _addButton = new Button { text = "Add to Chat", tooltip = "Commit region as chat chip (Enter)" };
            _addButton.style.marginTop = 4;
            _addButton.style.display   = DisplayStyle.None;
            _addButton.clicked += () => SceneRegionTool.CommitAction?.Invoke();
            root.Add(_addButton);

            root.schedule.Execute(UpdateStats).Every(200);
            return root;
        }

        VisualElement BuildModeRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };

            void ModeBtn(string label, string tip, DrawingModeId id)
            {
                var btn = new Button { text = label, tooltip = tip };
                btn.style.flexGrow = 1;
                btn.clicked += () =>
                {
                    if (!(ToolManager.activeToolType == typeof(SceneRegionTool)))
                        ToolManager.SetActiveTool<SceneRegionTool>();
                    SceneRegionTool.SetModeAction?.Invoke(id);
                };
                row.Add(btn);
            }

            ModeBtn("Lasso", "Freehand lasso (Q)",         DrawingModeId.Lasso);
            ModeBtn("Rect",  "Rectangle selection (W)",    DrawingModeId.Rectangle);
            ModeBtn("Circle","Circle selection (E)",        DrawingModeId.Circle);
            ModeBtn("PbP",   "Point-by-point polygon (R)", DrawingModeId.PointByPoint);
            return row;
        }

        VisualElement BuildDetailRow()
        {
            var detailField = new EnumField("Detail", PolygonDetailSettings.Default);
            detailField.style.marginTop = 4;
            detailField.RegisterValueChangedCallback(evt =>
            {
                PolygonDetailSettings.Default = (PolygonDetailLevel)evt.newValue;
                SceneRegionTool.RequestResimplify?.Invoke();
            });
            return detailField;
        }

        VisualElement BuildSnapToggle()
        {
            var toggle = new Toggle("Grid Snap");
            toggle.style.marginTop = 2;
            toggle.value = SceneRegionTool.GridSnap;
            toggle.RegisterValueChangedCallback(evt =>
            {
                SceneRegionTool.SetGridSnapAction?.Invoke(evt.newValue);
            });
            // Keep in sync with tool state
            toggle.schedule.Execute(() => toggle.value = SceneRegionTool.GridSnap).Every(300);
            return toggle;
        }

        void UpdateStats()
        {
            var state = SceneRegionTool.PreviewState;
            if (state == null)
            {
                var mode = SceneRegionTool.CurrentModeId.ToString();
                _statsLabel.text         = $"Mode: {mode} | Shift+R to activate";
                _addButton.style.display = DisplayStyle.None;
                _warnLabel.style.display = DisplayStyle.None;
            }
            else
            {
                int tokens = state.VertexCount * PolygonDetailConfig.TokensPerVertex;
                _statsLabel.text =
                    $"Area: {state.Area:F1}m²  Objects: {state.ObjectCount}\n" +
                    $"Raw: {state.RawVertexCount} → Simplified: {state.VertexCount}\n" +
                    $"~{tokens} tokens";
                _addButton.style.display = DisplayStyle.Flex;
                _warnLabel.style.display = state.VertexCount > PolygonDetailConfig.WarnVertexCount
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
