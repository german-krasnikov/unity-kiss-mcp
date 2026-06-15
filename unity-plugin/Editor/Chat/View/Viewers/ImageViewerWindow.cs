// EditorWindow for viewing images with zoom/pan support.
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ImageViewerWindow : EditorWindow
    {
        [SerializeField] private string _imagePath;
        private Texture2D _texture;
        private ZoomPanManipulator _zoomPan;
        private VisualElement _content;

        internal static void Show(string path)
        {
            var w = GetWindow<ImageViewerWindow>();
            w.titleContent = new GUIContent("Image Viewer");
            w._imagePath = path;
            w.LoadImage();
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(_imagePath))
                LoadImage();
        }

        private void OnDisable()
        {
            if (_texture != null) DestroyImmediate(_texture);
        }

        private void LoadImage()
        {
            if (_texture != null) DestroyImmediate(_texture);
            if (string.IsNullOrEmpty(_imagePath)) return;
            if (!System.IO.File.Exists(_imagePath))
            {
                rootVisualElement.Clear();
                rootVisualElement.Add(new Label($"File not found:\n{_imagePath}"));
                return;
            }

            var bytes = System.IO.File.ReadAllBytes(_imagePath);
            _texture = new Texture2D(2, 2);
            _texture.LoadImage(bytes);

            rootVisualElement.Clear();
            BuildUI();
        }

        private void BuildUI()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 24;

            var zoomLabel = new Label("100%");
            zoomLabel.style.width = 50;
            toolbar.Add(zoomLabel);

            toolbar.Add(new Button(() => { _zoomPan?.Reset(); zoomLabel.text = "100%"; }) { text = "Fit" });
            toolbar.Add(new Button(() => { _zoomPan?.Reset(); zoomLabel.text = "100%"; }) { text = "1:1" });

            rootVisualElement.Add(toolbar);

            var viewport = new VisualElement();
            viewport.style.flexGrow = 1;
            viewport.style.overflow = Overflow.Hidden;

            _content = new VisualElement();
            var img = new Image { image = _texture, scaleMode = ScaleMode.ScaleToFit };
            _content.Add(img);

            _zoomPan = new ZoomPanManipulator(_content);
            viewport.AddManipulator(_zoomPan);
            viewport.Add(_content);

            viewport.RegisterCallback<WheelEvent>(_ =>
                zoomLabel.text = $"{Mathf.RoundToInt(_zoomPan.Zoom * 100)}%");

            rootVisualElement.Add(viewport);
        }
    }
}
