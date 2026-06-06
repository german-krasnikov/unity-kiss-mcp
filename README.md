# Unity MCP — Control Unity Editor from Claude Code

<div align="center">

<img src="docs/assets/hero.svg" width="100%" alt="Unity MCP — control Unity from Claude, a live heartbeat status window with a breathing mint-green orb, ECG trace, and TCP packet field">

<a href="https://github.com/german-krasnikov/unity-kiss-mcp">
<img src="https://readme-typing-svg.demolab.com?font=Fira+Code&weight=600&size=22&pause=900&color=3AD29F&center=true&vCenter=true&width=760&lines=The+editor's+heartbeat%2C+made+visible.;Control+Unity+from+Claude.;80-95%25+batch+token+savings.;Scene+CRUD+%C2%B7+Animation+%C2%B7+VFX+%C2%B7+PlayTest+DSL." alt="Control Unity from Claude — token-minimized MCP tools — 80-95% batch token savings">
</a>

</div>

<!-- ───────────────────────────  BADGE WALL  ─────────────────────────── -->

<div align="center">

<sub>**STATUS**</sub><br>
<img src="https://img.shields.io/github/actions/workflow/status/german-krasnikov/unity-kiss-mcp/update-readme.yml?style=for-the-badge&labelColor=1a1a2e&color=3ad29f&logo=githubactions&logoColor=white&label=build" alt="Build">
<img src="https://img.shields.io/github/license/german-krasnikov/unity-kiss-mcp?style=for-the-badge&labelColor=1a1a2e&color=3ad29f&logo=opensourceinitiative&logoColor=white" alt="License">
<img src="https://img.shields.io/github/stars/german-krasnikov/unity-kiss-mcp?style=for-the-badge&labelColor=1a1a2e&color=3ad29f&logo=github&logoColor=white" alt="Stars">
<img src="https://img.shields.io/github/last-commit/german-krasnikov/unity-kiss-mcp?style=for-the-badge&labelColor=1a1a2e&color=3ad29f&logo=git&logoColor=white" alt="Last Commit">

<sub>**SPEC**</sub><br>
<img src="https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/german-krasnikov/unity-kiss-mcp/master/.github/badges/tests.json&style=for-the-badge&labelColor=1a1a2e" alt="Tests">
<img src="https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/german-krasnikov/unity-kiss-mcp/master/.github/badges/tools.json&style=for-the-badge&labelColor=1a1a2e" alt="Tools">
<img src="https://img.shields.io/badge/dynamic/toml?url=https://raw.githubusercontent.com/german-krasnikov/unity-kiss-mcp/master/server/pyproject.toml&query=$.project.version&label=server&style=for-the-badge&labelColor=1a1a2e&color=888899" alt="Server Version">
<img src="https://img.shields.io/github/package-json/v/german-krasnikov/unity-kiss-mcp?filename=unity-plugin/package.json&label=plugin&style=for-the-badge&labelColor=1a1a2e&color=888899" alt="Plugin Version">

<sub>**STACK**</sub><br>
<img src="https://img.shields.io/badge/Unity-6000.0+-e8a23a?style=for-the-badge&labelColor=1a1a2e&logo=unity&logoColor=white" alt="Unity">
<img src="https://img.shields.io/badge/Python-3.10+-ccccff?style=for-the-badge&labelColor=1a1a2e&logo=python&logoColor=white" alt="Python">
<img src="https://img.shields.io/badge/MCP-1.0+-ccccff?style=for-the-badge&labelColor=1a1a2e&logo=anthropic&logoColor=white" alt="MCP">

</div>

> **Let Claude Code control your Unity Editor** — inspect scenes, edit GameObjects, run playtests, and capture screenshots without leaving the chat. Binary TCP protocol with 10–15× token compression and 80–95% batch savings.

<sub>MCP (Model Context Protocol) is Anthropic's open standard for giving AI assistants structured tool access.</sub>

<img src="docs/assets/divider.svg" width="100%" alt="">

## Why Unity MCP?

- **Stop alt-tabbing.** Claude inspects your scene, edits components, runs playtests, and captures screenshots without you leaving the chat.
- **Stop burning tokens on boilerplate.** Each `batch` call replaces 5–20 individual round-trips — **80–95% fewer tokens** on the same work.
- **Stop writing glue code.** Registered tools cover scene CRUD, animation, VFX, UI, shaders, runtime control, and code intelligence — with a plugin seam for your own.

### Two ways to work

