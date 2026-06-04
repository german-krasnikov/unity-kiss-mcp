# Codex MCP Setup

This guide shows how to connect Codex CLI to `unity-mcp` in this repository.
The goal is that, after setup, Codex can see the Unity MCP server and call tools such as `get_hierarchy` and `get_console`.

## What You Need

- macOS, Linux, or Windows with a working `Codex CLI`
- Python 3.10+
- A Unity project with `unity-plugin` installed
- The server Python environment in `server/.venv`
- Unity Editor open so the plugin can expose TCP port `9500`

## 1. Verify That Unity MCP Already Works

Start by confirming that the server and Unity plugin are installed correctly.

1. Open the Unity project from `unity-test-project/`.
2. Wait for this message in the Console:

```text
[MCP] Server started on port 9500
```

3. Make sure the server Python dependencies are installed:

```bash
cd /path/to/unity-kiss-mcp/server
pip install -e ".[dev]"
```

If you already have a virtual environment in `server/.venv`, use that interpreter directly.

## 2. Install Codex CLI

If Codex CLI is not installed yet, install it with one of these commands:

```bash
npm install -g @openai/codex
```

or:

```bash
brew install openai/tap/codex
```

Verify the installation:

```bash
codex --version
```

For JSONL debugging, it is also useful to check:

```bash
codex exec --help | grep -- --json
```

If `--json` is not available, use the option supported by your Codex CLI version.

## 3. Sign In to Codex

You need to authenticate with either a ChatGPT account or an API key.

### Option A. Sign In With an Account

```bash
codex login
codex login status
```

### Option B. Sign In With an API Key

```bash
printenv OPENAI_API_KEY | codex login --with-api-key
codex login status
```

If authentication is stored in the system keychain instead of a file, that is fine. The important part is that `codex login status` shows you are signed in.

## 4. Configure the MCP Server for Codex

This repository uses a project-local Codex config:

- [`.codex/config.toml`](../../.codex/config.toml)

If the file does not exist yet, create it in the repository root. If it already exists, verify that it defines the `unity-mcp` server.

### Recommended Setup

It is better to point `command` at the server virtual environment explicitly so Codex uses the same interpreter that has `unity_mcp` installed.

```toml
[mcp_servers.unity-mcp]
command = "/path/to/unity-kiss-mcp/server/.venv/bin/python"
args = ["-m", "unity_mcp.server"]
cwd = "/path/to/unity-kiss-mcp/server"
startup_timeout_sec = 10
tool_timeout_sec = 120
enabled = true
```

### If Your `python` on PATH Is Already Correct

You can use the shorter version:

```toml
[mcp_servers.unity-mcp]
command = "python"
args = ["-m", "unity_mcp.server"]
cwd = "/path/to/unity-kiss-mcp/server"
startup_timeout_sec = 10
tool_timeout_sec = 120
enabled = true
```

### What Matters in the Config

- `command` must point to a Python interpreter that can import `unity_mcp`
- `args = ["-m", "unity_mcp.server"]` starts the MCP server without a wrapper script
- `cwd` must be the `server/` directory, not the repository root
- `startup_timeout_sec` helps if Unity or the Python environment starts slowly
- `tool_timeout_sec` protects against hung Unity calls

## 5. Restart Codex

After changing `.codex/config.toml`, fully restart Codex CLI.

If you were already in an open session, do not expect the config to be reloaded automatically. Close Codex and start it again.

## 6. Verify That Codex Sees the MCP Server

Run a simple one-shot command and ask Codex to talk to Unity MCP.

```bash
codex exec --json --skip-git-repo-check \
  "Use the Unity MCP tools. Call get_hierarchy and return the result."
```

Expected behavior:

- Codex launches `unity-mcp` from `server/`
- Unity Console shows no connection errors
- The response includes the result of `get_hierarchy`

If your Codex version only supports the non-JSON mode, run the same request without `--json`.

## 7. Quick Manual Check

If you want a more explicit connectivity test, use any simple read-only tool:

- `get_hierarchy`
- `get_console`
- `get_compile_errors`

Example:

```bash
codex exec --skip-git-repo-check \
  "Call get_console and summarize the latest Unity console output."
```

If the server is set up correctly, Codex should discover the Unity MCP tools and return a result without any manual connection setup.

## 8. Typical Workflow

1. Unity is open and shows `[MCP] Server started on port 9500`
2. `server/.venv` exists and contains the `unity_mcp` dependencies
3. `.codex/config.toml` points to `server/.venv/bin/python`
4. Codex CLI is authenticated
5. You run `codex exec`
6. Codex calls Unity MCP tools through `unity-mcp`

## 9. Common Problems and Fixes

| Problem | What to Check |
|---|---|
| Codex cannot see the MCP server | Make sure `.codex/config.toml` is in the repository root and includes `[mcp_servers.unity-mcp]` |
| `ModuleNotFoundError: unity_mcp` | Point `command` to `server/.venv/bin/python` instead of the system Python |
| Codex starts, but Unity tools do not respond | Confirm that Unity Editor is open and the plugin started the TCP server on `9500` |
| `codex exec` hangs at startup | Increase `startup_timeout_sec` and make sure Unity is not compiling at that moment |
| Unity Console shows no MCP messages | Confirm that `unity-plugin` is installed in the project and not disabled in Package Manager |
| The config uses the wrong `cwd` | `cwd` must point to `/path/to/unity-kiss-mcp/server`, not the repository root |

## 10. Minimal Working Example

If you want the shortest possible config for a quick test, use this:

```toml
[mcp_servers.unity-mcp]
command = "/path/to/unity-kiss-mcp/server/.venv/bin/python"
args = ["-m", "unity_mcp.server"]
cwd = "/path/to/unity-kiss-mcp/server"
enabled = true
```

That is enough for Codex to start `unity-mcp` and reach the Unity Editor through MCP.
