using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow : EditorWindow
    {
        private IChatBackend   _backend;
        private ChatTranscript _transcript;
        private bool           _agentMode;
        private PermissionConfig _permConfig = new PermissionConfig();
        private int            _inputTokens, _outputTokens;
        private readonly List<ChatEvent>       _evBuf   = new List<ChatEvent>(16);
        private readonly List<ToolCallRecord>  _toolBuf = new List<ToolCallRecord>(8);
        private TextField     _input;
        private Label         _tokenReadout;
        private Button        _askBtn, _agentBtn;
        private VisualElement _objChipStrip;
        private VisualElement _inputArea;
        private ScrollView    _scroll;
        private InputHeightCalc _heightCalc = new InputHeightCalc();

        [MenuItem("MCP/Chat", priority = 0)]
        public static void ShowWindow()
        {
            var w = GetWindow<MCPChatWindow>("MCP Chat");
            w.minSize = new Vector2(320, 400);
        }

        private void OnEnable()  => CreateBackend();
        private void OnDisable() { _backend?.Stop(); _backend = null; }

        private ChatRefResolver _resolver;

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.unity-mcp.editor/Editor/Chat/MCPChatWindow.uss");
            if (ss != null) root.styleSheets.Add(ss);
            root.AddToClassList("chat-root");
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.AddToClassList("chat-scroll");
            var inner = new VisualElement();
            _scroll.Add(inner);
            _resolver = new ChatRefResolver();
            _resolver.Refresh();
            var registry = ChatBlockRendererFactory.CreateDefault(_resolver, AddRefToContext);
            _transcript = new ChatTranscript(inner, registry);
            root.Add(_scroll);
            _inputArea = BuildInputArea();
            ResetInputAreaHeight();
            root.Add(BuildResizeHandle(_inputArea));
            root.Add(_inputArea);
            SetupAutoHeight();
            root.schedule.Execute(DrainAndRender).Every(33);
            root.schedule.Execute(TickFlowBarSweep).Every(950);
            root.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            root.RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        private VisualElement BuildInputArea()
        {
            var area = new VisualElement(); area.AddToClassList("input-area");
            area.Add(BuildFlowBar());
            _objChipStrip = new VisualElement(); _objChipStrip.AddToClassList("obj-chip-strip");
            area.Add(_objChipStrip);
            _input = new TextField { multiline = true }; _input.AddToClassList("chat-input");
            area.Add(_input);
            EnterKeySend.Attach(_input, OnSend);
            area.Add(BuildFooterBar());
            return area;
        }

        private VisualElement BuildFooterBar()
        {
            var bar = new VisualElement(); bar.AddToClassList("footer-bar");

            // Agent selector (left, shrinkable)
            var sel = BuildAgentSelector();
            sel.AddToClassList("footer-selector");
            bar.Add(sel);

            // Segmented mode toggle
            var seg = new VisualElement(); seg.AddToClassList("mode-segment");
            _askBtn   = MakeModeBtn("Ask",   false);
            _agentBtn = MakeModeBtn("Agent", true);
            seg.Add(_askBtn); seg.Add(_agentBtn);
            bar.Add(seg);

            bar.Add(BuildPermissionsButton());

            var spacer = new VisualElement(); spacer.AddToClassList("footer-spacer");
            bar.Add(spacer);

            _tokenReadout = new Label(""); _tokenReadout.AddToClassList("token-readout");
            bar.Add(_tokenReadout);

            var ssBtn   = new Button(AttachScreenshot) { text = "SS", tooltip = "Attach 4-panel screenshot" };
            ssBtn.AddToClassList("chat-btn"); ssBtn.AddToClassList("chat-btn--screenshot");
            var sendBtn = new Button(OnSend) { text = "Send" };
            sendBtn.AddToClassList("chat-btn"); sendBtn.AddToClassList("chat-btn--send");
            bar.Add(ssBtn); bar.Add(sendBtn);
            return bar;
        }

        private Button MakeModeBtn(string label, bool isAgent)
        {
            var btn = new Button(() => SetMode(isAgent)) { text = label };
            btn.AddToClassList("mode-toggle-btn");
            if (_agentMode == isAgent) btn.AddToClassList("mode-toggle-btn--active");
            return btn;
        }

        private void SetMode(bool agentMode)
        {
            if (_agentMode == agentMode) return;
            _agentMode = agentMode;
            _backend?.Stop(); CreateBackend();
            _askBtn?.EnableInClassList("mode-toggle-btn--active",   !agentMode);
            _agentBtn?.EnableInClassList("mode-toggle-btn--active", agentMode);
        }

        private void CreateBackend()
        {
            var cfg = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                ".claude", "mcp.json");
            _backend = new ClaudeBackend(cfg, _agentMode ? "acceptEdits" : "plan", _selectedAgent, _permConfig);
        }

        private void OnSend()
        {
            var text  = _input.value?.Trim() ?? "";
            var chips = CollectChipPaths();
            if (chips.Count > 0) text += "\n" + string.Join("\n", chips);
            if (string.IsNullOrEmpty(text)) return;
            _transcript.AppendUserBubble(text);
            _backend.SendTurn(UserTurnBuilder.Build(text));
            _input.value = ""; _input.cursorIndex = _input.selectIndex = 0;
            _objChipStrip.Clear();
            _heightCalc.Reset();
            ResetInputAreaHeight();
            if (_activity.Send()) OnActivityChanged();
        }

        private void AttachScreenshot()
        {
            var target = Selection.activeGameObject;
            if (target == null) { Debug.LogWarning("[MCP Chat] Select a GameObject first"); return; }
            var path = MultiViewCapture.CaptureToFile(target);
            if (string.IsNullOrEmpty(path)) { Debug.LogWarning("[MCP Chat] Screenshot failed"); return; }
            var bytes = File.ReadAllBytes(path);
            var text  = _input.value?.Trim() ?? "";
            var chips = CollectChipPaths();
            if (chips.Count > 0) text += "\n" + string.Join("\n", chips);
            _transcript.AppendUserBubble(text, path);
            _backend.SendTurn(UserTurnBuilder.Build(text, bytes));
            _input.value = ""; _input.cursorIndex = _input.selectIndex = 0;
            _objChipStrip.Clear();
            _heightCalc.Reset();
            ResetInputAreaHeight();
            if (_activity.Send()) OnActivityChanged();
        }

        private void ResetInputAreaHeight()
        {
            // Definite height (not minHeight) is required so flex-grow on .chat-input
            // has a parent size to grow into — minHeight is a floor and leaves a dead gap.
            _inputArea.style.height    = InputHeightCalc.CompactH;
            _inputArea.style.minHeight = StyleKeyword.Null;
            _inputArea.style.maxHeight = _heightCalc.ComputeMax(position.height);
        }

        private void AddRefToContext(string refPath)
        {
            if (string.IsNullOrEmpty(refPath)) return;
            var current = _input.value ?? "";
            var sep     = current.Length > 0 && !current.EndsWith(" ") ? " " : "";
            _input.value = current + sep + refPath + " ";
            _input.Focus();
            _input.SelectRange(_input.value.Length, _input.value.Length);
            UpdateAutoHeight();
        }
    }
}
