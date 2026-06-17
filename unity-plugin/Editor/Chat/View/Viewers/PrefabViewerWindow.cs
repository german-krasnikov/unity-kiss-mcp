// EditorWindow showing prefab thumbnail, name, component list, child count, Ping/Open buttons.
// UIElements only (no IMGUI). Domain-reload-safe via [SerializeField] _assetPath.
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class PrefabViewerWindow : EditorWindow
    {
        private const int MaxComponents = 8;

        [SerializeField] private string _assetPath;
        private PrefabPreviewLoader _loader;
        private Image _thumbnail;

        internal static void Show(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) { ClearIfOpen(); return; }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null) { ShowNotFound(assetPath); return; }

            var w = GetWindow<PrefabViewerWindow>("Prefab Viewer");
            w.minSize = new Vector2(300, 220);
            w._assetPath = assetPath;
            w.rootVisualElement.Clear();
            w._loader?.Cancel();

            var isVariant = PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant;
            var comps = prefab.GetComponents<Component>()
                              .Select(c => c?.GetType().Name ?? "?")
                              .ToArray();
            var childCount = prefab.transform.childCount;

            BuildUIForTest(w.rootVisualElement, prefab.name, isVariant, comps, childCount);
            w.WireButtons(prefab);

            // async thumbnail
            w._thumbnail = w.rootVisualElement.Q<Image>(name: "prefab-thumbnail");
            w._loader = new PrefabPreviewLoader(prefab, tex =>
            {
                if (w._thumbnail != null && tex != null)
                    w._thumbnail.image = tex;
            });
        }

        // Called from Show when asset is not found (null path or missing asset).
        internal static void ShowNotFound(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) { ClearIfOpen(); return; }
            var w = GetWindow<PrefabViewerWindow>("Prefab Viewer");
            w.rootVisualElement.Clear();
            BuildNotFoundUI(w.rootVisualElement, assetPath);
        }

        // Clear content of an already-open window; no-op if window is not open.
        private static void ClearIfOpen()
        {
            if (!HasOpenInstances<PrefabViewerWindow>()) return;
            var w = GetWindow<PrefabViewerWindow>("Prefab Viewer");
            w._loader?.Cancel();
            w._loader = null;
            w.rootVisualElement.Clear();
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(_assetPath))
                Show(_assetPath);
        }

        private void OnDisable()
        {
            _loader?.Cancel();
            _loader = null;
        }

        // ── Testable static helpers ──────────────────────────────────────────

        internal static void BuildUIForTest(VisualElement root, string name,
            bool isVariant, string[] components, int childCount)
        {
            root.style.flexDirection = FlexDirection.Row;
            root.style.paddingLeft = root.style.paddingRight =
                root.style.paddingTop = root.style.paddingBottom = new StyleLength(8);

            // Left: thumbnail placeholder
            var thumb = new Image { name = "prefab-thumbnail" };
            thumb.style.width  = 128;
            thumb.style.height = 128;
            thumb.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            root.Add(thumb);

            // Right: info column
            var col = new VisualElement();
            col.style.flexGrow = 1;
            col.style.paddingLeft = 8;
            root.Add(col);

            var displayName = isVariant ? $"{name} [Variant]" : name;
            col.Add(new Label(displayName) { name = "prefab-name" });

            var shown = Math.Min(components.Length, MaxComponents);
            for (int i = 0; i < shown; i++)
            {
                var lbl = new Label("· " + components[i]);
                lbl.AddToClassList("prefab-component");
                col.Add(lbl);
            }

            if (components.Length > MaxComponents)
            {
                var extra = components.Length - MaxComponents;
                col.Add(new Label($"…and {extra} more") { name = "prefab-more" });
            }

            col.Add(new Label($"Children: {childCount}") { name = "prefab-children" });

            // Buttons row
            var btns = new VisualElement();
            btns.style.flexDirection = FlexDirection.Row;
            btns.Add(new Button { text = "Ping", name = "btn-ping" });
            btns.Add(new Button { text = "Open", name = "btn-open" });
            col.Add(btns);
        }

        internal static void BuildNotFoundUI(VisualElement root, string assetPath)
        {
            root.Add(new Label($"Prefab not found: {assetPath}"));
        }

        // Wire button callbacks — called after BuildUIForTest in Show().
        private void WireButtons(UnityEngine.Object prefab)
        {
            rootVisualElement.Q<Button>("btn-ping").clicked +=
                () => { EditorGUIUtility.PingObject(prefab); Selection.activeObject = prefab; };
            rootVisualElement.Q<Button>("btn-open").clicked +=
                () => AssetDatabase.OpenAsset(prefab);
        }

        // C9/C10: IAssetViewer adapter — registered by AssetViewerFactory for .prefab.
        // Eliminates static OnNavigate field and multi-window nulling bug.
        internal sealed class ViewerAdapter : IAssetViewer
        {
            public void Show(string assetPath) => PrefabViewerWindow.Show(assetPath);
        }
    }
}
