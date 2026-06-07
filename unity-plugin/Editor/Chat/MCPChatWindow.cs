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
        private BackendKind    _selectedKind = BackendKind.Claude;
        private PermissionConfig _permConfig = new PermissionConfig();
        private int            _inputTokens, _outputTokens;
        private readonly TurnUndoTracker _undoTracker = new TurnUndoTracker();
        private readonly SentTextCache _sentTextCache = new SentTextCache();
        private readonly List<ChatEvent>       _evBuf    = new List<ChatEvent>(16);
        private readonly List<ToolCallRecord>  _toolBuf  = new List<ToolCallRecord>(8);
        internal readonly CompileAutoFix       _autoFix  = new CompileAutoFix();
        internal bool _turnEditedCode;
        internal bool _turnHasToolCalls;
        internal bool _autoScrollEnabled = true;
        private TextField          _input;
        private Label              _tokenReadout;
        private Button             _askBtn, _agentBtn;
        private VisualElement      _inputArea;
        private ScrollView         _scroll;
        private InputHeightCalc    _heightCalc = new InputHeightCalc();

        [MenuItem("MCP/Chat", priority = 0)]
        public static void ShowWindow()
        {
            var w = GetWindow<MCPChatWindow>("MCP Chat");
            w.minSize = new Vector2(320, 400);
        }

        public static bool IsChatBackendRunning()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<MCPChatWindow>())
                if (w._backend?.IsRunning ?? false) return true;
            return false;
        }

        internal void ResetTokenCounters()
        {
            _inputTokens = _outputTokens = 0;
            if (_tokenReadout != null) _tokenReadout.text = "";
        }

        internal void RefreshColorResolver()
        {
            ChipPillFactory.ColorResolver = BackendConfigStore.Load().Chips.ResolveColor;
        }

        private void OnEnable()
        {
            _autoScrollEnabled = EditorPrefs.GetBool("MCPChat.AutoScroll", true);
            RefreshColorResolver();
            ChipPillFactory.AddToContextAction = chip => _chipField?.AddChip(chip);
            CreateBackend();
            ResetTokenCounters();
            ChatProcess.OnAfterReloadResume += TryResumePendingTurn;
            AssemblyReloadEvents.beforeAssemblyReload += SaveStateBeforeReload;
            EditorApplication.hierarchyChanged += RefreshLinker;
            _autoFix.Subscribe();
            _autoFix.OnErrorsDetected += InjectCompileErrors;
            _undoTracker.Invalidate();
        }

        internal void RefreshChipDisplay()
        {
            _chipField?.RebuildFromModel();
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= RefreshLinker;
            AssemblyReloadEvents.beforeAssemblyReload -= SaveStateBeforeReload;
            ChatProcess.OnAfterReloadResume -= TryResumePendingTurn;
            _autoFix.OnErrorsDetected -= InjectCompileErrors;
            _autoFix.Unsubscribe();
            ChipPillFactory.AddToContextAction = null;
            ReloadGuard.OnTurnFinished();
            if (_activity.Phase != ActivityPhase.Idle)
                _undoTracker.OnTurnFailed();
            _backend?.Stop();
            _backend = null;
        }

        private ChatRefResolver _resolver;
        private SceneNameLinker  _linker;

        private void RefreshLinker()
        {
            if (_resolver == null) return;
            _resolver.Refresh();
            _linker?.Refresh(_resolver.Objects);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.unity-mcp.editor/Editor/Chat/MCPChatWindow.uss");
            if (ss != null) root.styleSheets.Add(ss);
            root.AddToClassList("chat-root");
            if (!EditorGUIUtility.isProSkin) root.AddToClassList("chat-root--light");
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.AddToClassList("chat-scroll");
            var inner = new VisualElement();
            _scroll.Add(inner);
            _resolver = new ChatRefResolver();
            _resolver.Refresh();
            _linker = new SceneNameLinker();
            _linker.Refresh(_resolver.Objects);
            // Static seam: all windows share one scene, so last-write-wins is correct.
            MarkdownInline.Linker = _linker;
            var registry = ChatBlockRendererFactory.CreateDefault(_resolver, AddRefToContext);
            _transcript = new ChatTranscript(inner, registry);
            _transcript.SceneObjects = () => _resolver.Objects;
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
            TryResumePendingTurn();
        }

        private VisualElement BuildInputArea()
        {
            var area = new VisualElement(); area.AddToClassList("input-area");
            area.Add(BuildFlowBar());

            _chipField = new InlineChipField();
            _chipField.AddToClassList("chat-input");
            _input = _chipField.TextField;
            area.Add(_chipField);
            WireChipInput();

            EnterKeySend.Attach(_input, OnSend);
            area.Add(BuildFooterBar());
            return area;
        }

        private void SetMode(bool agentMode)
        {
            if (_agentMode == agentMode) return;
            _agentMode = agentMode;
            _backend?.Stop();
            ResetTokenCounters();
            CreateBackend();
            _askBtn?.EnableInClassList("mode-toggle-btn--active",   !agentMode);
            _agentBtn?.EnableInClassList("mode-toggle-btn--active", agentMode);
        }

        private void CreateBackend() => CreateBackendWithSession(null);

        private void CreateBackendWithSession(string resumeSessionId, BackendConfigStore store = null)
        {
            store ??= BackendConfigStore.Load();
            switch (_selectedKind)
            {
                case BackendKind.Codex:
                    _backend = new CodexBackend(resumeSessionId,
                        store.Codex.StartupTimeoutSec,
                        store.Codex.ExtraArgs);
                    break;

                case BackendKind.CodexAppServer:
                    _backend = new CodexAppServerBackend(resumeSessionId,
                        store.Codex.StartupTimeoutSec,
                        store.Codex.ExtraArgs);
                    break;

                default: // BackendKind.Claude
                    var cfg = ChatMcpConfigWriter.GetOrCreateConfigPath()
                        ?? Path.Combine(
                            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                            ".claude", "mcp.json");
                    _backend = new ClaudeBackend(cfg, _agentMode ? "acceptEdits" : "plan",
                        _selectedAgent, _permConfig, resumeSessionId,
                        store.Claude.Model, store.Claude.ExtraArgs);
                    break;
            }
        }

        private void ResetInputAreaHeight()
        {
            _inputArea.style.height    = InputHeightCalc.CompactH;
            _inputArea.style.minHeight = StyleKeyword.Null;
            _inputArea.style.maxHeight = _heightCalc.ComputeMax(position.height);
        }

    }
}
