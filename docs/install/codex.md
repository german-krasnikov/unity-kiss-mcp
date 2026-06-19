# Codex Setup

Since v0.14.0 the primary Codex workflow is in-Unity Chat. The plugin spawns `codex exec` and injects MCP config via `-c` flags — no static config file needed. Works on **Windows, macOS, and Linux**.

## Prerequisites

- Codex CLI installed and authenticated
- Unity 6000.0+ with the `unity-mcp` plugin installed (via UPM git URL)
- TCP port 9500 (or assigned by wizard) free

## 1. Quick Setup

Bootstrap script handles everything:

**macOS/Linux:**
```bash
curl -fsSL https://raw.githubusercontent.com/german-krasnikov/unity-kiss-mcp/master/install/bootstrap.sh | bash
```

**Windows PowerShell:**
```powershell
iex (iwr https://raw.githubusercontent.com/german-krasnikov/unity-kiss-mcp/master/install/bootstrap.ps1).Content
```

Then open Unity and follow the Setup Wizard.

## 2. Manual Setup

### Install Codex CLI

```bash
npm install -g @openai/codex
codex --version
```

**macOS alternative:**
```bash
brew install openai/tap/codex
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

### Authenticate Codex

```bash
# Option A — browser
codex login
codex login status

# Option B — API key
printenv OPENAI_API_KEY | codex login --with-api-key
codex login status
```

### Configure Codex (Automatic)

Open Unity, then open the **Setup Wizard** via **MCP → Setup Wizard** menu. Select **Codex** and follow the prompts.

**Manual configuration:**

```bash
python install.py configure --tool codex
```

## 3. Use Codex From the Editor (Primary Workflow)

1. Open Unity and wait for `[MCP] Server started on port <XXXX>` in the Console.
2. Open **Window → MCP Chat**.
3. Select **Codex** from the backend dropdown.
4. Type a prompt and press Enter.

The plugin resolves the Python command, injects MCP config via `-c` flags, and spawns `codex exec`.

## 4. How the Plugin Wires MCP

On each turn, the plugin resolves the Python command (in order):

1. **Windows:** `<project_root>/server/.venv/Scripts/python.exe` (if exists)
2. **macOS / Linux:** `<project_root>/server/.venv/bin/python` (if exists)
3. System `python3` or `python` fallback

Then spawns `codex exec` with `-c` flags:

```bash
codex exec -c mcp_servers.unity.command="<python>" \
           -c mcp_servers.unity.args=["-m","unity_mcp.server"] \
           -c mcp_servers.unity.startup_timeout_sec=30 \
           "<prompt>"
```

The server key is `unity` (not `unity-mcp`).

## 5. Verify Installation

```bash
python install.py doctor
```

Checks Python version, server package, config validity, and TCP connectivity.

## 6. Troubleshooting

| Problem | Fix |
|---------|-----|
| `codex: command not found` | Ensure `npm install -g @openai/codex` completed. Check `which codex` or `where.exe codex`. |
| `unknown MCP server 'unity'` | The `-c` flags are missing — they must be passed on every turn. Setup Wizard should have configured this. |
| `ModuleNotFoundError: unity_mcp` | Run `python install.py setup` or clone and setup manually. |
| Setup Wizard doesn't open in Unity | (1) Verify plugin is in Package Manager. (2) Close/reopen Unity. (3) Check Console for errors. |
| Tools don't respond in Chat | Confirm Unity is open and Console shows `[MCP] Server started on port <XXXX>`. Check `python install.py doctor`. |
| `codex exec` blocks at startup (macOS/Linux) | Redirect stdin: append `</dev/null`. The plugin handles this automatically. |
| Binary path resolution fails in Settings | Override manually: **Settings > Agent Chat > Codex Binary Path** — enter absolute path. |
