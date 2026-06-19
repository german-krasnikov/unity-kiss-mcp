# Gemini Setup

Since v0.30.1. The in-Unity Chat window supports Gemini as a backend. The plugin spawns `gemini -p` per turn with stream-json output, auto-writing MCP config to `~/.gemini/settings.json` without clobbering other settings. Works on **macOS and Linux**.

## Prerequisites

- Gemini CLI installed and authenticated
- Unity 6000.0+ with the `unity-mcp` plugin installed (via UPM git URL)
- TCP port 9500 (or assigned by wizard) free

## 1. Quick Setup

Bootstrap script handles everything:

```bash
curl -fsSL https://raw.githubusercontent.com/german-krasnikov/unity-kiss-mcp/master/install/bootstrap.sh | bash
```

Then open Unity and follow the Setup Wizard.

## 2. Manual Setup

### Install Gemini CLI

**macOS (Homebrew — recommended):**

```bash
brew install gemini-cli
gemini --version
```

**All platforms (npm):**

```bash
npm install -g @google/gemini-cli
gemini --version
```

### Install Python Server

```bash
git clone https://github.com/german-krasnikov/unity-kiss-mcp.git
cd unity-kiss-mcp
python install.py setup
```

### Add Plugin to Unity

1. Open Package Manager (Window → Package Manager)
2. Click `+` → **Add package from git URL**
3. Paste: `https://github.com/german-krasnikov/unity-kiss-mcp.git?path=unity-plugin`

### Authenticate Gemini

```bash
gemini
```

Gemini opens a browser for Google OAuth. Credentials are stored in `~/.gemini/oauth_creds.json`.

### Configure Gemini (Automatic)

Open Unity, then open the **Setup Wizard** via **MCP → Setup Wizard** menu. Select **Gemini** and follow the prompts.

**Manual configuration:**

```bash
python install.py configure --tool gemini
```

## 3. Use From the Editor (Primary Workflow)

1. Open Unity and wait for `[MCP] Server started on port <XXXX>` in the Console.
2. Open **Window → MCP Chat**.
3. Select **Gemini** from the backend dropdown.
4. Optionally choose a model from the dropdown (defaults to Gemini CLI default).
5. Type a prompt and press Send.

## 4. How the Plugin Wires MCP

Before each turn, the plugin merges the `unity-mcp` entry into `~/.gemini/settings.json`:

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

If the file already exists, only the `unity-mcp` entry is modified — other MCP servers are preserved.

The plugin spawns:

```bash
gemini -p "<prompt>" --output-format stream-json [--model <id>] [--yolo] [--sandbox]
```

## 5. Verify Installation

```bash
python install.py doctor
```

Or check manually:

```bash
gemini --version
cat ~/.gemini/settings.json  # should contain unity-mcp entry

# Test (with Unity running)
gemini -p "Call the mcp tool get_hierarchy and return the result" --output-format stream-json
```

## 6. Verify Connectivity (Manual CLI Test)

Expected: NDJSON stream with `tool_use` line containing `"tool_name": "mcp_unity-mcp_get_hierarchy"`.

## 7. Troubleshooting

| Problem | Fix |
|---------|-----|
| `gemini: command not found` | Check `which gemini`. On macOS, run `brew install gemini-cli`. Restart terminal after install. |
| Setup Wizard doesn't open | (1) Verify plugin is in Package Manager. (2) Close/reopen Unity. (3) Check Console for errors. |
| MCP tools not available in Chat | Verify `~/.gemini/settings.json` contains `unity-mcp` entry. Check Unity Console shows `[MCP] Server started on port <XXXX>`. Restart Chat session. |
| `ModuleNotFoundError: unity_mcp` | Run `python install.py setup` or clone and setup manually. |
| Binary not found in Chat Settings but works in terminal | Terminal sources `~/.zshrc` but Unity doesn't. Override: **Settings > Agent Chat > Gemini Binary Path** — enter absolute path. |
| Settings file broke other MCP servers | The plugin merges intelligently — it only modifies the `unity-mcp` entry. If other servers disappeared, they likely had malformed JSON. Restore from backup if needed. |
