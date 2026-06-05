<div align="center">

<img src="docs/assets/hero.svg" width="100%" alt="Unity MCP — control Unity from Claude, a live heartbeat status window with a breathing mint-green orb, ECG trace, and TCP packet field">

<a href="https://github.com/german-krasnikov/unity-kiss-mcp">
<img src="https://readme-typing-svg.demolab.com?font=Fira+Code&weight=600&size=22&pause=900&color=3AD29F&center=true&vCenter=true&width=760&lines=Control+Unity+from+Claude.;Token-minimized+MCP+tools.;80-95%25+batch+token+savings.;The+editor's+heartbeat%2C+made+visible." alt="Control Unity from Claude — token-minimized MCP tools — 80-95% batch token savings">
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
<img src="https://img.shields.io/badge/Unity-2021.3+-e8a23a?style=for-the-badge&labelColor=1a1a2e&logo=unity&logoColor=white" alt="Unity">
<img src="https://img.shields.io/badge/Python-3.10+-ccccff?style=for-the-badge&labelColor=1a1a2e&logo=python&logoColor=white" alt="Python">
<img src="https://img.shields.io/badge/MCP-1.0+-ccccff?style=for-the-badge&labelColor=1a1a2e&logo=anthropic&logoColor=white" alt="MCP">

</div>

> **MCP server bridging Claude Code to the Unity Editor over a binary protocol** — 10–15× token compression, capability gating, and a live status window whose heartbeat the banner above mirrors beat-for-beat.

<img src="docs/assets/divider.svg" width="100%" alt="">

## Why Unity MCP?

- **Stop alt-tabbing.** Claude inspects your scene, edits components, runs playtests, and captures screenshots without you leaving the chat.
- **Stop burning tokens on boilerplate.** Each `batch` call replaces 5–20 individual round-trips — **80–95% fewer tokens** on the same work.
- **Stop writing glue code.** Registered tools cover scene CRUD, animation, VFX, UI, shaders, runtime control, and code intelligence — with a plugin seam for your own.

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

<img src="docs/assets/divider.svg" width="100%" alt="">

## Quick Start

**Prerequisites:** <kbd>Python 3.10+</kbd> · <kbd>Unity 2021.3+</kbd> · <kbd>Claude Code</kbd> · TCP port <kbd>9500</kbd> free

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

</details>

<table>
<tr>
<td align="center" width="33%"><h2><code>91</code></h2><sub>MCP TOOLS</sub><br><sub>core + plugin</sub></td>
<td align="center" width="33%"><h2><code>1136</code></h2><sub>TESTS PASSING</sub><br><sub>unit + live</sub></td>
<td align="center" width="33%"><h2><code>80–95%</code></h2><sub>BATCH SAVINGS</sub><br><sub>vs. individual calls</sub></td>
</tr>
</table>

<img src="docs/assets/divider.svg" width="100%" alt="">

## Features

- 💸 **Token Optimization** — `batch` ops compress 5–20 calls into one (80–95% savings), deferred schemas, per-session analytics
- 🎬 **PlayTest DSL** — 21-command runtime testing language: `MOVE`, `ASSERT`, `WAIT_UNTIL`, `INVOKE`, `SNAPSHOT`, `SIMULATE`
- 🏗️ **Scene Management** — full CRUD, hierarchy inspection, Unity query syntax, diff tracking, checkpoint/restore
- 🎞️ **Animation & Timeline** — clips with key management, Timeline assets, Animator controllers with states/transitions
- ✨ **VFX & Particles** — particle system CRUD with 11 module presets + shader graph integration
- 🎨 **Multi-View Screenshots** — 4-panel grid (Front/Left/Top/Iso), bounding-box overlay, visual regression baseline
- 🔍 **Code Intelligence** — Roslyn-powered `find_references`, `compile_preflight`, `semantic_at` — no disk writes
- 🎯 **Capability Gating** — TIER1 core always visible; 8 category toggles per-session (`SCENE_EDIT`, `ANIMATION`, `VFX`, `UI`, `ASSETS`)
- 💬 **In-Unity Agent Chat** — `MCPChatWindow` spawns Claude/Codex CLI directly — domain-reload safe, typed context chips, per-turn undo
- 🔌 **Plugin Extensibility** — register your own tools in one file, no cross-imports

<img src="docs/assets/divider.svg" width="100%" alt="">

## Recent Changes

<!-- CHANGELOG_START -->
## [v0.15.8] — 2026-06-05
- **Inline Chips + Extensible Chip-Kind Registry (Plugin v0.15.8, F11)** (2026-06-05) — Production-ready extensible typed-context-chip system for in-Unity agent chat.

## [v0.15.0] — 2026-06-04
- **Chat UX Sprint: 10 Features + Review-Hardening (Plugin v0.15.0)** (2026-06-04) — Six-wave comprehensive UX polish for in-Unity agent chat.
<!-- CHANGELOG_END -->

<div align="center"><sub>Auto-updated by CI — see <a href="CHANGELOG.md"><b>CHANGELOG.md</b></a> for full history</sub></div>

<img src="docs/assets/divider.svg" width="100%" alt="">

<div align="center">

<img src="https://github.com/german-krasnikov.png" width="80" alt="German Krasnikov">

**German Krasnikov** — the human at the console.

Released under the **MIT License** — see [`LICENSE`](LICENSE).

**If the heartbeat resonates, give it a star**

<a href="https://github.com/german-krasnikov/unity-kiss-mcp">
<img src="https://img.shields.io/github/stars/german-krasnikov/unity-kiss-mcp?style=for-the-badge&labelColor=1a1a2e&color=3ad29f&logo=github&logoColor=white" alt="GitHub stars">
</a>

</div>