🖥️ **CLI Mode** — run from terminal via Claude Code or any MCP client. The Python server connects to Unity over TCP :9500. Best for automation, batch operations, and scripting. Full access to 91 MCP tools with 80–95% token compression.

💬 **In-Unity Chat** — open `Window → MCP Chat` inside the editor. No API key needed — spawns the Claude or Codex CLI directly. Drag GameObjects, scripts, and materials into chat as typed context chips. Each AI turn gets its own undo group — one Ctrl+Z rolls back everything the AI changed. Domain-reload safe. Extensible chip-kind registry lets third-party plugins add new chip types with zero core edits.

**Before / after — creating and configuring 3 objects:**

```python
# Before: 9 separate MCP calls (~1800 tokens)
create_object("Enemy")
set_property("Enemy", "Transform", "position", "0,1,0")
manage_component("Enemy", "add", "Health")
set_property("Enemy", "Health", "maxHp", "100")
# ... 5 more calls
```

```python
# After: 1 batch call (~120 tokens, 93% savings)
batch([
  {"cmd": "create_object",    "path": "Enemy", "type": "Empty"},
  {"cmd": "set_property",     "path": "Enemy", "component": "Transform", "property": "position", "value": "0,1,0"},
  {"cmd": "manage_component", "path": "Enemy", "action": "add", "component": "Health"},
  {"cmd": "set_property",     "path": "Enemy", "component": "Health", "property": "maxHp", "value": "100"},
])
```

<img src="docs/assets/divider-heartbeat.svg" width="100%" alt="">

### Architecture

<img src="docs/assets/architecture.svg" width="100%" alt="Architecture: Claude Code → Python MCP Server → TCP :9500 → Unity Editor Plugin">

## Quick Start

**Prerequisites:** <kbd>Python 3.10+</kbd> · <kbd>Unity 6000.0+</kbd> · <kbd>Claude Code</kbd> · TCP port <kbd>9500</kbd> free

**1. Install the Python server**

```bash
git clone https://github.com/german-krasnikov/unity-kiss-mcp.git
cd unity-kiss-mcp/server && pip install -e ".[dev]"
```

**2. Install the Unity plugin**

Add to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.unity-mcp.editor": "file:/absolute/path/to/unity-kiss-mcp/unity-plugin"
  }
}
```

Open Unity — wait for `[MCP] Server started on port 9500` in the Console.

**3. Configure Claude Code**

Add to `~/.claude/mcp_settings.json`:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "python",
      "args": ["-m", "unity_mcp.server"],
      "cwd": "/absolute/path/to/unity-kiss-mcp/server"
    }
  }
}
```

Restart Claude Code. Call `get_hierarchy()` to verify.

<details>
<summary><b>Troubleshooting</b></summary>

- **Port 9500 not listening** — Ensure plugin is in `manifest.json`. Click Unity window to recompile. Check Console.
- **"Connection refused"** — Unity must be open with the plugin. Server auto-retries on reconnect.
- **Tools don't appear** — Verify path in `mcp_settings.json`. Restart Claude Code. `pip show unity-mcp`.
- **C# changes not reflected** — Click Unity window or `open -a Unity` (macOS) to trigger recompile.
- **Security** — TCP server binds to `localhost` only. Do not expose port 9500 to the network.

</details>

<details>
<summary><b>Compatibility</b></summary>

| Component | Tested | Minimum |
|-----------|--------|---------|
| Unity | 6000.0 (Unity 6) | 2021.3 LTS |
| Python | 3.12, 3.11, 3.10 | 3.10 |
| OS | macOS (primary), Windows, Linux | — |
| Claude Code | latest | any with MCP support |

</details>

<img src="docs/assets/stats.svg" width="100%" alt="91 MCP Tools · 2963 Tests (1723 Python · 1187 Unity · 53 Live) · 80-95% Batch Savings">

<img src="docs/assets/divider-wave.svg" width="100%" alt="">

## Features

