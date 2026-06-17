// EditorWindow for sprite preview: UIToolkit Image + ZoomPanManipulator.
// Falls back to ImageViewerWindow when the asset isn't imported as Sprite.
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class SpriteViewerWindow : EditorWindow
    {
        [SerializeField] private string _assetPath;
        private Sprite _sprite;
        private ZoomPanManipulator _zoomPan;

        internal static void Show(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                // Not imported as sprite — fall back to texture viewer.
                ImageViewerWindow.Show(path);
                return;
            }

            var w = GetWindow<SpriteViewerWindow>("Sprite Viewer");
            w.minSize = new Vector2(200, 200);
            w._assetPath = path;
            w._sprite = sprite;
            w.BuildUI();
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_assetPath)) return;
            _sprite = AssetDatabase.LoadAssetAtPath<Sprite>(_assetPath);
            if (_sprite != null) BuildUI();
        }

        private void BuildUI()
        {
            rootVisualElement.Clear();

            // Toolbar
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 24;
            var zoomLabel = new Label("100%");
            zoomLabel.style.width = 50;
            toolbar.Add(zoomLabel);
            toolbar.Add(new Button(() => { _zoomPan?.Reset(); zoomLabel.text = "100%"; }) { text = "Fit" });
            rootVisualElement.Add(toolbar);

            // Viewport
            var viewport = new VisualElement();
            viewport.style.flexGrow = 1;
            viewport.style.overflow = Overflow.Hidden;

            var content = new VisualElement();
            var img = new Image { sprite = _sprite, scaleMode = ScaleMode.ScaleToFit };
            content.Add(img);

            _zoomPan = new ZoomPanManipulator(content);
            viewport.AddManipulator(_zoomPan);
            viewport.Add(content);

            viewport.RegisterCallback<WheelEvent>(_ =>
                zoomLabel.text = $"{Mathf.RoundToInt(_zoomPan.Zoom * 100)}%");

            rootVisualElement.Add(viewport);
        }

        // IAssetViewer adapter registered by AssetViewerFactory.
        internal sealed class ViewerAdapter : IAssetViewer
        {
            public void Show(string assetPath) => SpriteViewerWindow.Show(assetPath);
        }
    }
}
