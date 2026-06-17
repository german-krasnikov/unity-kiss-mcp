# Gemini Setup

Since v0.34.0. Spawns `gemini -p` per turn with stream-json output. MCP config auto-written to `~/.gemini/settings.json` (injected into the existing file without clobbering other settings). Works on **macOS and Linux**.

## Prerequisites

- Gemini CLI installed and authenticated
- Unity project with the `unity-mcp` plugin installed
- Python 3.10+ with `server/` dependencies installed (see main install guide)

## 1. Install Gemini CLI

### macOS (Homebrew — recommended)

```bash
brew install gemini-cli
gemini --version
```

### All Platforms (npm)

```bash
npm install -g @google/gemini-cli
gemini --version
```

## 2. Authenticate

```bash
gemini
```

On first launch, Gemini opens a browser for Google OAuth. After authentication, credentials are stored in `~/.gemini/oauth_creds.json`.

## 3. Use From the Editor (Primary Workflow)

1. Open Unity and wait for `[MCP] Server started on port XXXX` in the Console.
2. Open `Window > MCP Chat`.
3. Select **Gemini** from the backend dropdown.
4. Choose a model (optional — defaults to the Gemini CLI default).
5. Type a prompt and press Send.

## 4. How the Plugin Wires MCP

Before each turn, the plugin writes the `unity-mcp` entry into `~/.gemini/settings.json`:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "<python>",
      "args": ["-m", "unity_mcp.server"],
      "env": { "UNITY_MCP_PORT": "<port>" },
      "trust": true
    }
  }
}
```

If the file already exists, the plugin merges the `unity-mcp` entry into the existing `mcpServers` block — it does **not** overwrite other settings. The port is always kept up to date.

**Command shape:**

```bash
gemini -p "<prompt>" --output-format stream-json [--model <id>] [--yolo] [--sandbox]
```

`--yolo` maps to the `yolo` approval mode. `--sandbox` is passed when sandbox mode is enabled in the Chat settings.

## 5. Verify Connectivity (Manual CLI Test)

With Unity open:

```bash
gemini -p "Call the mcp tool get_hierarchy and return the result" --output-format stream-json
```

Expected: NDJSON stream. Look for a `tool_use` line with `"tool_name": "mcp_unity-mcp_get_hierarchy"` followed by a `tool_result` line, then a `message` line with `"role": "assistant"`.

## 6. Available Models

The model dropdown passes the value directly to `--model`. Leave it empty to use the Gemini CLI default (currently `gemini-2.5-pro`). Any model ID accepted by `gemini --model` works.

## 7. Common Problems

| Problem | Fix |
|---------|-----|
| `gemini: command not found` | Check `which gemini`; if missing, re-run the installer. On macOS, `brew install gemini-cli`. |
| MCP tools not available in session | Verify `~/.gemini/settings.json` contains the `unity-mcp` entry (the plugin writes it before each turn). Check that Unity is running and the port matches `UNITY_MCP_PORT`. |
| `ModuleNotFoundError: unity_mcp` | `server/.venv` is missing or stale. Recreate: `cd server && python3 -m venv .venv && .venv/bin/python -m pip install -e ".[dev]"` |
| Binary not found in Unity but works in terminal | Unity doesn't source `~/.zshrc`/shell profile. Set the full path in **Settings > Agent Chat > Gemini Binary Path**. |
| Settings file clobbered other MCP servers | The plugin uses merge logic — it only touches the `unity-mcp` key. If other entries disappeared, they were likely in a `mcpServers` block with malformed JSON. |