- **Token Optimization** — `batch` compresses 5–20 calls into one (80–95% savings), deferred tool schemas, per-session cost analytics
- **In-Unity Chat** — Claude/Codex backends, no API key needed, typed context chips (`[hierarchy:/Player]`, `[script:Health.cs]`), per-turn undo, domain-reload safe
- **Code Intelligence** — Roslyn-powered `find_references`, `compile_preflight`, `semantic_at`
- **PlayTest DSL** — 21 commands: `MOVE`, `ASSERT`, `WAIT_UNTIL`, `INVOKE`, `SNAPSHOT`, `SIMULATE`
- **Scene Management** — CRUD, hierarchy inspection, query syntax, diff tracking, checkpoint/restore
- **Animation & Timeline** — clips, key management, Timeline assets, Animator states/transitions
- **VFX & Particles** — particle system CRUD, 11 module presets, shader graph integration
- **Multi-View Screenshots** — 4-panel grid (Front/Left/Top/Iso), bounding-box overlay, visual regression
- **Capability Gating** — TIER1 core always on; 8 category toggles per-session
- **Plugin Extensibility** — register your own tools in one file, no cross-imports

<details>
<summary><b>PlayTest DSL example</b></summary>

```
run_playtest(script="""
MOVE /Player TO 5,0,3
WAIT_UNTIL /Enemy|Health|hp <= 0 timeout=5
ASSERT /Player|Health|hp > 0
ASSERT_CONSOLE_CLEAN
SNAPSHOT /Player /Enemy
""")
```

</details>

<details>
<summary><b>Add your own tool</b> — one file, zero cross-imports</summary>

```python
# server/src/unity_mcp/tools/my_tool.py
def register(mcp, send, args):
    @mcp.tool()
    async def find_inactive(path: str = "/") -> str:
        """Find all inactive GameObjects under path."""
        return await send("find_objects", args(path=path, active="false"))
```

Drop the file in `tools/` — it's auto-discovered on next server start.

</details>

<img src="docs/assets/divider-pulse.svg" width="100%" alt="">

## Recent Changes

<div><sub>Full history: <a href="CHANGELOG.md"><b>CHANGELOG.md</b></a></sub></div>

<!-- CHANGELOG_START -->
<details>
<summary><b>v0.17.0</b> — 2026-06-05 — full-project code review sprint — 12 waves of fixes across Python + C#</summary>
</details>

<details>
<summary><b>v0.16.0</b> — 2026-06-05 — F12 chat UX overhaul — composed inline-chip field + response pills + session clear</summary>
</details>

<details>
<summary><b>v0.15.8</b> — 2026-06-05 — inline-chips + extensible chip-kind registry — F11</summary>
</details>

<details>
<summary>Older releases</summary>

- **v0.15.0** — 2026-06-04 — chat UX polish sprint — F1–F10 + review-hardening
- **v0.14.0** — 2026-06-04 — multi-backend agent chat — Claude + Codex via DRY CliBackendBase
- **v0.7.1** — 2026-06-04 — tech-debt sprint wave 1–3 (Python/C#/Chat) — pure quality
- **v0.7.0** — 2026-06-04 — Editor.log out-of-band corroboration — P0 compile-tool blindness fix
- **v0.6.1** — 2026-06-04 — atomic batch rollback — transactional scene edits
- **v0.5.0 / 0.12.0** — 2026-06-04 — scoped scene queries — search_scene root+limit + spatial center
- **v0.11.0** — 2026-06-04 — per-turn undo rollback + Restore button
- **v0.10.0** — 2026-06-04 — chat plan/act approve & execute + slash templates
- **v0.9.0** — 2026-06-04 — chat context resolution + compile gating tool
- **v0.8.0** — 2026-06-04 — compile auto-fix + editor-state injection + tool ping
- **v0.6.0** — 2026-06-03 — Aura pill + native theme + perms gating
- **v0.5.0** — 2026-06-03 — chat UX polish — refs, grouping, scroll
- **v0.4.0** — 2026-06-03 — extensible render: md + mermaid + img
- **v0.3.0** — 2026-06-03 — in-Unity Agent Chat + UIToolkit status
- **v0.2.6** — 2026-06-02 — tool-gating fix + settings UI

</details>
<!-- CHANGELOG_END -->

<img src="docs/assets/divider.svg" width="100%" alt="">

## Contributing

```bash
# Python tests (no Unity needed)
cd server && pytest -m "not live" -v

# With Unity running on :9500
pytest -m "live"

# C# tests — Unity Test Runner → EditMode
```

Architecture overview: [`AI/architecture.md`](AI/architecture.md) · Full tool catalog: [`AI/mcp-server.md`](AI/mcp-server.md)

<img src="docs/assets/divider.svg" width="100%" alt="">

<div align="center"><sub>MIT License · © <a href="https://github.com/german-krasnikov">German Krasnikov</a> · <a href="https://github.com/german-krasnikov/unity-kiss-mcp">⭐ Star</a></sub></div>
