# Feature: Optional In-Unity Agent Chat

## Overview

An optional Editor window that brings agentic chat directly into Unity, spawning the user's local `claude` CLI as a child process. Zero new MCP tools — reuses all ~90 existing tools via the spawn-the-CLI architecture.

**Isolation:** Behind the `UNITY_MCP_CHAT` scripting define in `UnityMCP.Editor.Chat.asmdef`. OFF by default; deleting the `Chat/` folder leaves core untouched.

## Architecture

```
Unity Editor Window (MCPChatWindow)
    │
    └─ System.Diagnostics.Process
        │
        └─ claude CLI (headless, stream-json mode)
            │
            └─ python -m unity_mcp.server
                │
                └─ TCP:9500 → Unity Editor Plugin
                    └─ ~90 MCP tools (create, set_property, screenshot, etc.)
```

### Spawn Invocation

```bash
claude -p \
  --output-format stream-json \
  --verbose \
  --include-partial-messages \
  --input-format stream-json \
  --mcp-config <config.json> \
  --permission-mode <plan|acceptEdits>
```

Key details:
- **`-p`** — headless streaming mode (no interactive terminal)
- **`--output-format stream-json`** — stream JSON events (partial message chunks)
- **`--include-partial-messages`** — emit tool cards + results as they arrive
- **`--input-format stream-json`** — accept JSON-encoded user turns on stdin
- **`--mcp-config`** — path to the MCP config file (defines `unity_mcp` server)
- **`--permission-mode plan|acceptEdits`** — user-selected mode (tool calls require acknowledgment or auto-accept)
- **Auth:** Uses user's locally-installed `claude` CLI with cached subscription login. `ANTHROPIC_API_KEY` is explicitly stripped from child env to prevent API key leakage or double-billing.

### Module Isolation

**C# asmdef + Scripting Define:**
- `UnityMCP.Editor.Chat.asmdef` (references ONLY `UnityMCP.Editor`, autoReferenced=false, defineConstraints `["UNITY_MCP_CHAT"]`)
- One-way dependency: Chat → Core (via assembly reference), not Core → Chat
- Scripting define `UNITY_MCP_CHAT` must be manually enabled in Player Settings > Other Settings > Scripting Define Symbols (or toggled via MCPChatWindow settings)

**InternalsVisibleTo:**
- Core exposes internals: `[assembly: InternalsVisibleTo("UnityMCP.Editor.Chat")]` in `AssemblyInfo.cs`
- Enables Chat to access internal core APIs (CommandRouter, RefManager, CommandRegistry, etc.)

**Settings Hook (Event-Driven):**
- Core fires `ChatSettingsHook.OnBuildToolsCatalog` event on MCPSettings build
- Chat subscribes: `ChatSettingsHook.OnBuildToolsCatalog += RefreshSettings`
- Preserves one-way dependency: core does not know Chat exists
- Removed the GUI code for Chat settings completely in core for clarity

## Multi-Backend Architecture (v0.14.0+)

Each CLI-based backend is a strategy over **4 variation axes:**

1. **BuildArgs** — spawn/resume argv construction (e.g., Claude uses `--resume <sessionId>`, Codex uses `exec resume <id>`)
2. **ParseLine** — NDJSON line → ChatEvent[] conversion (stream format differs per CLI)
3. **BinaryName** — CLI executable name for ChatBinaryResolver (e.g., `"claude"`, `"codex"`)
4. **IsPersistentProcess** — true = stdin loop (Claude), false = spawn-per-turn (Codex)

**CliBackendBase** (127-line abstract host): Owns shared lifecycle (spawn, drain, accumulate, SessionId, Stop, Dispose). Subclasses override only the 4 axes; all other logic (turn dispatch, tool accumulation, session management) is inherited.

**ClaudeBackend** (ported): Zero behavior change (−65 lines net). Now a thin wrapper over the base. Regression anchor proving the abstraction doesn't alter existing behavior.

