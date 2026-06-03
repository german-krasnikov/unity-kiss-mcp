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

**MVP implementation:** `ClaudeBackend` (the only backend shipped). Cursor/Codex are future seams (not implemented). Future: add `IChatBackend` subclasses in separate plugins if needed.

**ChatEvent struct:**
- Normalized event type (ToolCard, ToolResult, UserMessage, Error, Status, Done)
- Humanized text output (e.g., "Editing /Enemies/Boss" not raw JSON)
- Raw event data preserved for debugging

## Features

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

### Screenshot Attach

- Capture button → `MultiViewCapture` (4-panel: Front, Left, Top, Isometric)
- Attach screenshot to next user message
- Sends as base64-encoded binary in the stdin JSON turn

### Ask / Agent Mode Toggle

Two permission modes:
- **Ask** (`--permission-mode plan`) — tool calls require user acknowledgment before executing
- **Agent** (`--permission-mode acceptEdits`) — tool calls auto-execute with confirmation only on mutations

User can toggle mid-conversation via settings dropdown.

### Orphan Process Cleanup

- Child `claude` process PID stored in `SessionState` (Editor-scoped serialization)
- On assembly reload (domain reload), cleanup task kills the PID via `Process.Kill()`
- Prevents zombie processes on recompilation or script reload

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
│   ├── MCPChatWindow.uss             # UIToolkit styling
│   ├── ChatSettingsSection.cs        # Settings foldout in MCPSettings
│   ├── UnityMCP.Editor.Chat.asmdef   # Assembly definition (references Core)
│   └── Tests/
│       ├── ChatStreamParserTests.cs
│       ├── ClaudeArgBuilderTests.cs
│       ├── UserTurnBuilderTests.cs
│       ├── ToolVerbMapTests.cs
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

### Domain Reload Lifecycle

1. User edits a C# script in the Chat assembly or core
2. Unity detects domain reload, fires `[InitializeOnLoad]` finalizers
3. Chat's orphan-cleanup task reads PID from SessionState, calls `Process.Kill()`
4. Domain reload completes; Chat window re-initializes on next EditorApplication.update
5. User can start a new chat session

## Related

- **Core Architecture:** `AI/architecture.md` (CommandRouter, TCP bridge, tools catalog)
- **TCP Bridge:** `AI/tcp-bridge.md` (4-byte framing, heartbeat, SO_KEEPALIVE)
- **MCP Server:** `AI/mcp-server.md` (Python FastMCP, plugin system, tool gating)
- **Changelog:** `AI/changelog.md` (feature timeline)
