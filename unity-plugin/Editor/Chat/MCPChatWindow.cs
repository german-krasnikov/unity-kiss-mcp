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
        private bool           _agentMode, _waitingReply;
        private int            _dotCount, _inputTokens, _outputTokens;
        private float          _totalCostUsd;
        private readonly List<ChatEvent>       _evBuf   = new List<ChatEvent>(16);
        private readonly List<ToolCallRecord>  _toolBuf = new List<ToolCallRecord>(8);
        private TextField     _input;
        private Label         _modePill, _costBadge, _typingDots;
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
            root.Add(BuildToolbar());
            root.Add(BuildFlowBar());
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
            _typingDots = new Label("..."); _typingDots.AddToClassList("typing-dots");
            _typingDots.style.display = DisplayStyle.None;
            root.Add(_typingDots);
            _inputArea = BuildInputArea();
            ResetInputAreaHeight();
            root.Add(BuildResizeHandle(_inputArea));
            root.Add(_inputArea);
            SetupAutoHeight();
            root.schedule.Execute(DrainAndRender).Every(33);
            root.schedule.Execute(TickDots).Every(400);
            root.schedule.Execute(TickFlowBarSweep).Every(800);
            root.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            root.RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        private VisualElement BuildToolbar()
        {
            var bar   = new VisualElement(); bar.AddToClassList("chat-toolbar");
            var title = new Label("MCP Chat"); title.AddToClassList("chat-title");
            _modePill  = new Label("ASK"); _modePill.AddToClassList("mode-pill"); _modePill.AddToClassList("mode-pill--ask");
            _costBadge = new Label("");    _costBadge.AddToClassList("cost-badge");
            bar.Add(title); bar.Add(_modePill); bar.Add(_costBadge);
            return bar;
        }

        private VisualElement BuildInputArea()
        {
            var area = new VisualElement(); area.AddToClassList("input-area");
            _objChipStrip = new VisualElement(); _objChipStrip.AddToClassList("obj-chip-strip");
            area.Add(_objChipStrip);
            _input = new TextField { multiline = true }; _input.AddToClassList("chat-input");
            area.Add(_input);
            EnterKeySend.Attach(_input, OnSend);

            var actionBar  = new VisualElement(); actionBar.AddToClassList("input-actionbar");
            var modeGroup  = new VisualElement(); modeGroup.AddToClassList("mode-toggle-row");
            modeGroup.Add(MakeModeBtn("Ask", false)); modeGroup.Add(MakeModeBtn("Agent", true));
            actionBar.Add(modeGroup);
            var spacer = new VisualElement(); spacer.AddToClassList("actionbar-spacer");
            actionBar.Add(spacer);
            var ssBtn   = new Button(AttachScreenshot) { text = "SS", tooltip = "Attach 4-panel screenshot" };
            ssBtn.AddToClassList("chat-btn"); ssBtn.AddToClassList("chat-btn--screenshot");
            var sendBtn = new Button(OnSend) { text = "Send" };
            sendBtn.AddToClassList("chat-btn"); sendBtn.AddToClassList("chat-btn--send");
            actionBar.Add(ssBtn); actionBar.Add(sendBtn);
            area.Add(actionBar);
            return area;
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
            _modePill.text = agentMode ? "AGENT" : "ASK";
            _modePill.RemoveFromClassList("mode-pill--ask"); _modePill.RemoveFromClassList("mode-pill--agent");
            _modePill.AddToClassList(agentMode ? "mode-pill--agent" : "mode-pill--ask");
        }

        private void CreateBackend()
        {
            var cfg = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                ".claude", "mcp.json");
            _backend = new ClaudeBackend(cfg, _agentMode ? "acceptEdits" : "plan");
        }

        private void OnSend()
        {
            var text  = _input.value?.Trim() ?? "";
            var chips = CollectChipPaths();
            if (chips.Count > 0) text += "\n" + string.Join("\n", chips);
            if (string.IsNullOrEmpty(text)) return;
            _transcript.AppendUserBubble(text);
            _backend.SendTurn(UserTurnBuilder.Build(text));
            _input.value = ""; _objChipStrip.Clear();
            _heightCalc.Reset();
            ResetInputAreaHeight();
            _waitingReply = true; _typingDots.style.display = DisplayStyle.Flex;
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
            _input.value = ""; _objChipStrip.Clear();
            _heightCalc.Reset();
            ResetInputAreaHeight();
            _waitingReply = true; _typingDots.style.display = DisplayStyle.Flex;
            if (_activity.Send()) OnActivityChanged();
        }

        // Clears fixed height and restores min/max so flex layout sizes to content.
        private void ResetInputAreaHeight()
        {
            _inputArea.style.height    = StyleKeyword.Null;
            _inputArea.style.minHeight = InputHeightCalc.CompactH;
            _inputArea.style.maxHeight = _heightCalc.ComputeMax(position.height);
        }

        // Appends a reference path to the input field and focuses it.
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
