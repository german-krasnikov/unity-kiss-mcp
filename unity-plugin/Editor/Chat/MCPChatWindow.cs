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
        // FIX A: cache the sent text so SaveStateBeforeReload reads it, not the cleared input.
        private readonly SentTextCache _sentTextCache = new SentTextCache();
        private readonly List<ChatEvent>       _evBuf    = new List<ChatEvent>(16);
        private readonly List<ToolCallRecord>  _toolBuf  = new List<ToolCallRecord>(8);
        internal readonly CompileAutoFix       _autoFix  = new CompileAutoFix();
        // Set when Claude's turn contained a code-editing tool call; cleared at TurnDone.
        internal bool _turnEditedCode;
        internal bool _turnHasToolCalls;
        internal bool _autoScrollEnabled = true;
        private TextField          _input;
        private Label              _tokenReadout;
        private Button             _askBtn, _agentBtn;
        private VisualElement      _objChipStrip;
        private VisualElement      _inputArea;
        private ScrollView         _scroll;
        private InputHeightCalc    _heightCalc = new InputHeightCalc();

        [MenuItem("MCP/Chat", priority = 0)]
        public static void ShowWindow()
        {
            var w = GetWindow<MCPChatWindow>("MCP Chat");
            w.minSize = new Vector2(320, 400);
        }

        /// <summary>Called by ChatBackendProbe via reflection. True when any open chat window has a live backend.</summary>
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

        /// <summary>Load config once and bind its ResolveColor — avoids per-pill file read.</summary>
        internal void RefreshColorResolver()
        {
            ChipPillFactory.ColorResolver = BackendConfigStore.Load().Chips.ResolveColor;
        }

        private void OnEnable()
        {
            _autoScrollEnabled = EditorPrefs.GetBool("MCPChat.AutoScroll", true);
            // P4: wire color resolver so pills use persisted overrides (re-set after domain reload).
            RefreshColorResolver();
            CreateBackend();
            ResetTokenCounters();
            ChatProcess.OnAfterReloadResume += TryResumePendingTurn;
            AssemblyReloadEvents.beforeAssemblyReload += SaveStateBeforeReload;
            _autoFix.Subscribe();
            _autoFix.OnErrorsDetected += InjectCompileErrors;
            // F6: group indices are stale after domain reload.
            _undoTracker.Invalidate();
        }

        /// <summary>Live-refresh input chip colors after settings change.</summary>
        internal void RefreshChipDisplay()
        {
            _chipField?.RebuildFromModel();
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= SaveStateBeforeReload;
            ChatProcess.OnAfterReloadResume -= TryResumePendingTurn;
            _autoFix.OnErrorsDetected -= InjectCompileErrors;
            _autoFix.Unsubscribe();
            // #3 fix: window closed mid-turn → release the reload lock so the next compile isn't blocked.
            ReloadGuard.OnTurnFinished();
            // F6: mark in-flight turn as failed so the group is still restorable.
            if (_activity.Phase != ActivityPhase.Idle)
                _undoTracker.OnTurnFailed();
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
            if (!EditorGUIUtility.isProSkin) root.AddToClassList("chat-root--light");
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
            // CreateGUI runs after OnEnable, so _flowBar is ready here (OnEnable would NRE).
            TryResumePendingTurn();
        }

        private VisualElement BuildInputArea()
        {
            var area = new VisualElement(); area.AddToClassList("input-area");
            area.Add(BuildFlowBar());
            _objChipStrip = new VisualElement(); _objChipStrip.AddToClassList("obj-chip-strip");
            area.Add(_objChipStrip);

            // Wave 0: replace raw TextField + overlay with composed InlineChipField.
            _chipField = new InlineChipField();
            _chipField.AddToClassList("chat-input");
            _input = _chipField.TextField; // keep _input for back-compat (EnterKeySend, height calc)
            area.Add(_chipField);

            // Wire context menu on the chip field.
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
            // Definite height (not minHeight) is required so flex-grow on .chat-input
            // has a parent size to grow into — minHeight is a floor and leaves a dead gap.
            _inputArea.style.height    = InputHeightCalc.CompactH;
            _inputArea.style.minHeight = StyleKeyword.Null;
            _inputArea.style.maxHeight = _heightCalc.ComputeMax(position.height);
        }

    }
}
