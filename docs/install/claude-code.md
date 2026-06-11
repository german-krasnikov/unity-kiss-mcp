# Claude Code Setup

Since v0.14.0 the in-Unity Chat window supports the Claude Code CLI as the primary backend. The plugin generates a temporary `--mcp-config` JSON file and invokes `claude code` with `--strict-mcp-config` to use only Unity's MCP tools. Works on **Windows, macOS, and Linux**.

## Prerequisites

- Claude Code CLI installed and authenticated
- Unity project with the `unity-mcp` plugin installed
- Python 3.10+ with `server/` dependencies installed (see main install guide)
- Per-platform requirements (see section 1)

## 1. Install Claude Code CLI

### All Platforms

**Official installer (recommended):**
Visit https://claude.com/download and install the native Claude Code app for your OS.

**Or via npm:**
```bash
npm install -g @anthropic-ai/claude-code
claude --version
```

### Platform-Specific: Python Venv Setup

After installing Claude Code, ensure the `server/` directory has a platform-appropriate venv.

**macOS / Linux:**
```bash
cd <project_root>/server
python3 -m venv .venv
.venv/bin/python -m pip install -e ".[dev]"
```

**Windows (PowerShell):**
```powershell
cd <project_root>\server
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -e ".[dev]"
```

> **Important:** If you copied `.venv` from macOS/Linux to Windows, it contains Unix paths (`bin/python`) and **will not run**. Delete and recreate:
> ```powershell
> Remove-Item -Recurse .\.venv
> python -m venv .venv
> ```

## 2. Authenticate Claude Code

```bash
claude auth login
claude auth status
```

## 3. Use Claude Code From the Editor (Primary Workflow)

1. Open Unity and wait for `[MCP] Server started on port 9500` in the Console.
2. Open `Window > MCP Chat`.
3. Select **Claude** from the backend dropdown.
4. Type a prompt and press Enter.

The plugin generates a temporary MCP config file, passes it via `--mcp-config`, and spawns `claude code` with `--strict-mcp-config` to isolate the session to Unity tools only.

## 4. How the Plugin Wires MCP

The plugin generates a temporary JSON config and calls Claude Code like this:

```bash
claude code --mcp-config <tempfile.json> --strict-mcp-config
```

The generated JSON contains:

```json
{
  "mcpServers": {
    "unity": {
      "command": "<python>",
      "args": ["-m", "unity_mcp.server"]
    }
  }
}
```

The `--strict-mcp-config` flag ensures Claude Code uses **only** this temporary config, ignoring the user's global `~/.claude/mcp.json`. This isolation guarantees the session can only access Unity tools, not other unrelated MCP servers.

### Python Resolution Order

`ChatMcpConfigWriter.ResolvePythonCommand` picks the interpreter in this order:

1. **Windows:** `<project_root>/server/.venv/Scripts/python.exe` (if exists)
2. **macOS / Linux:** `<project_root>/server/.venv/bin/python` (if exists)
3. `uv` binary (if found in PATH)
4. **Windows fallback:** `python`
5. **macOS / Linux fallback:** `python3`

## 5. Verify Connectivity (Manual CLI Test)

### macOS / Linux (bash)

With Unity open and running on port 9500:

```bash
VENV_PYTHON="<project_root>/server/.venv/bin/python"
CONFIG_FILE="/tmp/unity-mcp-test.json"

cat > "$CONFIG_FILE" <<EOF
{
  "mcpServers": {
    "unity": {
      "command": "$VENV_PYTHON",
      "args": ["-m", "unity_mcp.server"]
    }
  }
}
EOF

echo "Call get_hierarchy with depth=1 and return the result." | \
  claude code --mcp-config "$CONFIG_FILE" --strict-mcp-config
```

### Windows (PowerShell)

```powershell
$venvPython = "<project_root>\server\.venv\Scripts\python.exe"
$configFile = "$env:TEMP\unity-mcp-test.json"

$config = @{
    mcpServers = @{
        unity = @{
            command = $venvPython
            args = @("-m", "unity_mcp.server")
        }
    }
} | ConvertTo-Json

Set-Content -Path $configFile -Value $config

$prompt = "Call get_hierarchy with depth=1 and return the result."

$prompt | & claude code --mcp-config $configFile --strict-mcp-config
```

Expected: Claude responds using the `get_hierarchy` tool and returns the result.

## 6. Common Problems

| Problem | What to Check |
|---------|---------------|
| `claude: command not found` (macOS/Linux) or `claude: The term 'claude' is not recognized` (Windows) | Claude Code binary not in PATH; ensure `npm install -g @anthropic-ai/claude-code` completed or the native installer ran successfully. Check `which claude` (bash) or `where.exe claude` (PowerShell) |
| `ModuleNotFoundError: unity_mcp` | `server/.venv` does not exist or is stale. Delete and recreate per section 1 (platform-specific) |
| Unity tools do not appear in Claude Code session | (1) Verify `--strict-mcp-config` is passed (shown in `claude --version` debug output). (2) Confirm Unity is open and Console shows `[MCP] Server started on port 9500`. (3) Check `lsof -i :9500` (macOS/Linux) or `Test-NetConnection -ComputerName localhost -Port 9500` (Windows). (4) In Settings, open Agent Chat foldout and check the generated config path |
| Binary path resolution fails in Settings | Override via **Settings > Agent Chat > Claude Binary Path**. Enter absolute path to `claude` binary; this sets EditorPref `UnityMCP_Chat_ClaudePath` |
| `pydantic_core` library load error (macOS quarantine) | Remove macOS quarantine attributes: `xattr -dr com.apple.quarantine <project_root>/server/.venv/lib` |

## Appendix: Claude vs Codex MCP Wiring

| Aspect | Claude Code | Codex |
|--------|------------|-------|
| **Config delivery** | `--mcp-config <tempfile.json>` (JSON) | `-c` flags (TOML inline) |
| **Isolation flag** | `--strict-mcp-config` (ignore global config) | N/A (Codex session-scoped) |
| **Server key** | `unity` | `unity` |
| **Process model** | Spawn per turn (stateless) | Persistent `codex app-server` (JSON-RPC, stateful resume) |
| **Resume mechanism** | N/A (fresh start each turn) | `thread/start` on first turn, `turn/start` on resume |
| **Config permanence** | Temporary file (auto-cleanup) | Passed at spawn time; no persistence needed |
