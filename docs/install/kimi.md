# Kimi Setup

Since v0.34.0. Spawns `kimi -p` per turn with stream-json output. MCP config auto-written to `~/.kimi-code/mcp.json`. Model presets auto-provisioned in `~/.kimi-code/config.toml`. Works on **macOS and Linux**.

## Prerequisites

- Kimi CLI installed and authenticated
- Unity project with the `unity-mcp` plugin installed
- Python 3.10+ with `server/` dependencies installed (see main install guide)

## 1. Install Kimi CLI

```bash
curl -fsSL https://kimi.ai/install.sh | bash
kimi --version
```

The installer adds `~/.kimi-code/bin` to PATH via `~/.zshrc` (macOS) or `~/.bashrc` (Linux). **Restart your terminal** (or run `source ~/.zshrc`) before continuing.

## 2. Authenticate

```bash
kimi login
```

Opens a browser for OAuth authorization. Credentials are stored in `~/.kimi-code/credentials/`.

## 3. Use From the Editor (Primary Workflow)

1. Open Unity and wait for `[MCP] Server started on port XXXX` in the Console.
2. Open `Window > MCP Chat`.
3. Select **Kimi** from the backend dropdown.
4. Choose a model (K2.7 Code is the default).
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

Kimi reads this file automatically — no CLI flag needed.

The plugin also appends to `~/.kimi-code/config.toml` (append-only, skips existing entries):

| Preset | Model ID | Context |
|--------|----------|---------|
| K2.7 Code | `kimi-for-coding` | 262 144 tokens |
| K2.6 | `k2p6` | 262 144 tokens |
| K2.5 | `k2p5` | 262 144 tokens |

**Command shape:**

```bash
kimi -p "<prompt>" --output-format stream-json [--model <id>]
```

Note: `--yolo` and `--plan` are incompatible with `-p` mode — approval modes are not supported.

## 5. Verify Connectivity (Manual CLI Test)

With Unity open:

```bash
kimi -p "Call the mcp tool get_hierarchy and return the result" --output-format stream-json
```

Expected: NDJSON stream with `role:assistant` content, followed by a `role:meta` line.

## 6. Available Models

| Display Name | Model ID | Notes |
|--------------|----------|-------|
| Default | (empty) | Uses `config.toml` default |
| K2.7 Code | `kimi-for-coding` | Latest coding model |
| K2.6 | `k2p6` | Previous version |
| K2.5 | `k2p5` | Earlier version |
| Custom… | any | Add `[models."X"]` to `config.toml` manually |

## 7. Common Problems

| Problem | Fix |
|---------|-----|
| `kimi: command not found` | Restart terminal; verify `~/.kimi-code/bin` is in PATH (`echo $PATH`) |
| `Model "X" is not configured` | Plugin auto-provisions the three known models. For custom models, add a `[models."X"]` section to `~/.kimi-code/config.toml` |
| `Cannot combine --prompt with --yolo` | Expected — `--yolo`/`--plan` are unsupported in `-p` mode |
| Chat connects then immediately disconnects | Check `~/.kimi-code/logs/kimi-code.log` |
| Binary not found in Unity but works in terminal | Unity doesn't source `~/.zshrc`. Set the path in **Settings > Agent Chat > Kimi Binary Path** |
