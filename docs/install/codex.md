# Codex Setup

Since v0.14.0 the primary Codex workflow is the in-Unity Chat window. The plugin spawns `codex exec` internally and injects MCP config via `-c` flags — no `.codex/config.toml` needed. Works on **Windows, macOS, and Linux**.

## Prerequisites

- Codex CLI installed and authenticated
- Unity project with the `unity-mcp` plugin installed
- Python 3.10+ with `server/` dependencies installed (see main install guide)
- Per-platform requirements (see section 1)

## 1. Install Codex CLI

### All Platforms

```bash
npm install -g @openai/codex
codex --version
```

**macOS only** (alternative):
```bash
brew install openai/tap/codex
```

### Platform-Specific: Python Venv Setup

After installing Codex, ensure the `server/` directory has a platform-appropriate venv.

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

> **Important:** If you copied `.venv` from macOS/Linux to Windows, it contains Unix paths (`bin/python`) and **will not run**. Delete it and recreate:
> ```powershell
> Remove-Item -Recurse .\.venv
> python -m venv .venv
> ```

## 2. Authenticate Codex

```bash
# Option A — browser / account
codex login
codex login status

# Option B — API key
printenv OPENAI_API_KEY | codex login --with-api-key
codex login status
```

## 3. Use Codex From the Editor (Primary Workflow)

1. Open Unity and wait for `[MCP] Server started on port 9500` in the Console.
2. Open `Window > MCP Chat`.
3. Select **Codex** from the backend dropdown.
4. Type a prompt and press Enter.

The plugin resolves the Python command, builds the `codex exec` argv, and spawns the process.

## 4. How the Plugin Wires MCP

The plugin calls `codex exec` with three `-c` flags on every invocation (including resume turns):

```
-c mcp_servers.unity.command="<python>"
-c mcp_servers.unity.args=["-m","unity_mcp.server"]
-c mcp_servers.unity.startup_timeout_sec=30
```

The MCP server key is `unity` (not `unity-mcp`).

### Python Resolution Order

`ChatMcpConfigWriter.ResolvePythonCommand` picks the interpreter in this order:

1. **Windows:** `<project_root>/server/.venv/Scripts/python.exe` (if exists)
2. **macOS / Linux:** `<project_root>/server/.venv/bin/python` (if exists)
3. `uv` binary (if found in PATH; for Claude backend only, Codex passes `null`)
4. **Windows fallback:** `python`
5. **macOS / Linux fallback:** `python3`

### Two Command Shapes

**First turn:**

```bash
codex exec --json -C <project_root> -s danger-full-access --skip-git-repo-check \
  -c 'mcp_servers.unity.command="<python>"' \
  -c 'mcp_servers.unity.args=["-m","unity_mcp.server"]' \
  -c 'mcp_servers.unity.startup_timeout_sec=30' \
  "<prompt>"
```

**Resume turn** (`-C` and `-s` are not accepted; use `--dangerously-bypass-approvals-and-sandbox`):

```bash
codex exec resume <thread_id> --json --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check \
  -c 'mcp_servers.unity.command="<python>"' \
  -c 'mcp_servers.unity.args=["-m","unity_mcp.server"]' \
  -c 'mcp_servers.unity.startup_timeout_sec=30' \
  "<prompt>"
```

## 5. Verify Connectivity (Manual CLI Test)

### macOS / Linux (bash)

With Unity open and running on port 9500:

```bash
VENV_PYTHON="<project_root>/server/.venv/bin/python"
codex exec --json -C <project_root> -s danger-full-access --skip-git-repo-check \
  -c "mcp_servers.unity.command=\"$VENV_PYTHON\"" \
  -c 'mcp_servers.unity.args=["-m","unity_mcp.server"]' \
  -c 'mcp_servers.unity.startup_timeout_sec=30' \
  "Call get_hierarchy and return the result." </dev/null
```

### Windows (PowerShell)

```powershell
$venvPython = "<project_root>\server\.venv\Scripts\python.exe"
$projectRoot = "<project_root>"

$codexArgs = @(
  'exec', '--json',
  '-C', $projectRoot,
  '-s', 'danger-full-access',
  '--skip-git-repo-check',
  '-c', "mcp_servers.unity.command='$venvPython'",
  '-c', "mcp_servers.unity.args=['-m','unity_mcp.server']",
  '-c', 'mcp_servers.unity.startup_timeout_sec=30',
  'Call get_hierarchy and return the result.'
)

& codex @codexArgs
```

> **Note:** On macOS/Linux, stdin redirect (`</dev/null`) is required — without it `codex exec` blocks waiting for input. On Windows, pass the prompt as a positional argument (shown above).

Expected: output contains `mcp_tool_call` with `"server":"unity","tool":"get_hierarchy"`.

## 6. Common Problems

| Problem | What to Check |
|---------|---------------|
| `codex: command not found` (macOS/Linux) or `codex: The term 'codex' is not recognized` (Windows) | Codex binary not in PATH; ensure `npm install -g @openai/codex` completed successfully. Check `which codex` (bash) or `where.exe codex` (PowerShell) |
| `unknown MCP server 'unity'` | The `-c` flags were omitted — they must be passed on every turn including resume |
| `ModuleNotFoundError: unity_mcp` | `server/.venv` does not exist or is stale. Delete and recreate per section 1 (platform-specific) |
| Unity tools do not respond | Confirm Unity is open and Console shows `[MCP] Server started on port 9500`. Check `lsof -i :9500` (macOS/Linux) or `Test-NetConnection -ComputerName localhost -Port 9500` (Windows) |
| `codex exec` blocks at startup (macOS/Linux) | Redirect stdin: append `</dev/null` to the command |
| Binary path resolution fails in Settings | Override via **Settings > Agent Chat > Codex Binary Path**. Enter absolute path to `codex` binary; this sets EditorPref `UnityMCP_Chat_Path_codex` |

## Appendix: Manual config.toml (terminal-only)

The repo ships `.codex/config.toml` as a convenience for testing Codex from a terminal without the Unity editor. It uses `command = "python"` (not the venv path) and key `unity-mcp` (not `unity`). **The plugin does not read this file** — it injects config via `-c` flags at runtime.

```toml
# For manual terminal testing only. The in-editor workflow ignores this file.
[mcp_servers.unity-mcp]
command = "python"
args = ["-m", "unity_mcp.server"]
cwd = "<project_root>/server"
startup_timeout_sec = 10
tool_timeout_sec = 120
enabled = true
```
