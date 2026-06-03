# Unity MCP

MCP server for controlling Unity Editor from Claude Code (or any MCP client).

## What Can It Do?

- **Create and inspect scene objects** — create GameObjects, add components, set properties
- **Batch operations** — run multiple commands in a single call (10-15x token savings)
- **Run tests** — trigger Unity Test Runner and get results
- **Take screenshots** — capture scene views, multi-view, or camera perspectives
- **AI-powered intents** — high-level commands like `ui_intent`, `animator_intent`, `vfx_intent` that build complex setups from natural language
- **Optional: In-Unity Agent Chat** — EditorWindow for agentic chat directly in Unity (OFF by default, enable via `UNITY_MCP_CHAT` scripting define)

## Requirements

- Python >= 3.10
- Unity >= 2021.3
- Claude Code (or any MCP client supporting stdio transport)

## Quick Start

### 1. Install the Python server

```bash
cd server
pip install -e ".[dev]"
```

### 2. Install the Unity plugin

Option A — `manifest.json` (recommended):

```json
{
  "dependencies": {
    "com.unity-mcp.editor": "file:../unity-kiss-mcp/unity-plugin"
  }
}
```

Option B — Unity Package Manager UI: **Add package from disk** → select `unity-plugin/package.json`.

Once installed, Unity will show a TCP listener on port 9500 in the console.

### 3. Configure Claude Code

Add to `.claude/mcp_settings.json` (or your MCP client config):

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "python",
      "args": ["-m", "unity_mcp.server"],
      "cwd": "/path/to/unity-kiss-mcp/server"
    }
  }
}
```

Then restart Claude Code. The `unity-mcp` server will appear in your tools list.

### 4. Verify it works

```bash
# Python server tests (no Unity required)
cd server && pytest tests/ -v

# Check Unity is listening
lsof -i :9500
```

## Architecture

```
Claude Code ←─stdio─→ Python MCP Server ←─TCP:9500─→ Unity Editor Plugin
```

## Troubleshooting

**Port 9500 not open** — Open Unity and wait for the plugin to initialize. The console should print `[MCP] Server started on port 9500`.

**Plugin not loading after install** — Click on the Unity window to trigger recompilation, or use Assets > Reimport All.

**"Connection refused" in MCP server** — Unity must be running with the plugin installed. The server will retry automatically on reconnect.

**Changes not reflected** — After C# code edits, Unity needs focus to recompile. Run `osascript -e 'tell application "Unity" to activate'` to trigger it.

## Billing Note (Optional Agent Chat)

If you enable the optional in-Unity Agent Chat window (via `UNITY_MCP_CHAT` scripting define), it spawns your locally-installed `claude` CLI using your own logged-in Claude subscription. Usage, credits, and Anthropic Terms of Service are between you and Anthropic. This feature does not proxy, cache, or share your login credentials — each user drives their own CLI instance independently.

## License

MIT — see [LICENSE](LICENSE) for details.
