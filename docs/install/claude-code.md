# Claude Code Setup

Since v0.14.0 the in-Unity Chat window supports Claude Code as the primary backend. The plugin generates a temporary `--mcp-config` JSON file and invokes `claude` to isolate to only Unity's MCP tools. Works on **Windows, macOS, and Linux**.

## Prerequisites

- Claude Code CLI installed and authenticated
- Unity 6000.0+ with the `unity-mcp` plugin installed (via UPM git URL)
- TCP port 9500 (or assigned by wizard) free

## 1. Quick Setup

The bootstrap script handles everything:

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

### Install Claude Code CLI

**Official installer (recommended):**
Visit https://claude.com/download and install the native Claude Code app for your OS.

**Or via npm:**
```bash
npm install -g @anthropic-ai/claude-code
claude --version
```

### Install Python Server

The Python MCP server runs on-demand via `uvx` — no installation needed. The setup wizard will auto-discover it.

You can optionally verify it works:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-kiss-mcp.git#subdirectory=server unity-mcp --help
```

### Add Plugin to Unity

1. Open Package Manager (Window → Package Manager)
2. Click `+` → **Add package from git URL**
3. Paste: `https://github.com/german-krasnikov/unity-kiss-mcp.git?path=unity-plugin`
4. Wait for import, then open any scene

### Authenticate Claude Code

```bash
claude auth login
claude auth status
```

## 3. Configure Claude Code (Automatic)

Open Unity, then open the **Setup Wizard** via **MCP → Setup Wizard** menu. Select **Claude Code** and follow the prompts. It will:

1. Verify Python 3.10+ is available
2. Test MCP server connectivity
3. Write your Claude Code MCP config file
4. Show the TCP port number

**Manual configuration** (if wizard fails):

Edit your Claude Code `mcp_settings.json` file (usually `~/.config/claude/mcp_settings.json` on macOS/Linux or `%APPDATA%\Anthropic\Claude\mcp_settings.json` on Windows) and add:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "uvx",
      "args": ["--from", "git+https://github.com/german-krasnikov/unity-kiss-mcp.git#subdirectory=server", "unity-mcp"],
      "env": { "UNITY_MCP_PORT": "9500" }
    }
  }
}
```

Save the file, then restart Claude Code.

<details>
<summary><b>Alternative: Manual Installation (git clone)</b></summary>

```bash
git clone https://github.com/german-krasnikov/unity-kiss-mcp.git
cd unity-kiss-mcp
python install.py setup
```

This clones the repository locally, creates a Python venv, and installs dependencies. After setup, verify:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-kiss-mcp.git#subdirectory=server unity-mcp --help
```

Then configure your AI tool:

```bash
python install.py configure --tool claude-code
# Or project-scoped:
python install.py configure --tool claude-code --project-dir /path/to/unity/project
```

Verify installation:

```bash
python install.py doctor
```

</details>

## 4. Use Claude Code From the Editor (Primary Workflow)

1. Open Unity and wait for `[MCP] Server started on port <XXXX>` in the Console.
2. Open **MCP → Chat**.
3. Select **Claude** from the backend dropdown.
4. Type a prompt and press Enter.

The plugin generates a temporary MCP config on each turn, passes it via `--mcp-config`, and spawns `claude` with `--mcp-config` for isolation.

## 5. How the Plugin Wires MCP

For each turn, the plugin:

1. **Resolves the Python command** (in order):
   - `<project_root>/server/.venv/bin/python` (if exists on macOS/Linux)
   - `<project_root>/server/.venv/Scripts/python.exe` (if exists on Windows)
   - `uv` binary (if in PATH)
   - System `python3` or `python` fallback

2. **Generates a temporary JSON config:**
   ```json
   {
     "mcpServers": {
       "unity": {
         "command": "<python>",
         "args": ["-m", "unity_mcp.server"],
         "env": {"UNITY_MCP_PORT": "<port>"}
       }
     }
   }
   ```

3. **Spawns Claude Code:**
   ```bash
   claude --mcp-config <tempfile.json> "<prompt>"
   ```


## 6. Verify Installation

Open the **Setup Wizard** in Unity (**MCP → Setup Wizard**) and select **Diagnostics**. It will check:

- Python >= 3.10
- TCP port connectivity to Unity
- MCP server responsiveness
- Plugin installation

Alternatively, call the `doctor` tool from Claude Code:

```
@claude-code doctor
```

## 7. Troubleshooting

| Problem | Fix |
|---------|-----|
| `claude: command not found` | Ensure Claude Code is installed and in PATH. Check `which claude` or `where.exe claude`. |
| MCP server fails to start | Run Setup Wizard → Diagnostics. Check that Python 3.10+ is available and TCP port 9500 is free. |
| Setup Wizard doesn't open in Unity | (1) Verify plugin is in Package Manager. (2) Close and reopen Unity. (3) Check Console for errors. |
| MCP tools don't appear in Claude Code | (1) Confirm Setup Wizard configured Claude Code. (2) Restart Claude Code. (3) Check Console for MCP connection errors. |
| Tools fail with "Connection refused" | (1) Ensure Unity is open with the plugin. (2) Run Setup Wizard → Diagnostics to check TCP port. (3) Restart Unity. |
| Python path resolution fails in Chat Settings | Override manually: **Settings > Agent Chat > Claude Binary Path** — enter absolute path to `claude` binary. |

