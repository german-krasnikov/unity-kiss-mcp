// EditorWindow for 3D model preview: PreviewRenderUtility + IMGUI drag rotation/zoom.
// Must use IMGUI — PreviewRenderUtility.BeginPreview requires IMGUI context.
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ModelViewerWindow : EditorWindow
    {
        [SerializeField] private string _assetPath;

        private PreviewRenderUtility _pru;
        private GameObject           _previewGO;
        private float _rotX = 20f, _rotY = -30f, _dist = 3f;
        private Vector2 _lastMouse;
        private bool    _dragging;

        internal static void Show(string path)
        {
            var w = GetWindow<ModelViewerWindow>("Model Viewer");
            w.minSize = new Vector2(300, 300);
            w._assetPath = path;
            w.LoadAsset();
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(_assetPath))
                LoadAsset();
        }

        private void OnDisable()
        {
            // Halt rendering but keep PRU alive — window may re-enable (docked toggle).
            // Full cleanup happens in OnDestroy.
        }

        private void OnDestroy()
        {
            _pru?.Cleanup();
            _pru = null;
            if (_previewGO != null) DestroyImmediate(_previewGO);
        }

        private void LoadAsset()
        {
            _pru?.Cleanup();
            if (_previewGO != null) DestroyImmediate(_previewGO);

            _pru = new PreviewRenderUtility();
            _pru.camera.farClipPlane  = 100f;
            _pru.camera.nearClipPlane = 0.01f;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_assetPath);
            if (prefab == null) return;

            _previewGO = _pru.InstantiatePrefabInScene(prefab);
            FitCamera();
        }

        private void FitCamera()
        {
            if (_previewGO == null) return;
            var renderers = _previewGO.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            _dist = bounds.size.magnitude * 1.5f;
        }

        private void OnGUI()
        {
            HandleInput();

            if (_pru == null) { EditorGUILayout.HelpBox("Loading...", MessageType.Info); return; }

            var rect = new Rect(0, EditorStyles.toolbar.fixedHeight, position.width,
                position.height - EditorStyles.toolbar.fixedHeight);

            DrawToolbar();

            if (_previewGO == null)
            {
                GUI.Label(rect, "No mesh in asset", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _pru.BeginPreview(rect, GUIStyle.none);
            _pru.camera.transform.position =
                Quaternion.Euler(_rotX, _rotY, 0) * new Vector3(0, 0, _dist);
            _pru.camera.transform.LookAt(Vector3.zero);
            _pru.camera.Render();
            var tex = _pru.EndPreview();
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(System.IO.Path.GetFileName(_assetPath), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset", EditorStyles.toolbarButton))
            {
                _rotX = 20f; _rotY = -30f;
                FitCamera();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void HandleInput()
        {
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    _dragging = true;
                    _lastMouse = e.mousePosition;
                    e.Use();
                    break;

                case EventType.MouseUp when e.button == 0:
                    _dragging = false;
                    e.Use();
                    break;

                case EventType.MouseDrag when _dragging:
                    var delta = e.mousePosition - _lastMouse;
                    _lastMouse = e.mousePosition;
                    _rotY += delta.x * 0.5f;
                    _rotX -= delta.y * 0.5f;
                    Repaint();
                    e.Use();
                    break;

                case EventType.ScrollWheel:
                    _dist = Mathf.Clamp(_dist + e.delta.y * 0.1f, 0.1f, 50f);
                    Repaint();
                    e.Use();
                    break;
            }
        }

        // IAssetViewer adapter registered by AssetViewerFactory.
        internal sealed class ViewerAdapter : IAssetViewer
        {
            public void Show(string assetPath) => ModelViewerWindow.Show(assetPath);
        }
    }
}