**CodexBackend** (new, v0.14.0): Implements the 4 axes for OpenAI Codex. Spawn-per-turn model: each turn disposes the old process and spawns a fresh one with the prompt baked into argv (via `-c mcp_servers.*` flags). Stdin closed immediately (spike fact #4: Codex hangs without this).

**CodexArgBuilder** (new): Constructs `codex exec --json` argv. Three `-c mcp_servers.unity*` flags re-passed every turn, including on resume. Format: `-c mcp_servers.{unity,unity_auth,unity_plugins}=<value>`.

**CodexStreamParser** (new): Codex NDJSON → ChatEvent. Emits agent_message, mcp_tool_call, command_execution (aggregated_output or declined), file_change (changes array), and turn.completed (usage stats; CostUsd=0). 26 NUnit test cases cover all paths.

**BackendRegistry** & **BackendKind**: Central enum + factory. User selects Claude or Codex from dropdown; MCPChatWindow.CreateBackend dispatches to the right subclass.

**PendingTurnState v3** (upgraded): Now persists `BackendKind` to survive domain reload. Back-compatible with v1/v2 state; header includes version marker.

**Result:** Adding a new backend = 1 new CliBackendBase subclass + parser file. No changes to window, dispatcher, or lifecycle code.

## IChatBackend Abstraction

Single interface for pluggable chat backends:

```csharp
public interface IChatBackend
{
    event EventHandler<ChatEvent>? OnChatEvent;
    Task<bool> StartAsync(string modePermission, string userPrompt);
    Task StopAsync();
    Task SendUserTurnAsync(JsonObject turn);
    bool IsConnected { get; }
    string Status { get; }
}
```

**Implementations:** `ClaudeBackend` (Claude, persistent stdin), `CodexBackend` (Codex, spawn-per-turn). Future: add more via `CliBackendBase` subclasses.

**ChatEvent struct:**
- Normalized event type (ToolCard, ToolResult, UserMessage, Error, Status, Done)
- Humanized text output (e.g., "Editing /Enemies/Boss" not raw JSON)
- Raw event data preserved for debugging

## Features

### Compile Auto-Fix Loop (F5, plugin 0.8.0)

`CompileAutoFix.cs` automatically retries after edits fail to compile. Lifecycle:

1. **On turn start:** Arm the retry loop (MAX_RETRIES = 3)
2. **On each compile finish:** Check if retries remain
3. **If compile succeeds:** Disarm immediately
4. **If retries exhausted:** Show a cap chip; final compile absorbed silently (no error spam)

**Provenance gating:** Only arms when the turn actually edited a `.cs` file (tracked by `_turnEditedCode` flag in MCPChatWindow.Drain.cs). Manual IDE edits never trigger auto-retries, preventing false positives.

### Editor State Snapshot Injection (F7, plugin 0.8.0)

`EditorStateSnapshot.cs` builds a lightweight context block and injects it early:

**Content:**
- Active scene name
- Compile status (OK, Compiling, Error)
- Console error count
- First 500 chars of scene hierarchy (with "…(truncated)" if longer)

**Injection:** Via `--append-system-prompt` on fresh chat sessions (ClaudeArgBuilder.cs sets the flag; ClaudeBackend.cs appends the block). On domain-reload resume, the snapshot is prepended to sent text via SentTextCache.

**Result:** Claude starts with full context, eliminating the 2–3 cold-start probe calls it used to make ("What scene are we in?", "Are there compile errors?", "Show me the hierarchy"). Immediate productivity boost; no extra token cost on subsequent turns.

### Tool Ping on Call Complete (F29, plugin 0.8.0)

`ToolPing.cs` flashes any GameObject a tool call touches. Behavior:

1. Tool call completes with args (e.g., `set_property path=/Enemies/Boss`)
2. `ToolPing` extracts the object path from the args
3. Resolves via `ComponentSerializer.FindObject(path)`
4. Calls `EditorGUIUtility.PingObject(instance)` (main thread, inside MCPChatWindow.Drain)
5. Object flashes briefly in the Hierarchy window

**Graceful:** If path missing or unresolvable, no-op (no error shown). Fires exactly once per tool call. Immediate visual feedback for the user on which object was just mutated.

### Plan/Act "Approve & Execute" Bridge (F11, plugin 0.10.0)

After a Plan-mode (Ask) turn finishes, `MCPChatWindow.Drain.cs` injects a one-shot "Approve & Execute" button into the transcript via `ApproveButtonFactory`. Clicking it:
1. Captures the current backend `SessionId`
2. Flips the window to Agent mode
3. Recreates the backend with `--resume <sessionId>` (preserves the just-produced plan)
4. Auto-dispatches the prompt "Execute the plan above."

Files: `MCPChatWindow.Approve.cs` (event handler), `ApproveHelper.cs` (session management), `ApproveButtonFactory.cs` (button builder), `ChatTranscript.Append(VisualElement)` made internal.

**Result:** Seamless bridge from planning to execution in a single workflow, plan never lost. 10 NUnit EditMode tests green.

### Slash-Command Templates (F12, plugin 0.10.0)

Typing `/` in the composer opens a UIToolkit popup of 5 builtin templates: `/fix-compile`, `/add-component`, `/playtest`, `/inspect`, `/screenshot`. Selecting one resolves to plain composer text BEFORE send — a pure input transform with NO MCP coupling.

Files: `SlashTemplate.cs` (`[Flags] ContextGather` enum + readonly struct), `SlashRegistry.cs` (Builtins/Match/Resolve), `SlashPopup.cs` (UIToolkit popup, MaxVisible=5), `MCPChatWindow.Slash.cs` (SetupSlash wires ChangeEvent + KeyDownEvent on parent `_inputArea` at TrickleDown).

**Optional context-gather** (compile errors / selection / scene state / console) with graceful "(context unavailable)" fallback on throw. KeyDown handler on parent at TrickleDown ensures deterministic trickle-down order: Enter resolves template BEFORE `EnterKeySend` fires.

**Result:** Speed up common workflows with one keystroke; templates provide context automatically. 16 NUnit EditMode tests green. +44 lines MCPChatWindow.uss.

### Per-Turn Undo Rollback (F6, plugin 0.11.0)

`TurnUndoTracker.cs` + `RestoreButton.cs` wrap each agent turn in a named Unity Undo group. An amber **Restore** button appears after each turn and reverts that turn's scene mutations in one click (native Unity Undo, scene-only). Only the last turn's button is active; older buttons disable when a new turn starts. Resumed-after-domain-reload turns also get a group.

Files: `TurnUndoTracker.cs` (group lifecycle), `RestoreButton.cs` (button UI + revert logic), `MCPChatWindow.Undo.cs` (partial, split from MCPChatWindow.cs), `.chat-btn--restore` in `MCPChatWindow.uss`.

**Reusable Primitive:** Built on a new public `UndoGroupHelper` core API (4 methods: `OpenNamedGroup`, `CloseNamedGroup`, `RevertToBeforeGroup`, `CanRevert`). Upcoming F27 (atomic batch rollback) will reuse this same system — one rollback mechanism, not two.

**Tests:** 11 NUnit EditMode tests green (TurnUndoTrackerTests 9/9, RestoreButtonTests 2/2). Core `UndoGroupHelper` has 6 NUnit EditMode tests.

**Result:** Agents can now safely mutate scene state with instant undo per turn. Full isolation: behind UNITY_MCP_CHAT define. 9 EditMode tests in Chat, 6 EditMode tests in Core.

### Chat Context Resolution via Chips (F2, plugin 0.9.0)

`ChipContextResolver.cs` resolves object-path chips to plain text at send-time. Three depth levels:

1. **PathOnly** — just the path (e.g., `/Enemies/Boss`)
2. **Summary** — path + top 3 non-Transform components (e.g., `/Enemies/Boss (Health, Animator, Collider)`)
3. **Full** — path + all components with serialized state

**Resolution logic:**
- **One chip** → Full depth (rich context for single object)
- **Many chips** → Summary depth (token budget)
- **Asset paths** → PathOnly (no components)
- **Budget cap** (2000 chars) → if Full exceeds cap, fall back to Summary

**Integration:** Wired into MCPChatWindow's send path via `OnSend` callback + `AttachScreenshot`. Before sending user message, `ChipContextResolver.ResolveAll()` translates each chip to plain text and inlines it. Reuses `SelectionSummary` + `ComponentSerializer` (DRY).

**Result:** Eliminates 1–3 `get_component` round-trips agents used to make on first turn with chipped objects. 12 NUnit EditMode tests green.

### Humanized Tool Card Rendering

Stream-json output from `claude -p` emits raw JSON tool cards. Chat parses and humanizes them to plain English:

**Raw:** `{"type":"tool_use","id":"t1","name":"set_property","input":{"path":"/Enemies/Boss","component":"Health","property":"value","value":"100"}}`

**Rendered:** `🔧 Editing /Enemies/Boss (Health.value = 100)`

Mapping in `ToolVerbMap.cs` (tool name → human action).

### Drag-Drop GameObjects / Assets

- Drag a GameObject or asset into the chat input → creates a clickable "chip"
- Chip text: stable hierarchy path (e.g., `/Player/Sword`)
- Chip click: `PingObject(path)` + `SelectObject(path)` (Unity editor highlights the object)
- On scene change, chips invalidated (path refs are scene-relative)

### Auto-Include Selection Context (F4, plugin 0.7.0)

**SelectionSummary.cs** prepends the active GameObject's context to user messages. Format:

```
[Selection: /Path/To/GameObject (Component1, Component2, Component3)]

<user message>
```

Extracts top 3 non-Transform components; deduped against existing object-chip references. Result: Claude always knows what you're editing without explicit mention. Deferred rendering; chip paths persisted but not repainted after domain reload (UX-only; turn executes with correct context).

### Screenshot Attach

- Capture button → `MultiViewCapture` (4-panel: Front, Left, Top, Isometric)
- Attach screenshot to next user message
- Sends as base64-encoded binary in the stdin JSON turn

### Ask / Agent Mode Toggle

Two permission modes:
- **Ask** (`--permission-mode plan`) — tool calls require user acknowledgment before executing
- **Agent** (`--permission-mode acceptEdits`) — tool calls auto-execute with confirmation only on mutations

User can toggle mid-conversation via settings dropdown.

### Domain-Reload Safety & Turn Survival (F4, plugin 0.7.0)

### Reload Guard (ReloadGuard.cs)

When a turn is in-flight, prevents domain reload from interrupting by calling `EditorBuildSettingsScenes.LockReloadAssemblies()`. Lifecycle:

1. **On turn start:** Acquire lock via `LockReloadAssemblies()` (blocks Unity domain reload)
2. **Watchdog timer:** 120s countdown; if turn completes, unlock early. If timer fires, auto-unlock (fail-safe)
3. **On turn done:** Release lock immediately via `UnlockReloadAssemblies()`

Result: Domain reload queued during a turn waits until the turn finishes, so the chat session survives intact.

### Pending Turn State (PendingTurnState.cs)

Serializes in-flight turn state to `Library/MCP_ChatPendingTurn.txt` (plain-text pipe-delimited, base64-encoded payload). Format: `sessionId|turnId|requestJson_b64`. On `afterAssemblyReload`, the window's `OnEnable` reads the file and calls:

```csharp
ClaudeBackend.ResumeAsync(sessionId)  // via --resume <sessionId>
```

The CLI's `--resume` flag loads prior message history (via `load_session`) and continues the in-flight turn with the same context, picking up where it left off.

**Persistence:** Plain-text, survives recompilation and process restart. Cleaned up after resume or on window close.

### Sent Text Cache (SentTextCache.cs)

Tracks recently sent text (last 10 messages) to dedup against accumulated text during resume. Prevents duplicate context on reconnect.

### Orphan Process Cleanup

- Child `claude` process PID stored in `SessionState` (Editor-scoped serialization)
- On assembly reload (domain reload), cleanup task kills the PID via `Process.Kill()`
- Prevents zombie processes on recompilation or script reload

## UX Sprint Features (v0.15.0)

### Feature F1 — Token Counter Reset on Backend/Model Switch

`TokenResetTests.cs` ensures token counters reset when user switches backend (Claude → Codex) or selects a different model. Implemented in `MCPChatWindow.Selector.cs`:

```csharp
void SelectModelDropdown_OnChange(ChangeEvent<string> evt) {
    ResetTokenCounters();
    CreateBackendWithSession();
}
```

Result: No stale token carry-over across model changes.

### Feature F2 — Cascade Restore (Undo Earlier Turns)

`RestoreButton.cs` + `TurnUndoTracker.RestoreFromIndex()`: User can restore any earlier turn (not just the last one). Clicking Restore on turn 3 reverts turns 3, 4, 5 in reverse order (cascade rollback via sequential `UndoGroupHelper.RevertToBeforeGroup()`).

**New method:** `RestoreFromIndex(int turnIndex)` iterates from tail backward, reverting each turn's Undo group. Verified in TurnUndoTrackerTests (9/9 green).

### Feature F3 — Approve Button Shows Only for Real Tool Calls

`MCPChatWindow.Drain.cs` + `ApproveButtonFactory.cs`: The "Approve & Execute" button is injected only when a turn has real tool calls (`_turnHasToolCalls = true`). Turns with pure prose responses never show the button, eliminating UI clutter.

**Verification:** ApproveFlowTests check flag gating.

### Feature F4 — Hierarchy Refs Carry #instanceID for Disambiguation

`SelectionSummary.Summarize()` + `ChipContextResolver.ResolveOne()`: When a scene has duplicate object names (e.g., two "Enemy" GameObjects), the chip path now includes the Unity instance ID: `/Enemy #12345`. Enables Claude to distinguish them.

**Format:** `path #<instanceID>` (appended by ChipContextResolver at send-time). Verified in SelectionSummaryTests (path-only scene objects gain #ID markers).

### Feature F5 — Inline Removable Chips + Drag-Drop + Context Menu

`InlineChipData.cs` + `InlineChipTracker.cs` + `InlineChipOverlay.cs` + `InlineChipKeyHandler.cs`: Type objects directly into the composer via drag-drop. Chips appear as removable pills (✕) at cursor (fallback: top-left row if char-rect API unavailable in 2021).

**Drag-drop routing** (`Chips.cs` OnDragPerform): hit-test vs `_input.worldBound` chooses inline vs strip chip.

**Context menu** (`MCPChatWindow.cs` OnContextualMenuPopulate): "Add Selection to Context" inserts chip at cursor.

**Tracker logic** (`InlineChipTracker.SyncToText()`): Common-prefix/suffix diff detects which U+FFFC marker was deleted → drops matching chip (handles backspace, selection-delete, paste).

Verified: InlineChipTrackerTests 13/13 green, visual/interactive paths compile clean.

### Feature F6 — Auto-Scroll Toggle

`MCPChatWindow.Drain.cs`: EditorPref gate for auto-scroll behavior. Default ON. When OFF, streaming messages do not auto-scroll; user can read top of transcript while turn completes.

**Wired in:** `Drain()` loop checks `EditorPrefs.GetBool(PrefKey.AutoScroll, true)` before calling `ScrollViewMode.Scroll()`.

### Feature F7 — Status Panel Distinguishes CLI-Listening vs Chat-Active

`ChatBackendProbe.cs` (reflection-based, domain-reload safe): Detects if chat backend is running via reflection on `MCPChatWindow.s_instance`. `MCPStatusModel.GetState()` now returns 3-state enum:
- Down (no server)
- Listen (TCP running, no chat)
- ChatActive (Chat window running)

**Reflection:** `Type.GetProperty("IsRunning")` on Chat assembly (if loaded). Domain-reload safe: re-queried per call, no static cache.

Verified: MCPStatusModelTests include ChatActive state transitions.

### Feature F8 — Remove "(Beta)" Labels

`MCPSettingsUI.cs` + `ChatSettingsSection.cs`: Removed "(Beta)" from:
- Chat toggle button in MCPSettings
- Chat settings foldout header

Result: UI looks shipping-ready.

### Feature F9 — Per-Backend Settings Form → Own JSON → CLI Args

`BackendConfig.cs` + `BackendConfigStore.cs` + `BackendSettingsForm.cs`:

**Settings form** (UIToolkit dropdowns per backend):
- Claude: model (Opus, Sonnet, Haiku), permission mode (plan, acceptEdits), timeout, extra args
- Codex: same axes

**Persistence:** Writes to `Library/MCP_ChatBackendConfig.json` (project-local, NOT ~/.codex/config.toml or ~/.mcp.json). Format:
```json
{
  "claude": { "model": "opus-4-1", "permission_mode": "acceptEdits", "extra_args": "--verbose" },
  "codex": { "model": "default", "permission_mode": "plan" }
}
```

**Arg wiring:** `ClaudeArgBuilder.BuildArgs()` + `CodexArgBuilder.BuildArgs()` read from config and inject into argv (e.g., `--model=<model>`, extra args split on whitespace).

**DRY:** `ArgTokenizer.cs` (new, shell-style quote-aware split) centralizes whitespace+quote parsing for both builders. +11 tests.

Verified: BackendConfigStoreTests 10/10, BackendSettingsFormTests integration passes.

### Feature F10 — Typed Context Tags (Kind-Aware Chips)

`ChipKindDetector.cs` + `ChipData.Kind` + `ChipConfig.cs` + `ResponseTagInliner.cs`:

**Send-side (input):**
- Each chip carries a `ChipKind` (Hierarchy, Scene, Script, Prefab, Material, Texture, ScriptableObject, Asset)
- AI-facing format: `[hierarchy:/Player #123]`, `[script:PlayerController]`, `[scene:.../Main.unity]`
- Depth configurable per kind (none|path|summary|full, stored in BackendConfigStore.ChipConfig)
- Chips display left-side color-coded kind prefix (visual feedback)

**Receive-side (response):**
- `ResponseTagInliner.Apply()` parses ONLY `[kind:ref]` format (conservative regex, no false positives on markdown/code/bare brackets)
- Renders compact colored pills with `<link>` click-nav (symmetric with input chips)
- Wired into `MarkdownInline` between escape and bold/italic

**Classes:**
- `ChipKindDetector.Detect()` → ChipKind (pure, reflection-based hierarchy vs scene discrimination)
- `ChipContextResolver.EmitTyped()` + `ResolveAllTyped()` — send-time API
- `ResponseTagInliner` — response-time parser + renderer

Verified: ChipKindDetector 13/13, ResponseTagInliner 17/17 (false-positive guards), EmitTyped 7/7, ChipConfig 3/3.

### Feature F11 — Inline Chips + Extensible Chip-Kind Registry (v0.15.8)

#### Extensibility: IChipKindProvider & ChipKindRegistry

**Core Innovation:** Third-party plugins (in separate asmdefs referencing `UnityMCP.Editor.Chat` + defining `UNITY_MCP_CHAT`) register custom chip kinds via the public `ChipKindRegistry` with ZERO core edits.

**Public Interface:**
```csharp
public interface IChipKindProvider
{
    string Key { get; }                    // Unique lowercase key, e.g. "hierarchy"
    int Priority { get; }                  // Lower = checked first
    bool CanHandle(Object obj, string assetPath);
    ChipData Create(Object obj, string assetPath);
    string IconName { get; }               // EditorGUIUtility.IconContent key
    string HexColor { get; }               // Pill + response tag color
    string FormatPayload(ChipData chip, ChipPayloadContext ctx);
    string DefaultDepth { get; }           // Fallback when no config entry
    void Navigate(string reference);       // Handle click on chip link
}
```

**Public Registry:**
```csharp
public static class ChipKindRegistry
{
    public static bool Register(IChipKindProvider p);
    public static bool Unregister(string key);
    public static IChipKindProvider Resolve(Object obj, string assetPath);
    public static IChipKindProvider ForKey(string key);
}
```

**Built-in Providers (8 total, Priority 100–800):**
- HierarchyChipProvider (100): GameObjects not in assets
- SceneChipProvider (200): .unity scene files
- ScriptChipProvider (300): MonoScript C# files
- PrefabChipProvider (400): .prefab files
- MaterialChipProvider (500): .mat material files
- TextureChipProvider (600): .png/.jpg image files
- ScriptableObjectChipProvider (700): .asset SO files
- AssetChipProvider (800): generic fallback for unlisted asset types

**Priority Convention:**
- <100: Plugin providers override a built-in type
- 100–800: Built-ins (default)
- >800: Plugin providers extend (new kinds)

**Reload Survival (PendingTurnState v4):** Serializes `KindKeys[]` parallel to chip paths; on resume, re-binds by key. Falls back to re-detection if provider not yet registered.

#### Inline Rendering at Cursor

**Positioning (UitkCharRect.cs):** Uses PUBLIC `TextField.textSelection.GetCursorPositionFromStringIndex` API — confirmed working live on Unity 6000.3.0b7. H10 degradation: if API unavailable, falls back to row-layout strip (current behavior).

**Width Reservation (NbspReservation.cs):** Reserves pill width via U+FFFC marker + N×U+00A0 (non-breaking spaces), ensuring layout won't reflow when pill moves.

**Atomic Caret (TokenSpan.cs):** Caret skips whole chips (never lands mid-pill). Backspace on chip deletes entire chip (not character-by-character). Press arrow → moves caret before/after chip boundary.

**"Show LLM Payload" Context Menu:** Right-click on chip → reveals exact byte-for-byte payload sent to AI (symmetry test enforces match).

**Breaking Change — BUG B:** `ChipConfig` default depth `"summary"` → `"path"` (token-minimal). Restore via F9 settings form (per-kind dropdown). Marked in-code: `// BREAKING (H15)`.

#### Test Coverage

- **ChipKindRegistryTests:** Register, Unregister, Resolve, ForKey, priority ordering, version bumping
- **ChipKindRegistryPipelineTests:** End-to-end: detect → resolve → format → render
- **NbspReservationTests:** Width prediction, marker insertion/cleanup
- **TokenSpanTests:** Atomic caret boundaries, backspace/arrow behavior
- **UitkCharRectProbeTests:** Positioning API availability detection, H10 fallback
- **Wave4ChipInputTests:** Integration: drag-drop, context menu, serialization

All suites: 100% pass, zero new failures (5 pre-existing reds unrelated to F11).

### Review-Hardening Pass (v0.14.6)

**ArgTokenizer Quote-Awareness:** Fixes silent corruption of quoted multi-word ExtraArgs values (e.g., `--append-system-prompt "be terse"`). Shell-style: double+single quotes, unbalanced trailing tolerated. DRY across both Claude/CodexArgBuilder. +11 tests.

**ChatBackendProbe Reload-Safety:** Drops stale static MethodInfo cache; resolved per-call so status stays correct across domain reloads (was wrongly showing Listen when Chat was active).

**Dedup BackendConfigStore.Load():** MCPChatWindow.OnSend/AttachScreenshot now load store once and thread into AppendChipContext (lazy ??= fallback), avoiding double file-read+parse per turn.

### Binary Resolution on macOS

**Problem:** Finder-launched Unity has a minimal PATH; `claude` binary may not be found.

**Solution:** Wrap the invocation in `/bin/zsh -lc`:

```csharp
var psi = new ProcessStartInfo
{
    FileName = "/bin/zsh",
    Arguments = "-lc 'claude -p --mcp-config ... > /tmp/claude.log 2>&1'",
    UseShellExecute = false,
    RedirectStandardInput = true,
    RedirectStandardOutput = true
};
```

This ensures the child shell inherits the user's `.zshrc` PATH and finds `claude`.

## File Layout

```
unity-plugin/Editor/
├── AssemblyInfo.cs                   # [assembly: InternalsVisibleTo("UnityMCP.Editor.Chat")]
├── Chat.meta                         # Meta for Chat/ folder
├── Chat/
│   ├── ChatEvent.cs                  # Normalized event struct
│   ├── ChatStreamParser.cs           # Parse stream-json from stdout
│   ├── ClaudeArgBuilder.cs           # Build --mcp-config JSON + --permission-mode
│   ├── UserTurnBuilder.cs            # Encode user message → stdin JSON
│   ├── ToolVerbMap.cs                # Tool name → humanized action
│   ├── IChatBackend.cs               # Backend interface
│   ├── ChatBinaryResolver.cs         # Binary PATH resolution
│   ├── ChatProcess.cs                # Process lifecycle manager
│   ├── ClaudeBackend.cs              # Implementation: spawns claude CLI
│   ├── ChatTranscript.cs             # In-memory message history
│   ├── MCPChatWindow.cs              # EditorWindow UI + interaction
│   ├── MCPChatWindow.Drain.cs        # Partial: message accumulation
│   ├── MCPChatWindow.FlowBar.cs      # Partial: activity indicator animation
│   ├── MCPChatWindow.uss             # UIToolkit styling
│   ├── ChatSettingsSection.cs        # Settings foldout in MCPSettings
│   ├── CliBackendBase.cs             # Abstract host for CLI backends (4 axes)
│   ├── CodexArgBuilder.cs            # Codex: argv + env-key-strip builder
│   ├── CodexStreamParser.cs          # Codex: NDJSON → ChatEvent
│   ├── CodexBackend.cs               # Codex: IChatBackend implementation (spawn-per-turn)
│   ├── BackendRegistry.cs            # Backend factory + enum
│   ├── ReloadGuard.cs                # Domain-reload: lock + unlock mechanism
│   ├── PendingTurnState.cs           # Domain-reload: persist in-flight turn state (v3: BackendKind)
│   ├── SelectionSummary.cs           # Auto-Selection: prepend active GameObject context
│   ├── SentTextCache.cs              # Domain-reload: track sent text for dedup
│   ├── CompileAutoFix.cs             # Auto-retry on compile failures (MAX_RETRIES=3)
│   ├── EditorStateSnapshot.cs        # Inject context block (scene, compile, errors)
│   ├── ToolPing.cs                   # Flash object on tool-call completion
│   ├── ChipContextResolver.cs        # Resolve object chips to plain text at 3 depths
│   ├── MCPChatWindow.Approve.cs      # Event handler: Approve & Execute button
│   ├── ApproveHelper.cs              # Session management: resume, mode flip
│   ├── ApproveButtonFactory.cs       # Button builder: humanized button UI
│   ├── SlashTemplate.cs              # Template model: enum ContextGather + struct
│   ├── SlashRegistry.cs              # Template registry: Builtins, Match, Resolve
│   ├── SlashPopup.cs                 # UIToolkit popup: 5 visible, arrow nav
│   ├── MCPChatWindow.Slash.cs        # Slash setup: KeyDown + ChangeEvent on parent
│   ├── UnityMCP.Editor.Chat.asmdef   # Assembly definition (references Core)
│   └── Tests/
│       ├── ChatStreamParserTests.cs
│       ├── ClaudeArgBuilderTests.cs
│       ├── UserTurnBuilderTests.cs
│       ├── ToolVerbMapTests.cs
│       ├── CliBackendBaseTests.cs              # Tests base lifecycle + 4-axis dispatch
│       ├── CodexArgBuilderTests.cs             # Tests argv construction + env-key-strip
│       ├── CodexStreamParserTests.cs           # Tests Codex NDJSON → ChatEvent (26 cases)
│       ├── ReloadGuardTests.cs
│       ├── PendingTurnStateTests.cs            # Tests v3 header + BackendKind persistence
│       ├── SelectionSummaryTests.cs
│       ├── SentTextCacheTests.cs
│       ├── ApproveFlowTests.cs
│       ├── SlashRegistryTests.cs
│       ├── SlashPopupTests.cs
│       └── UnityMCP.Editor.Chat.Tests.asmdef
├── ChatSettingsHook.cs               # Event hook for settings updates
├── MCPSettingsUI.cs                  # Modified: fires ChatSettingsHook.Invoke
└── [other core files]
```

## Enabling the Feature

### In Player Settings (Editor)

1. **Edit > Project Settings > Player > Other Settings**
2. **Scripting Define Symbols** → add `UNITY_MCP_CHAT`
3. Editor recompiles; `Chat/` asmdef is now active

### In MCPSettings Window

1. **Window > UnityMCP > Settings**
2. Scroll to **Agent Chat** section
3. Toggle **Enable Agent Chat** checkbox
4. Configure mode (Ask / Agent) and binary path (optional; auto-resolved on macOS)

## JSON-Only-at-Boundaries Principle

Internal models are C# **structs + plain text strings**. JSON appears ONLY at forced protocol boundaries:

- **stdin** — user turn envelope (JSON): `{"messages":[...], "attachments":[...]}`
- **stdout** — claude stream-json events (JSON): `{"type":"message_start",...}`
- **--mcp-config** — config file (JSON): defines MCP server
- **--permission-mode** — CLI arg (string): "plan" or "acceptEdits"

All intermediate parsing → plain C# objects (ChatEvent, ChatTranscript, ToolCard, etc.). Humanized output is plain text strings (`"🔧 Editing..."`), not re-encoded JSON.

**Token savings:**
- Omit JSON serialization inside Chat logic (→ no JsonConvert overhead)
- Humanize at parse time (→ one-pass JSON→text, not JSON→object→JSON)
- No intermediate JSON round-trips

## Testing

Chat module has 4 NUnit suites (EditMode only, no Live dependency):

- `ChatStreamParserTests` — Parse raw stream-json, emit ChatEvent structs
- `ClaudeArgBuilderTests` — Generate --mcp-config file + args
- `UserTurnBuilderTests` — Encode user messages → stdin JSON
- `ToolVerbMapTests` — Tool name → humanized text

Run via **Window > TextExecution > Test Runner** when `UNITY_MCP_CHAT` is enabled and `UNITY_INCLUDE_TESTS` is also defined.

## Billing / Terms of Service

**Important:** Enabling MCP Chat spawns the **user's own** locally-installed `claude` CLI using **their own** logged-in Claude subscription. Usage, credits, and Anthropic Terms of Service are **between the user and Anthropic**. This feature does NOT proxy, cache, or share login credentials. Each user drives their own `claude` binary independently.

## Content Rendering

The Chat module includes an **extensible render subsystem** for displaying rich Markdown and Mermaid flowcharts in the transcript.

### Markdown Rendering

**Pipeline:** `string` (raw) → `MarkdownParser.Parse()` → `List<MdBlock>` → registry → `VisualElement` trees

- **MdBlock.cs** — Block model: enums `Heading`, `Paragraph`, `CodeFence`, `Mermaid`, `BulletList`, `OrderedList`, `BlockQuote`, `HorizontalRule`, `Table`, `Image` with metadata (Level, Lang, Lines, TableRows, Src/Alt).
- **MarkdownParser.cs + .Blocks.cs** — Single-pass string→blocks: fences parsed FIRST (lang==`mermaid` → Mermaid else CodeFence), `![alt](src)` standalone lines → Image blocks, table separator peek-ahead detection.
- **MarkdownInline.cs** — Rich-text escaping (angle-brackets FIRST, then inline markup): `**bold**`, `*italic*`, `` `code` ``, links `[text](url)` (renders text + dim URL), code-span protects inner stars.

**Renderers:**
- **MarkdownBlockRenderer** — dispatch 8 kinds (heading/paragraph/code/blockquote/rule/lists/table), partial files for table grid and bullet/ordered list layout
- **ImageBlockRenderer** — PNG/JPG paths/bytes → Texture2D, click opens via `EditorUtility.OpenWithDefaultApp`, textures freed on `DetachFromPanelEvent`

### Native Mermaid Flowchart Support

**Pure parse/layout stack (NO external library):**
- **MermaidGraph.cs** — POCO model: nodes (rect/round/diamond shapes), edges (with optional labels), direction (TD/LR/RL/BT)
- **MermaidParser.cs** — lines → graph or null (non-flowchart syntax → null); chained edges `A-->B-->C`, self-loops, labels non-greedy
- **MermaidLayout.cs + .Layers.cs** — Kahn topological sort + longest-path layering, pixel rects (float, no Vector2); cycle/self-loop guarded via visited-set cap; edge endpoints on node border not center. **Dynamic node sizing:** `MeasureNode(label)` calculates width from text lines + char-width estimate (fixes hardcoded 120px distortion). Bounds clamped (minW=60, maxW=280, minH=30, maxH=120) to prevent explosion on long text.
- **MermaidBlockRenderer** — `CanRender`= Mermaid kind; delegates to MermaidView; code-box fallback when TryBuild false
- **MermaidView.cs** — Absolute-positioned VE nodes + Label + edge overlay; **MANDATORY `edgeLayer.RegisterCallback<GeometryChangedEvent>(_ => edgeLayer.MarkDirtyRepaint())`** for edge redraws on resize
- **MermaidEdgePainter.cs** — Painter2D lines + arrowhead chevrons; no box-shadow, no transform (2021.3-safe)

### Extensible Registry Seam (Open/Closed Principle)

New content types = **1 new renderer file + 1 line in factory**, zero elsewhere edits.

- **IChatBlockRenderer.cs** — Interface: `bool CanRender(in MdBlock)`, `VisualElement Render(in MdBlock)`
- **ChatBlockRendererRegistry.cs** — Ordered, first-match-wins, Label fallback (never null)
- **ChatBlockRendererFactory.cs** — `CreateDefault()`: registers Mermaid + Image FIRST, MarkdownBlockRenderer LAST (catch-all)

**Future proof:** To add a 3D model preview renderer: (1) add `Model3D` to `MdBlockKind`, (2) parser maps fenced `lang=="unity-model"` → block, (3) new file `Model3DBlockRenderer : IChatBlockRenderer`, (4) one line in factory `reg.Register(new Model3DBlockRenderer())`. Done.

### Streaming → Finalize Strategy

Two-phase accumulation:
1. **Stream live** — plain text enters a Label (current behavior), accumulated into `_assistantRaw` StringBuilder
2. **Finalize on TurnDone** — `FinalizeAssistant()` clears live label, re-renders accumulated raw via `MarkdownParser.Parse()` + registry, replaces row children with rendered blocks

Called from `AppendUserBubble` + `AppendToolChip` so interrupted segments + text-between-tools each get their own bubble.

**Pinned invariant:** In `AppendOrExtendAssistant` null-branch: (1) `_assistantRaw.Clear()` FIRST, (2) create new row + label, (3) then (BOTH branches) append token. Raw is cleared exactly when a new live label begins.

### Texture Lifecycle

`ImageBlockRenderer`: `Texture2D` created from bytes → attached to `Image` VE → `DetachFromPanelEvent` callback destroys via `Object.DestroyImmediate()`. Eviction (first message dropped), finalize clears all children, OnDisable detaches all → callback fires for each texture.

### UX: Enter-to-Send + Removable Chips + Interactive Scene/Script Refs

- **EnterKeySend.cs** — Pure `Classify(KeyDownEvent)` → enum (Send/Newline/Ignore) + `InsertNewline(ref Caret)` logic (NUnit-testable); `Attach()` glue registers KeyDownEvent TrickleDown callback → Send calls `StopPropagation()` + `StopImmediatePropagation()` + `PreventDefault()` + onSend; Newline inserts `\n` at caret.
- **MCPChatWindow.Chips** partial — `AddObjChip(path)` + `CollectChipPaths()` → HashSet dedup; chip.userData=path; ✕ remove button = `_objChipStrip.Remove(chip)`. Ping moves to label on click.
- **Interactive Refs** — Chat messages can embed reference links via inline syntax `obj:/Path/To/Obj` or `script:Assets/MyScript.cs`. **ChatRefResolver** scans hierarchy at startup, **ChatRefAction** installs click/context-menu handlers (click=navigate+PingObject, Alt+click="Add to Context" → inject into input). LinkTag rendering (Unity rich-text `<link="obj:/...">`), hover tooltip, right-click menu with "Navigate" + "Add to context" options.
- **Tool-Call Grouping** — Multiple tool events from same tool call (e.g., 3 set_property on same object) group into 1 chip via ID tracking. Eliminates scatter when Claude chains mutations.
- **Copyable Text** — All transcript Labels have mouse selection enabled (drag select copies to clipboard). New CopyableText wrapper + CopyTextBuilder for multi-line copy blocks.

### Styling

**MCPChatWindow.uss** — ~156 lines appended: md-* classes (bubble, heading-1–6, code, code-fence, blockquote, hr, list-bullet, list-ordered, table, table-row, table-cell), mermaid-* (bubble, node-rect, node-round, node-diamond, edge-arrow), md-image + md-image-alt, obj-chip-remove. House palette: `#16161e/#1e1e2e/#2a2a44/#3a6aaa/#7aa2f7/#c0caf5/#d0d8ff`.

## Implementation Notes

### Why Spawn vs. Sidecar

- **No sidecar server needed** — reuses existing `unity_mcp.server` via the spawned CLI's MCP config
- **No API key exposure** — uses subscription auth from disk (logged-in CLI session)
- **Per-user isolation** — each Unity instance is independent
- **Natural upgrade path** — if user upgrades their `claude` CLI, MCP Chat auto-benefits

### macOS PATH Gotchas

- Finder-launched Unity has minimal PATH (e.g., `/usr/bin:/bin:/usr/sbin:/sbin`)
- `claude` binary typically installed in `/opt/homebrew/bin/claude` or user-local `~/.local/bin/claude`
- Solution: spawn via `/bin/zsh -lc 'claude ...'` to inherit user's shell config (`.zshrc`)
- Alternative: user can set `CLAUDE_PATH` env var in MCPSettings to override auto-resolution

### MCP Config Generation via ChatMcpConfigWriter

The `--mcp-config` file is auto-generated by `ChatMcpConfigWriter` at runtime, deriving the Python server path from the UPM package location. Resolution chain:

1. Probe `server/.venv/bin/python` (local venv)
2. Resolve absolute path via `uv` tool
3. Fall back to `python3` (system PATH)

No port is embedded in the config — the server self-discovers its port via `~/.unity-mcp/ports/*.port` (PID-locked file written on startup). The hardcoded `~/.claude/mcp.json` path is now a fallback only (used if config generation fails). This eliminates the need for manual setup in most cases.

### Prose-Fallback for Headless Chat (--disallowedTools AskUserQuestion)

**Problem:** The built-in `AskUserQuestion` tool auto-fails when Claude runs in headless stream-json mode (no stdin interactivity). The spawn writes JSON questions to the tool card, but Unity has no way to capture user input back through stdin within the stream. Response: timeout (~500ms), tool fails, context lost.

**Solution:** In `ClaudeArgBuilder`, add `--disallowedTools AskUserQuestion` to the CLI args. This tells Claude's built-in tool-use logic to skip the tool and instead respond with prose text describing what it would ask. Example:

```
Claude normally: [tool_use AskUserQuestion ("What color?")]
With disallowedTools: "What color would you like for the particle system? (I would ask you, but I can't do that in this mode.)"
```

**Result:** No tool-call failures, context-preserved prose question, user can paste answer into next input. Cost: ~200 tokens per question (prose vs. tool card), acceptable trade-off.

### Domain Reload Lifecycle

1. User edits a C# script in the Chat assembly or core
2. Unity detects domain reload, fires `[InitializeOnLoad]` finalizers
3. Chat's orphan-cleanup task reads PID from SessionState, calls `Process.Kill()`
4. Domain reload completes; Chat window re-initializes on next EditorApplication.update
5. User can start a new chat session

## Known Limitations

- **ChipPath Repaint After Resume:** Object chips are persisted via `PendingTurnState` and restored after domain reload, but the chip strip UI is not repainted. The turn executes with correct context; the visual strip just shows stale paths until the next user message. This is a cosmetic UX issue; the actual turn data is correct.
- **MCPChatWindow Line Count:** At 185 lines (approaching the 200-line ceiling), the file may need splitting into more partials if significant features are added.

## Related

- **Core Architecture:** `AI/architecture.md` (CommandRouter, TCP bridge, tools catalog)
- **TCP Bridge:** `AI/tcp-bridge.md` (4-byte framing, heartbeat, SO_KEEPALIVE)
- **MCP Server:** `AI/mcp-server.md` (Python FastMCP, deferred schema loading, plugin system, tool gating)
- **Changelog:** `AI/changelog.md` (feature timeline)
