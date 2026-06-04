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
        // FIX A: cache the sent text so SaveStateBeforeReload reads it, not the cleared input.
        private readonly SentTextCache _sentTextCache = new SentTextCache();
        private readonly List<ChatEvent>       _evBuf    = new List<ChatEvent>(16);
        private readonly List<ToolCallRecord>  _toolBuf  = new List<ToolCallRecord>(8);
        internal readonly CompileAutoFix       _autoFix  = new CompileAutoFix();
        // Set when Claude's turn contained a code-editing tool call; cleared at TurnDone.
        internal bool _turnEditedCode;
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

        private void OnEnable()
        {
            CreateBackend();
            ChatProcess.OnAfterReloadResume += TryResumePendingTurn;
            AssemblyReloadEvents.beforeAssemblyReload += SaveStateBeforeReload;
            _autoFix.Subscribe();
            _autoFix.OnErrorsDetected += InjectCompileErrors;
            // #1 fix: OnEnable fires AFTER afterAssemblyReload, so the event already fired.
            // Call directly here — it's a no-op when no pending file exists.
            TryResumePendingTurn();
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= SaveStateBeforeReload;
            ChatProcess.OnAfterReloadResume -= TryResumePendingTurn;
            _autoFix.OnErrorsDetected -= InjectCompileErrors;
            _autoFix.Unsubscribe();
            // #3 fix: window closed mid-turn → release the reload lock so the next compile isn't blocked.
            ReloadGuard.OnTurnFinished();
            _backend?.Stop();
            _backend = null;
        }

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
            SetupSlash();
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

        private void SetMode(bool agentMode)
        {
            if (_agentMode == agentMode) return;
            _agentMode = agentMode;
            _backend?.Stop(); CreateBackend();
            _askBtn?.EnableInClassList("mode-toggle-btn--active",   !agentMode);
            _agentBtn?.EnableInClassList("mode-toggle-btn--active", agentMode);
        }

        private void CreateBackend() => CreateBackendWithSession(null);

        private void CreateBackendWithSession(string resumeSessionId)
        {
            var cfg = ChatMcpConfigWriter.GetOrCreateConfigPath()
                ?? Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                    ".claude", "mcp.json");
            _backend = new ClaudeBackend(cfg, _agentMode ? "acceptEdits" : "plan",
                _selectedAgent, _permConfig, resumeSessionId);
        }

        private void OnSend()
        {
            _autoFix.Disarm(); // user manually sending — cancel any pending auto-fix
            var text  = _input.value?.Trim() ?? "";
            var chips = CollectChipPaths();

            // #3 Auto-include selection: prepend summary line if not already a chip.
            var selGo  = Selection.activeGameObject;
            var chipSet = new HashSet<string>(chips);
            if (SelectionSummary.ShouldPrepend(selGo, chipSet))
                text = SelectionSummary.Summarize(selGo) + "\n" + text;

            if (chips.Count > 0) text += "\n" + ChipContextResolver.ResolveAll(chips);
            if (string.IsNullOrEmpty(text)) return;

            DispatchTurn(UserTurnBuilder.Build(text), text);
        }

        private void AttachScreenshot()
        {
            var target = Selection.activeGameObject;
            if (target == null) { Debug.LogWarning("[MCP Chat] Select a GameObject first"); return; }
            var path = MultiViewCapture.CaptureToFile(target);
            if (string.IsNullOrEmpty(path)) { Debug.LogWarning("[MCP Chat] Screenshot failed"); return; }
            var bytes  = File.ReadAllBytes(path);
            var text   = _input.value?.Trim() ?? "";
            var chips  = CollectChipPaths();
            if (chips.Count > 0) text += "\n" + ChipContextResolver.ResolveAll(chips);
            DispatchTurn(UserTurnBuilder.Build(text, bytes), text, screenshotPath: path);
        }

        // Shared send sequence — OnSend and AttachScreenshot must not drift from each other.
        private void DispatchTurn(string turnJson, string displayText, string screenshotPath = null)
        {
            // #1/#4 Lock reloads for the duration of this turn (symmetric for both send paths).
            ReloadGuard.OnTurnStarted();
            // FIX A: cache before clearing input so SaveStateBeforeReload can read the sent text.
            _sentTextCache.Set(displayText);
            _transcript.AppendUserBubble(displayText, screenshotPath);
            _backend.SendTurn(turnJson);
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
