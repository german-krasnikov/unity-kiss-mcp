using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow : EditorWindow
    {
        private IChatBackend   _backend;
        internal ChatTranscript _transcript;
        private bool           _agentMode;
        private BackendKind    _selectedKind = BackendKind.Claude;
        private PermissionConfig _permConfig = new PermissionConfig();
        private int            _inputTokens, _outputTokens;
        private float          _costUsd;
        private readonly TurnUndoTracker  _undoTracker       = new TurnUndoTracker();
        private readonly SessionAllowlist  _sessionAllowlist  = new SessionAllowlist();
        private readonly SentTextCache _sentTextCache = new SentTextCache();
        // task#10: caches the full-path LLM payload (paths + [kind:path] block) sent this turn,
        // so an in-flight domain reload re-sends the SAME payload, not the short-name display text.
        private readonly SentTextCache _sentLlmCache = new SentTextCache();
        private readonly List<ChatEvent>       _evBuf    = new List<ChatEvent>(16);
        private readonly List<ToolCallRecord>  _toolBuf  = new List<ToolCallRecord>(8);
        internal readonly CompileAutoFix       _autoFix  = new CompileAutoFix();
        internal bool _turnEditedCode;
        internal bool _turnHasToolCalls;
        internal bool _needsRefresh;
        private bool  _transcriptRestored;
        // D6: bounded retry counter for TryResumePendingTurn compile-clean gate.
        // Resets to 0 on success; gives up after MaxResumeRetries+1 calls with !IsCompileClean.
        internal int  _resumeRetryCount;
        internal const int MaxResumeRetries = 30;
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

        // P0-2: DRY helper — reset all per-turn flags (3 sites in Drain + CancelTurn + NewSession).
        private void ResetTurnFlags()
        {
            _turnEditedCode = _turnHasToolCalls = _needsRefresh = false;
        }

        internal void ResetTokenCounters()
        {
            _inputTokens = _outputTokens = 0;
            _costUsd = 0f;
            if (_tokenReadout != null) _tokenReadout.text = "";
        }

        internal void RefreshColorResolver()
        {
            ChipPillFactory.ColorResolver = BackendConfigStore.Load().Chips.ResolveColor;
        }

        private void OnEnable()
        {
            RefreshColorResolver();
            ChipPillFactory.AddToContextAction = chip => _chipField?.AddChip(chip);
            CreateBackend();
            ResetTokenCounters();
            ChatProcess.OnAfterReloadResume += TryResumePendingTurn;
            AssemblyReloadEvents.beforeAssemblyReload += SaveStateBeforeReload;
            EditorApplication.hierarchyChanged += RefreshResolver;
            _autoFix.Subscribe();
            _autoFix.OnErrorsDetected += InjectCompileErrors;
            _undoTracker.Invalidate();
            CommandRouter.OnAskUser += OnMcpAskUser;
        }

        internal void RefreshChipDisplay()
        {
            _chipField?.RebuildFromModel();
        }

        private void OnDisable()
        {
            CommandRouter.OnAskUser -= OnMcpAskUser;
            EditorApplication.hierarchyChanged -= RefreshResolver;
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

        private void RefreshResolver()
        {
            _resolver?.Refresh();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.unity-mcp.editor/Editor/Chat/View/MCPChatWindow.uss");
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
            _transcript.SceneObjects = () => _resolver.Objects;
            // F21: restore transcript that was saved before domain reload
            var savedTranscript = SessionState.GetString("MCPChat_Transcript", "");
            if (!string.IsNullOrEmpty(savedTranscript))
            {
                _transcript.RestoreFromReload(savedTranscript);
                SessionState.EraseString("MCPChat_Transcript");
            }
            _transcriptRestored = !string.IsNullOrEmpty(savedTranscript); // P0-1: guard duplicate bubble
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
            // F20: Esc cancels a running turn. Guard: Idle → no-op (slash popup handles its own Esc).
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape && _activity.Phase != ActivityPhase.Idle)
                {
                    CancelTurn();
                    evt.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
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
            var resumeId = _backend?.SessionId;   // capture before Stop() clears the process
            _agentMode = agentMode;
            _backend?.Stop();
            ResetTokenCounters();
            CreateBackendWithSession(resumeId);
            _askBtn?.EnableInClassList("mode-toggle-btn--active",   !agentMode);
            _agentBtn?.EnableInClassList("mode-toggle-btn--active", agentMode);
        }

        private void CreateBackend() => CreateBackendWithSession(null);

        private void CreateBackendWithSession(string resumeSessionId, BackendConfigStore store = null)
        {
            store ??= BackendConfigStore.Load();
            store = ApplySelectedModel(store, _selectedKind, _selectedModel);

            var providerId = BackendProviderRegistry.KindToId(_selectedKind);
            var provider   = BackendProviderRegistry.Get(providerId);
            if (provider != null)
            {
                var mcpCfg = ChatMcpConfigWriter.GetOrCreateConfigPath();
                _backend = provider.Create(new BackendCreateArgs(
                    mcpCfg, _agentMode, _selectedAgent, _permConfig, resumeSessionId, store));
                return;
            }
            // Fallback: provider not found (binary not installed) — default to Claude
            var cfg = ChatMcpConfigWriter.GetOrCreateConfigPath()
                ?? Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                    ".claude", "mcp.json");
            _backend = new ClaudeBackend(cfg, _agentMode ? "acceptEdits" : "plan",
                _selectedAgent, _permConfig, resumeSessionId,
                store.Claude.Model, store.Claude.ExtraArgs);
        }

        // Pure helper — no allocation if model unchanged.
        internal static BackendConfigStore CloneWithModel(BackendConfigStore src, string model)
        {
            if (src.Claude.Model == model) return src;
            return new BackendConfigStore
            {
                Claude = new ClaudeBackendConfig
                {
                    PermissionMode = src.Claude.PermissionMode,
                    Model          = model,
                    ExtraArgs      = src.Claude.ExtraArgs,
                },
                Codex  = src.Codex,
                Gemini = src.Gemini,
                Chips  = src.Chips,
            };
        }

        internal static BackendConfigStore ApplySelectedModel(
            BackendConfigStore src, BackendKind kind, string selectedModel)
        {
            if (string.IsNullOrEmpty(selectedModel) || selectedModel == "__custom__") return src;
            switch (kind)
            {
                case BackendKind.Claude:
                    if (src.Claude.Model == selectedModel) return src;
                    return new BackendConfigStore
                    {
                        Claude = new ClaudeBackendConfig
                        {
                            PermissionMode = src.Claude.PermissionMode,
                            Model          = selectedModel,
                            ExtraArgs      = src.Claude.ExtraArgs,
                        },
                        Codex  = src.Codex,
                        Gemini = src.Gemini,
                        Chips  = src.Chips,
                    };
                case BackendKind.Codex:
                    if (src.Codex.Model == selectedModel) return src;
                    return new BackendConfigStore
                    {
                        Claude = src.Claude,
                        Codex  = new CodexBackendConfig
                        {
                            Model             = selectedModel,
                            PermissionMode    = src.Codex.PermissionMode,
                            StartupTimeoutSec = src.Codex.StartupTimeoutSec,
                            ExtraArgs         = src.Codex.ExtraArgs,
                        },
                        Gemini = src.Gemini,
                        Chips  = src.Chips,
                    };
                case BackendKind.Gemini:
                    if (src.Gemini.Model == selectedModel) return src;
                    return new BackendConfigStore
                    {
                        Claude = src.Claude,
                        Codex  = src.Codex,
                        Gemini = new GeminiBackendConfig
                        {
                            Model        = selectedModel,
                            ApprovalMode = src.Gemini.ApprovalMode,
                            Sandbox      = src.Gemini.Sandbox,
                            ExtraArgs    = src.Gemini.ExtraArgs,
                        },
                        Chips  = src.Chips,
                    };
                default: return src;
            }
        }

        internal void CancelTurn()
        {
            if (_activity.Phase == ActivityPhase.Idle) return;
            _transcript?.FinalizeAssistant();
            ReloadGuard.OnTurnFinished();
            ResetTurnFlags(); // P0-2: clear stale per-turn flags on cancel
            _undoTracker.OnTurnFailed();
            _activity.Fail();
            OnActivityChanged();
            _backend?.Stop();
            _backend = null;
            CreateBackend();
        }

        private void ResetInputAreaHeight()
        {
            _inputArea.style.height    = InputHeightCalc.CompactH;
            _inputArea.style.minHeight = StyleKeyword.Null;
            _inputArea.style.maxHeight = _heightCalc.ComputeMax(position.height);
        }

    }
}
