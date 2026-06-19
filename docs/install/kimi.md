# Kimi Setup

Since v0.34.0. The in-Unity Chat window supports Kimi K2 as a backend. The plugin spawns `kimi -p` per turn with stream-json output, auto-configuring MCP at `~/.kimi-code/mcp.json` and model presets at `~/.kimi-code/config.toml`. Works on **macOS and Linux**.

## Prerequisites

- Kimi CLI installed and authenticated
- Unity 6000.0+ with the `unity-mcp` plugin installed (via UPM git URL)
- TCP port 9500 (or assigned by wizard) free

## 1. Quick Setup

Bootstrap script handles everything:

```bash
curl -fsSL https://raw.githubusercontent.com/german-krasnikov/unity-kiss-mcp/master/install/bootstrap.sh | bash
```

Then open Unity and follow the Setup Wizard.

## 2. Manual Setup

### Install Kimi CLI

```bash
curl -fsSL https://kimi.ai/install.sh | bash
kimi --version
```

The installer adds `~/.kimi-code/bin` to PATH via `~/.zshrc` (macOS) or `~/.bashrc` (Linux). **Restart your terminal** before continuing.

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

### Authenticate Kimi

```bash
kimi login
```

Opens a browser for OAuth authorization. Credentials are stored in `~/.kimi-code/credentials/`.

### Configure Kimi (Automatic)

Open Unity, then open the **Setup Wizard** via **MCP → Setup Wizard** menu. Select **Kimi** and follow the prompts.

**Manual configuration:**

```bash
python install.py configure --tool kimi
```

## 3. Use From the Editor (Primary Workflow)

1. Open Unity and wait for `[MCP] Server started on port <XXXX>` in the Console.
2. Open **Window → MCP Chat**.
3. Select **Kimi** from the backend dropdown.
4. Choose a model from the dropdown (K2.7 Code is the default).
5. Type a prompt and press Send.

## 4. How the Plugin Wires MCP

Before each turn, the plugin writes `~/.kimi-code/mcp.json`:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "<python>",
      "args": ["-m", "unity_mcp.server"],
      "env": { "UNITY_MCP_PORT": "<port>" }
    }
  }
}
```

Kimi reads this file automatically on startup.

The plugin appends to `~/.kimi-code/config.toml` (append-only, skips existing entries):

| Model | Model ID | Context |
|-------|----------|---------|
| K2.7 Code | `kimi-for-coding` | 262K tokens |
| K2.6 | `k2p6` | 262K tokens |
| K2.5 | `k2p5` | 262K tokens |

The plugin spawns per-turn:

```bash
kimi -p "<prompt>" --output-format stream-json [--model <id>]
```

## 5. Verify Installation

```bash
python install.py doctor
```

Or check manually:

```bash
# Verify kimi is in PATH
which kimi
kimi --version

# Check MCP config
cat ~/.kimi-code/mcp.json

# Test connectivity (with Unity running)
kimi -p "Call the mcp tool get_hierarchy and return the result" --output-format stream-json
```

## 6. Verify Connectivity (Manual CLI Test)

Expected: NDJSON stream with `role:assistant` content, followed by `role:meta` line.

## 7. Troubleshooting

| Problem | Fix |
|---------|-----|
| `kimi: command not found` | Restart terminal; verify `~/.kimi-code/bin` is in PATH (`echo $PATH`) |
| Setup Wizard doesn't run in Unity | (1) Check plugin is in Package Manager. (2) Close/reopen Unity. (3) Check Console for errors. |
| `ModuleNotFoundError: unity_mcp` | Run `python install.py setup` or `git clone` and setup manually. |
| `Model "X" is not configured` | Plugin auto-provisions K2.7 Code, K2.6, K2.5. For custom models, add `[models."X"]` to `~/.kimi-code/config.toml`. |
| Chat connects then immediately disconnects | Check `~/.kimi-code/logs/kimi-code.log` for errors. Verify `~/.kimi-code/mcp.json` exists and is valid JSON. |
| Binary not found in Chat Settings but works in terminal | Terminal sources `~/.zshrc` but Unity doesn't. Override manually: **Settings > Agent Chat > Kimi Binary Path** — enter absolute path. |
| Tools fail with "Connection refused" | Ensure Unity is open and TCP port is listening. Check `python install.py doctor`. |
