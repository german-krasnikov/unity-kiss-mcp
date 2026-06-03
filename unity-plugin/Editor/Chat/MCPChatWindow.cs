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
        private readonly List<ChatEvent> _evBuf  = new List<ChatEvent>(16);
        private readonly List<string>    _pending = new List<string>();
        private TextField     _input;
        private Label         _modePill, _costBadge, _typingDots;
        private VisualElement _objChipStrip;
        private ScrollView    _scroll;

        [MenuItem("Tools/Unity MCP/Chat")]
        public static void ShowWindow()
        {
            var w = GetWindow<MCPChatWindow>("MCP Chat");
            w.minSize = new Vector2(320, 400);
        }

        private void OnEnable()  => CreateBackend();
        private void OnDisable() { _backend?.Stop(); _backend = null; }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.unity-mcp.editor/Editor/Chat/MCPChatWindow.uss");
            if (ss != null) root.styleSheets.Add(ss);
            root.AddToClassList("chat-root");
            root.Add(BuildToolbar());
            _scroll = new ScrollView(); _scroll.AddToClassList("chat-scroll");
            var inner = new VisualElement();
            _scroll.Add(inner);
            _transcript = new ChatTranscript(inner);
            root.Add(_scroll);
            _typingDots = new Label("..."); _typingDots.AddToClassList("typing-dots");
            _typingDots.style.display = DisplayStyle.None;
            root.Add(_typingDots);
            var inputArea = BuildInputArea();
            inputArea.style.height = 96;                  // compact default; resizable via the handle above it
            root.Add(BuildResizeHandle(inputArea));
            root.Add(inputArea);
            root.schedule.Execute(DrainAndRender).Every(33);
            root.schedule.Execute(TickDots).Every(400);
            root.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            root.RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        private VisualElement BuildToolbar()
        {
            var bar = new VisualElement(); bar.AddToClassList("chat-toolbar");
            var title = new Label("MCP Chat"); title.AddToClassList("chat-title");
            _modePill  = new Label("ASK"); _modePill.AddToClassList("mode-pill"); _modePill.AddToClassList("mode-pill--ask");
            _costBadge = new Label("");    _costBadge.AddToClassList("cost-badge");
            bar.Add(title); bar.Add(_modePill); bar.Add(_costBadge);
            return bar;
        }

        private VisualElement BuildInputArea()
        {
            var area = new VisualElement(); area.AddToClassList("input-area");

            // Drag-dropped object chips sit just above the editor (only visible when populated).
            _objChipStrip = new VisualElement(); _objChipStrip.AddToClassList("obj-chip-strip");
            area.Add(_objChipStrip);

            // The text editor fills all the vertical room the resizable panel gives it.
            _input = new TextField { multiline = true }; _input.AddToClassList("chat-input");
            area.Add(_input);

            // Bottom action bar (toolbar-style): mode toggle on the left, SS + Send on the right.
            var actionBar = new VisualElement(); actionBar.AddToClassList("input-actionbar");

            var modeGroup = new VisualElement(); modeGroup.AddToClassList("mode-toggle-row");
            modeGroup.Add(MakeModeBtn("Ask", false)); modeGroup.Add(MakeModeBtn("Agent", true));
            actionBar.Add(modeGroup);

            var spacer = new VisualElement(); spacer.AddToClassList("actionbar-spacer");
            actionBar.Add(spacer);

            var ssBtn = new Button(AttachScreenshot) { text = "SS", tooltip = "Attach 4-panel screenshot" };
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
            _agentMode = agentMode; _backend?.Stop(); CreateBackend();
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
            var text = _input.value?.Trim() ?? "";
            if (_pending.Count > 0) { text += "\n" + string.Join("\n", _pending); _pending.Clear(); _objChipStrip.Clear(); }
            if (string.IsNullOrEmpty(text)) return;
            _transcript.AppendUserBubble(text);
            _backend.SendTurn(UserTurnBuilder.Build(text));
            _input.value = ""; _waitingReply = true; _typingDots.style.display = DisplayStyle.Flex;
        }

        private void DrainAndRender()
        {
            _evBuf.Clear(); _backend?.DrainEvents(_evBuf);
            if (_evBuf.Count == 0) return;
            foreach (var ev in _evBuf) HandleEvent(ev);
            _scroll.scrollOffset = new Vector2(0, float.MaxValue);
        }

        private void HandleEvent(ChatEvent ev)
        {
            switch (ev.Kind)
            {
                case ChatEventKind.TextDelta:
                    _transcript.AppendOrExtendAssistant(ev.Text);
                    _waitingReply = false; _typingDots.style.display = DisplayStyle.None; break;
                case ChatEventKind.ToolStart when ev.Text != null:
                    _transcript.AppendToolChip(ev.Text, ok: true); break;
                case ChatEventKind.TurnDone:
                    if (ev.CostUsd > 0f)
                    {
                        _totalCostUsd += ev.CostUsd; _inputTokens += ev.InputTokens; _outputTokens += ev.OutputTokens;
                        _costBadge.text = $"${_totalCostUsd:F4}  {_inputTokens}↑{_outputTokens}↓";
                    }
                    _waitingReply = false; _typingDots.style.display = DisplayStyle.None; break;
                case ChatEventKind.Error:
                    _transcript.AppendToolChip(ev.Text ?? "Error", ok: false);
                    _waitingReply = false; _typingDots.style.display = DisplayStyle.None; break;
            }
        }

        private void TickDots()
        {
            if (!_waitingReply) return;
            _dotCount = (_dotCount + 1) % 4;
            _typingDots.text = new string('.', _dotCount + 1);
        }

        private static void OnDragUpdated(DragUpdatedEvent e)
        {
            if (DragAndDrop.objectReferences.Length > 0) DragAndDrop.visualMode = DragAndDropVisualMode.Link;
        }

        private void OnDragPerform(DragPerformEvent e)
        {
            DragAndDrop.AcceptDrag();
            foreach (var obj in DragAndDrop.objectReferences)
                if (obj is GameObject go) AddObjChip(go);
        }

        private void AddObjChip(GameObject go)
        {
            _pending.Add(ComponentSerializer.GetPath(go));
            var chip = new VisualElement(); chip.AddToClassList("obj-chip");
            var lbl  = new Label(go.name);  lbl.AddToClassList("obj-chip-label");
            chip.Add(lbl);
            var cap = go;
            chip.RegisterCallback<ClickEvent>(_ => { EditorGUIUtility.PingObject(cap); Selection.activeObject = cap; });
            _objChipStrip.Add(chip);
        }

        private void AttachScreenshot()
        {
            var target = Selection.activeGameObject;
            if (target == null) { Debug.LogWarning("[MCP Chat] Select a GameObject first"); return; }
            var path = MultiViewCapture.CaptureToFile(target);
            if (string.IsNullOrEmpty(path)) { Debug.LogWarning("[MCP Chat] Screenshot failed"); return; }
            var bytes = File.ReadAllBytes(path);
            var text  = _input.value?.Trim() ?? "";
            if (_pending.Count > 0) { text += "\n" + string.Join("\n", _pending); _pending.Clear(); _objChipStrip.Clear(); }
            _transcript.AppendUserBubble($"[screenshot] {text}");
            _backend.SendTurn(UserTurnBuilder.Build(text, bytes));
            _input.value = ""; _waitingReply = true; _typingDots.style.display = DisplayStyle.Flex;
        }
    }
}
