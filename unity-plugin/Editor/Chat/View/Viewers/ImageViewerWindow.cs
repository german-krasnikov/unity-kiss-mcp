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

        /// <summary>
        /// Compute pixel-perfect zoom: tex native pixels / viewport width.
        /// Returns 1f when either dimension is zero (safe fallback).
        /// Exposed internal for unit tests.
        /// </summary>
        internal static float CalcPixelRatio(int texWidth, float viewportWidth)
            => (texWidth > 0 && viewportWidth > 0) ? texWidth / viewportWidth : 1f;

        /// <summary>
        /// Action for the "1:1" button. Sets zoom to native pixel ratio (tex pixels / viewport pixels).
        /// Exposed internal for unit tests via ApplyOneToOne(manipulator, label, pixelRatio).
        /// </summary>
        internal static void ApplyOneToOne(ZoomPanManipulator zp, Label zoomLabel, float pixelRatio = 1f)
        {
            zp?.SetZoom(pixelRatio);
            if (zoomLabel != null) zoomLabel.text = $"{Mathf.RoundToInt(pixelRatio * 100)}%";
        }

        private void BuildUI()
        {
            var viewport = new VisualElement();

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 24;

            var zoomLabel = new Label("100%");
            zoomLabel.style.width = 50;
            toolbar.Add(zoomLabel);

            toolbar.Add(new Button(() => { _zoomPan?.Reset(); zoomLabel.text = "100%"; }) { text = "Fit" });
            toolbar.Add(new Button(() => ApplyOneToOne(_zoomPan, zoomLabel, CalcPixelRatio(_texture.width, viewport.resolvedStyle.width))) { text = "1:1" });

            rootVisualElement.Add(toolbar);
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
