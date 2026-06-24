# Chat Backends Guide

In-Unity chat supports multiple CLI-based LLM backends.

## Supported Backends

| Backend | CLI | Model | Persistent | Status |
|---------|-----|-------|-----------|--------|
| Claude | claude | Claude 4 (Opus, Sonnet) | Per-turn | ✅ Stable |
| Claude Desktop | claude | Claude 4 (Opus, Sonnet) | Per-turn | ✅ Stable |
| Codex | codex | OpenAI GPT-4 | Session | ✅ Stable |
| Kimi | kimi | Kimi (Moonshot) | Per-turn | ✅ Stable |
| Antigravity | curl | Various | Per-turn | ✅ Stable |
| Cursor | cursor | Claude (Cursor config) | Per-turn | ✅ Stable |
| Windsurf | windsurf | Claude (Windsurf config) | Per-turn | ✅ Stable |
| VS Code | opencode | Various | Per-turn | ✅ Testing |

## Claude & Claude Desktop

**Executable:** `claude` (installed via `curl | bash` setup)

**Models:** Claude 4 Opus (reasoning), Claude 4 Sonnet (balanced), Claude 4 Haiku (fast, cost-effective)

**Session:** Per-turn (no session memory between turns in default config)

**Authentication:** Uses cached login from `claude auth login`

```python
# In Unity: MCP → Chat → Select "Claude"
# Then chat in the window
```

**Pricing:** See Anthropic's pricing page for current rates

## Codex (OpenAI)

**Executable:** `codex app-server`

**Models:** GPT-4.1, o3, o4-mini and newer

**Session:** Persistent (one process per chat, maintains conversation history)

**Protocol:** JSON-RPC 2.0 (not REST)

**Setup:**
```bash
# Install Codex CLI
npm install -g @openai/codex
codex login
```

**Note:** Pricing varies by model; check OpenAI pricing page.

## Kimi (Moonshot AI)

**Executable:** `kimi`

**Models:** Kimi (Chinese LLM), multilingual support

**Session:** Per-turn

**Setup:**
```bash
# Install Kimi CLI
curl -fsSL https://kimi.ai/install.sh | bash
kimi login
```

**Pricing:** Check Moonshot AI's pricing page for current rates.

## Antigravity

**Executable:** curl (HTTP client)

**Models:** Configurable backend (Claude via API, custom LLM)

**Session:** Per-turn

**Setup:**
```
Manual configuration in MCPSettings
- Base URL: https://api.antigravity.ai/...
- API key: User provides
```

## VS Code Extensions

### Cursor
**Executable:** cursor (Cursor IDE integrated)

**Models:** Claude (via Cursor's subscription)

**Session:** Per-turn

**Note:** Requires Cursor IDE installed; CLI auto-detected.

### Windsurf
**Executable:** windsurf (Windsurf IDE integrated)

**Models:** Claude (via Windsurf's subscription)

**Session:** Per-turn

**Note:** Requires Windsurf IDE installed; CLI auto-detected.

### OpenCode (VS Code)
**Executable:** opencode (VS Code plugin)

**Models:** Configurable (Claude, GitHub Copilot, custom)

**Session:** Per-turn (supports external MCP merge, v0.55.0+)

**Note:** Experimental; external MCP configs merged additively.

## Switching Backends in Unity

1. Open **MCP → Chat** in the menu
2. Click the **Backend** dropdown
3. Select Claude, Codex, Kimi, Antigravity, Cursor, Windsurf, or OpenCode
4. Enter API key if needed
5. Start chatting

**Persistence:** Selected backend is saved to EditorPrefs.

## Per-Turn vs Session Persistence

### Per-Turn
- New CLI process started each time (stateless)
- Faster startup (~100ms)
- No memory between turns
- Example: Claude, Kimi, Cursor

**Pros:**
- Simple; no session management
- Works offline (no session server)

**Cons:**
- Each turn must re-context the entire conversation
- Slower for long conversations

### Session (Persistent)
- Single CLI process for entire chat session
- Slower startup (~1s)
- Full conversation history maintained
- Example: Codex, Antigravity (if configured)

**Pros:**
- Faster per-turn response
- Rich conversation history
- Supports reasoning models (o3, etc.)

**Cons:**
- Requires process management
- Harder recovery after crash

## Configuration

### API Keys

Store in local settings:

```
Unity Editor → MCP → Settings → Chat → API Keys
```

**NOT stored in code or version control.**

### Environment Variables

Backend inherits only `UNITY_MCP_SESSION_TIMEOUT=300` (extended for reasoning models).

**Not injected:** `UNITY_MCP_PORT` (delivered via scoped --mcp-config only, v0.55.0+).

## Cost Tracking

**In-window display:**
- Token budget per day
- Current spend vs. cap
- Haiku cost breakdown (intent tools)

**Enable tracking:**
```
UNITY_MCP_BUDGET=1 environment variable
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "CLI not found" | See installation guide for your CLI (`docs/install/`) or add to PATH |
| "Authentication failed" | Run `<backend> auth login` to cache credentials |
| "Connection timeout" | Increase `UNITY_MCP_SESSION_TIMEOUT=600` for reasoning models |
| "No response" | Check Editor.log for connection errors; restart chat window |
| "Tool not available" | Upgrade backend CLI via its own updater — see `docs/install/` for your CLI |

## Switching Mid-Conversation

**Warning:** Switching backends loses current session context.

**Recommended:** Let conversation finish, then select new backend for next chat.

---

**See also:** `docs/chat/annotation.md` for visual tools, `docs/features/intent-tools.md` for LLM-driven automation.
