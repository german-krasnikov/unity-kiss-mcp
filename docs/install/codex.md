# Codex Setup

Since v0.14.0 the primary Codex workflow is the in-Unity Chat window. The plugin spawns `codex exec` internally and injects MCP config via `-c` flags — no `.codex/config.toml` needed.

## Prerequisites

- Codex CLI installed and authenticated
- Unity project with the `unity-mcp` plugin installed
- `server/` directory with Python dependencies installed (see main install guide)

## 1. Install Codex CLI

```bash
npm install -g @openai/codex
# or
brew install openai/tap/codex

codex --version
```

## 2. Authenticate

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

The plugin resolves the Python command, builds the `codex exec` argv, and spawns the process. No config file required.

## 4. How the Plugin Wires MCP

The plugin calls `codex exec` with three `-c` flags on every invocation (including resume turns):

```
-c mcp_servers.unity.command="<python>"
-c mcp_servers.unity.args=["-m","unity_mcp.server"]
-c mcp_servers.unity.startup_timeout_sec=30
```

The MCP server key is `unity` (not `unity-mcp`).

### Python Resolution Order

`ChatMcpConfigWriter.ResolvePythonCommand` picks the interpreter automatically:

1. `server/.venv/bin/python` — if the venv exists
2. `python3` — fallback

> `uv` resolution exists in `ResolvePythonCommand` but `CodexBackend` passes `null` for the uv path, so it is Claude-backend-only. If you have no venv: `cd server && pip install -e ".[dev]"`

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

With Unity open and running on port 9500:

```bash
VENV_PYTHON="/path/to/unity-kiss-mcp/server/.venv/bin/python"
codex exec --json -C /path/to/unity-kiss-mcp -s danger-full-access --skip-git-repo-check \
  -c "mcp_servers.unity.command=\"$VENV_PYTHON\"" \
  -c 'mcp_servers.unity.args=["-m","unity_mcp.server"]' \
  -c 'mcp_servers.unity.startup_timeout_sec=30' \
  "Call get_hierarchy and return the result." </dev/null
```

Expected: output contains `mcp_tool_call` with `"server":"unity","tool":"get_hierarchy"`.

> Note: redirecting stdin (`</dev/null`) is required — without it `codex exec` blocks waiting for input even when a positional prompt is given.

## 6. Common Problems

| Problem | What to Check |
|---------|---------------|
| `unknown MCP server 'unity'` | The `-c` flags were omitted — they must be passed on every turn including resume |
| `ModuleNotFoundError: unity_mcp` | `server/.venv` does not exist; run `cd server && pip install -e ".[dev]"` |
| Unity tools do not respond | Confirm Unity is open and Console shows `[MCP] Server started on port 9500` |
| `codex exec` blocks at startup | Redirect stdin: append `</dev/null` to the command |
| `codex exec resume` fails with flag error | Resume does not accept `-C` or `-s`; use `--dangerously-bypass-approvals-and-sandbox` |

## Appendix: Manual config.toml (terminal-only)

The repo ships `.codex/config.toml` as a convenience for testing Codex from a terminal without the Unity editor. It uses `command = "python"` (not the venv path) and key `unity-mcp` (not `unity`). **The plugin does not read this file** — it injects config via `-c` flags at runtime.

```toml
# For manual terminal testing only. The in-editor workflow ignores this file.
[mcp_servers.unity-mcp]
command = "python"
args = ["-m", "unity_mcp.server"]
cwd = "/path/to/unity-kiss-mcp/server"
startup_timeout_sec = 10
tool_timeout_sec = 120
enabled = true
```
