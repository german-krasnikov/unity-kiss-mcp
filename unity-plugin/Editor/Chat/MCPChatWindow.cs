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
        private InlineChipTracker  _chipTracker;
        private InlineChipOverlay  _chipOverlay;

        [MenuItem("MCP/Chat", priority = 0)]
        public static void ShowWindow()
        {
            var w = GetWindow<MCPChatWindow>("MCP Chat");
            w.minSize = new Vector2(320, 400);
        }

        /// <summary>
        /// Called by ChatBackendProbe via reflection. Returns true when any open chat window
        /// has a live backend process.
        /// </summary>
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

        private void OnEnable()
        {
            _autoScrollEnabled = EditorPrefs.GetBool("MCPChat.AutoScroll", true);
            CreateBackend();
            ResetTokenCounters();
            ChatProcess.OnAfterReloadResume += TryResumePendingTurn;
            AssemblyReloadEvents.beforeAssemblyReload += SaveStateBeforeReload;
            _autoFix.Subscribe();
            _autoFix.OnErrorsDetected += InjectCompileErrors;
            // F6: group indices are stale after domain reload.
            _undoTracker.Invalidate();
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
            // Resume any pending turn here — CreateGUI runs after OnEnable, so _flowBar is ready.
            // Calling this from OnEnable would NRE because _flowBar hasn't been built yet.
            // MANUAL TEST: open chat → send a turn → edit any .cs to trigger domain reload mid-turn
            //              → assert no NRE in console, flow bar shows Sending, turn resumes.
            TryResumePendingTurn();
        }

        private VisualElement BuildInputArea()
        {
            var area = new VisualElement(); area.AddToClassList("input-area");
            area.Add(BuildFlowBar());
            _objChipStrip = new VisualElement(); _objChipStrip.AddToClassList("obj-chip-strip");
            area.Add(_objChipStrip);
            _input = new TextField { multiline = true }; _input.AddToClassList("chat-input");
            area.Add(_input);

            // F5: inline chip overlay (absolute-positioned row of pills over the TextField)
            _chipTracker = new InlineChipTracker();
            _chipOverlay = new InlineChipOverlay(_input, _chipTracker);
            _chipOverlay.SetRemoveCallback(RemoveInlineChipAt);
            _chipOverlay.AttachTo(area);
            InlineChipKeyHandler.Attach(_input, _chipTracker, _chipOverlay);

            // Context-menu: right-click the input → "Add Selection to Context"
            _input.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Add Selection to Context",
                    _ => InsertInlineChip(UnityEditor.Selection.activeGameObject),
                    _ => UnityEditor.Selection.activeGameObject != null
                        ? DropdownMenuAction.Status.Normal
                        : DropdownMenuAction.Status.Disabled);
            }));

            EnterKeySend.Attach(_input, OnSend);
            area.Add(BuildFooterBar());
            return area;
        }

        /// <summary>Insert a chip marker at the cursor in _input and record ChipData.</summary>
        internal void InsertInlineChip(UnityEngine.GameObject go)
        {
            if (go == null) return;
            var path = ComponentSerializer.GetPath(go);
            InsertInlineChip(go, path, go.name);
        }

        internal void InsertInlineChip(UnityEngine.Object cap, string path, string displayName)
        {
            if (string.IsNullOrEmpty(path)) return;
            var cur   = _input.value ?? "";
            var caret = Mathf.Clamp(_input.cursorIndex, 0, cur.Length);
            // Insert marker char at caret position
            _input.value = cur.Substring(0, caret)
                         + InlineChipTracker.Marker
                         + cur.Substring(caret);
            // Move caret past the inserted marker
            _input.SelectRange(caret + 1, caret + 1);

            var instanceID = cap != null ? cap.GetInstanceID() : 0;
            _chipTracker.Add(new ChipData(path, displayName, instanceID));
            _chipOverlay.Refresh();
            _input.Focus();
            UpdateAutoHeight();
        }

        private void RemoveInlineChipAt(int chipIndex)
        {
            if (chipIndex < 0 || chipIndex >= _chipTracker.Count) return;

            // Find and remove the chipIndex-th U+FFFC marker from the text.
            // Setting _input.value triggers ValueChangedCallback which calls SyncToText,
            // so tracker + overlay are updated there — do NOT touch them here.
            var text = _input.value ?? "";
            int nth  = -1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == InlineChipTracker.Marker)
                {
                    nth++;
                    if (nth == chipIndex)
                    {
                        _input.value = text.Remove(i, 1);
                        break;
                    }
                }
            }

            UpdateAutoHeight();
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

        private void CreateBackendWithSession(string resumeSessionId)
        {
            var store = BackendConfigStore.Load();
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

        private void OnSend()
        {
            if (!_activity.CanSend) return; // #6: no re-entrant send during active turn
            _autoFix.Disarm(); // user manually sending — cancel any pending auto-fix

            // F5: strip U+FFFC markers from displayed text before trimming
            var rawText = _input.value ?? "";
            var text    = rawText.Replace(InlineChipTracker.Marker.ToString(), "").Trim();
            var chips   = CollectChipPaths();

            // F5: merge inline chip paths (deduplicated, strip-strip chips already in chips)
            foreach (var p in _chipTracker.Paths)
                if (!chips.Contains(p)) chips.Add(p);

            // #3 Auto-include selection: prepend summary line if not already a chip.
            var selGo   = Selection.activeGameObject;
            var chipSet = new HashSet<string>(chips);
            if (SelectionSummary.ShouldPrepend(selGo, chipSet))
                text = SelectionSummary.Summarize(selGo) + "\n" + text;

            if (chips.Count > 0) text += "\n" + ChipContextResolver.ResolveAll(chips);
            if (string.IsNullOrEmpty(text)) return;

            DispatchTurn(UserTurnBuilder.Build(text), text);
        }

        private void AttachScreenshot()
        {
            if (!_activity.CanSend) return; // #6: guard second vector — SS button also dispatches a turn
            var target = Selection.activeGameObject;
            if (target == null) { Debug.LogWarning("[MCP Chat] Select a GameObject first"); return; }
            var capturePath = MultiViewCapture.CaptureToFile(target);
            if (string.IsNullOrEmpty(capturePath)) { Debug.LogWarning("[MCP Chat] Screenshot failed"); return; }
            var bytes = File.ReadAllBytes(capturePath);
            // F5: strip markers from displayed text
            var text  = (_input.value ?? "").Replace(InlineChipTracker.Marker.ToString(), "").Trim();
            var chips = CollectChipPaths();
            foreach (var p in _chipTracker.Paths)
                if (!chips.Contains(p)) chips.Add(p);
            if (chips.Count > 0) text += "\n" + ChipContextResolver.ResolveAll(chips);
            DispatchTurn(UserTurnBuilder.Build(text, bytes), text, screenshotPath: capturePath);
        }

        // Shared send sequence — OnSend and AttachScreenshot must not drift from each other.
        private void DispatchTurn(string turnJson, string displayText, string screenshotPath = null)
        {
            // #1/#4 Lock reloads for the duration of this turn (symmetric for both send paths).
            ReloadGuard.OnTurnStarted();
            // F6: open a named undo group for the duration of this turn.
            _undoTracker.OnTurnStart(displayText?.Length > 40
                ? displayText.Substring(0, 40)
                : displayText ?? "");
            // FIX A: cache before clearing input so SaveStateBeforeReload can read the sent text.
            _sentTextCache.Set(displayText);
            _transcript.AppendUserBubble(displayText, screenshotPath);
            _backend.SendTurn(turnJson);
            _input.value = ""; _input.cursorIndex = _input.selectIndex = 0;
            _objChipStrip.Clear();
            // F5: clear inline chips
            _chipTracker?.Clear();
            _chipOverlay?.Refresh();
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
            // F5: use inline chip so ref is visually represented as a pill
            // DisplayName = last segment of path (after last '/')
            var display = refPath.Contains("/")
                ? refPath.Substring(refPath.LastIndexOf('/') + 1)
                : refPath;
            InsertInlineChip(null, refPath, display);
        }
    }
}
