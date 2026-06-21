# Unity MCP — Control Unity Editor from Any AI Assistant

<div align="center">

<img src="docs/assets/hero.svg" width="100%" alt="Unity MCP — control Unity from Claude, a live heartbeat status window with a breathing mint-green orb, ECG trace, and TCP packet field">

<a href="https://github.com/german-krasnikov/unity-kiss-mcp">
<img src="https://readme-typing-svg.demolab.com?font=Fira+Code&weight=600&size=22&pause=900&color=3AD29F&center=true&vCenter=true&width=760&lines=The+editor's+heartbeat%2C+made+visible.;Control+Unity+from+Claude%2C+Codex%2C+and+more.;80-95%25+batch+token+savings.;Scene+CRUD+%C2%B7+Animation+%C2%B7+VFX+%C2%B7+PlayTest+DSL." alt="Control Unity from Claude — token-minimized MCP tools — 80-95% batch token savings">
</a>

</div>

<!-- ───────────────────────────  BADGE WALL  ─────────────────────────── -->

<div align="center">

<sub>**STATUS**</sub><br>
<img src="https://img.shields.io/github/license/german-krasnikov/unity-kiss-mcp?style=for-the-badge&labelColor=1a1a2e&color=3ad29f&logo=opensourceinitiative&logoColor=white" alt="License">
<img src="https://img.shields.io/github/stars/german-krasnikov/unity-kiss-mcp?style=for-the-badge&labelColor=1a1a2e&color=3ad29f&logo=github&logoColor=white" alt="Stars">
<img src="https://img.shields.io/github/last-commit/german-krasnikov/unity-kiss-mcp?style=for-the-badge&labelColor=1a1a2e&color=3ad29f&logo=git&logoColor=white" alt="Last Commit">

<sub>**SPEC**</sub><br>
<img src="https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/german-krasnikov/unity-kiss-mcp/master/.github/badges/tests.json&style=for-the-badge&labelColor=1a1a2e" alt="Tests">
<img src="https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/german-krasnikov/unity-kiss-mcp/master/.github/badges/tools.json&style=for-the-badge&labelColor=1a1a2e" alt="Tools">
<img src="https://img.shields.io/badge/dynamic/toml?url=https://raw.githubusercontent.com/german-krasnikov/unity-kiss-mcp/master/server/pyproject.toml&query=$.project.version&label=SERVER%20BETA&style=for-the-badge&labelColor=1a1a2e&color=888899" alt="Server Version">
<img src="https://img.shields.io/github/package-json/v/german-krasnikov/unity-kiss-mcp?filename=unity-plugin/package.json&label=PLUGIN%20BETA&style=for-the-badge&labelColor=1a1a2e&color=888899" alt="Plugin Version">

<sub>**STACK**</sub><br>
<img src="https://img.shields.io/badge/Unity-6000.0+-e8a23a?style=for-the-badge&labelColor=1a1a2e&logo=unity&logoColor=white" alt="Unity">
<img src="https://img.shields.io/badge/Python-3.10+-ccccff?style=for-the-badge&labelColor=1a1a2e&logo=python&logoColor=white" alt="Python">
<img src="https://img.shields.io/badge/MCP-1.0+-ccccff?style=for-the-badge&labelColor=1a1a2e&logo=anthropic&logoColor=white" alt="MCP">

</div>

> **Let any MCP-compatible AI assistant control your Unity Editor** — inspect scenes, edit GameObjects, run playtests, and capture screenshots without leaving the chat. Binary TCP protocol with 10–15× token compression and 80–95% batch savings.

<sub>MCP (Model Context Protocol) is Anthropic's open standard for giving AI assistants structured tool access.</sub>

<img src="docs/assets/divider.svg" width="100%" alt="">

## Why Unity MCP?

- **Stop alt-tabbing.** Your AI assistant inspects your scene, edits components, runs playtests, and captures screenshots without you leaving the chat.
- **Stop burning tokens on boilerplate.** Each `batch` call replaces 5–20 individual round-trips — **80–95% fewer tokens** on the same work.
- **Stop writing glue code.** Registered tools cover scene CRUD, animation, VFX, UI, shaders, runtime control, and code intelligence — with a plugin seam for your own.

### Two ways to work

🖥️ **CLI Mode** — run from terminal via Claude Code, Codex CLI, or any MCP client. The Python server connects to Unity over TCP :9500. Best for automation, batch operations, and scripting. Full access to 99 MCP tools with 80–95% token compression.

💬 **In-Unity Chat** — open `Window → MCP Chat` inside the editor. No API key needed — spawns the CLI directly. 5 backends: Claude, Antigravity, Kimi, Codex, OpenCode. Drag GameObjects, scripts, and materials into chat as typed context chips. Each AI turn gets its own undo group — one Ctrl+Z rolls back everything the AI changed. Domain-reload safe. Extensible chip-kind registry lets third-party plugins add new chip types with zero core edits.

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

<img src="docs/assets/architecture.svg" width="100%" alt="Architecture: MCP Client → Python MCP Server → TCP :9500 → Unity Editor Plugin">

## Quick Start

**Prerequisites:** <kbd>Python 3.10+</kbd> · <kbd>Unity 6000.0+</kbd> · <kbd>TCP port 9500</kbd> free

**One-liner (macOS/Linux):**

```bash
curl -fsSL https://raw.githubusercontent.com/german-krasnikov/unity-kiss-mcp/master/install/bootstrap.sh | bash
```

**One-liner (Windows PowerShell):**

```powershell
iex (iwr https://raw.githubusercontent.com/german-krasnikov/unity-kiss-mcp/master/install/bootstrap.ps1).Content
```

<details>
<summary>Windows: antivirus blocked the script?</summary>

Run these commands manually in a **new PowerShell window**:

```powershell
winget install astral-sh.uv
git clone https://github.com/german-krasnikov/unity-kiss-mcp.git "$HOME\.unity-mcp\server"
cd "$HOME\.unity-mcp\server"
uv run python install.py setup
```

(The new window ensures `uv` is available in PATH after installation.)

</details>

<details>
<summary>Manual MCP configuration (all AI tools)</summary>

Add this to your MCP config file:

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

| AI Tool | Config file path |
|---------|-----------------|
| Claude Code | `~/.claude.json` |
| Claude Desktop | `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or `%APPDATA%\Claude\claude_desktop_config.json` (Windows) |
| Cursor | `~/.cursor/mcp.json` |
| Windsurf | `~/.codeium/windsurf/mcp_config.json` (macOS/Linux) or `%APPDATA%\Codeium\windsurf\mcp_config.json` (Windows) |
| Kimi | `~/.kimi-code/mcp.json` |
| VS Code | `~/Library/Application Support/Code/User/mcp.json` (macOS), `%APPDATA%\Code\User\mcp.json` (Windows), or `~/.config/Code/User/mcp.json` (Linux) |
| OpenCode | `~/.config/opencode/opencode.json` (macOS/Linux) or `%APPDATA%\opencode\opencode.json` (Windows) |
| Codex | `~/.codex/config.toml` |

**Without uvx:** replace `"command": "uvx", "args": ["--from", "git+...", "unity-mcp"]` with `"command": "python", "args": ["-m", "unity_mcp.server"]`

</details>

**Manual setup:**

1. Python server: `uvx --from git+https://github.com/german-krasnikov/unity-kiss-mcp.git#subdirectory=server unity-mcp` — zero-install, runs on demand from GitHub (no separate install step).
2. Add the Unity plugin via **Package Manager → Add package from git URL:**
   ```
   https://github.com/german-krasnikov/unity-kiss-mcp.git?path=unity-plugin
   ```
3. Open Unity, then open the **Setup Wizard** via **MCP → Setup Wizard** menu. It will:
   - Auto-detect installed AI tools (shows "detected" badge)
   - One-click configure: pick your tool → choose Global or Project scope → done
   - Supports 9 backends: Claude Code, Claude Desktop, Cursor, Windsurf, VS Code, Codex, Kimi, OpenCode, Antigravity

**Configure an AI tool manually:**

```bash
python install.py configure --tool claude-code
# Project-scoped config (writes .mcp.json to project root):
python install.py configure --tool claude-code --project-dir /path/to/unity/project
```

Supported tools: `claude-code`, `claude-desktop`, `cursor`, `windsurf`, `vscode`, `codex`, `kimi`, `opencode`

**Verify installation:**

```bash
python install.py doctor
```

Shows Python version, venv status, config validity, and TCP port connectivity.

<details>
<summary><b>Compatibility</b></summary>

| Component | Tested | Minimum |
|-----------|--------|---------|
| Unity | 6000.3 | 6000.0 (Unity 6) |
| Python | 3.12 | 3.10 |
| OS | macOS | macOS, Windows, Linux |
| Claude Code | latest | any with MCP support |
| Codex CLI | latest | any with MCP support |
| Claude Desktop | latest | any with MCP support |

</details>

<img src="docs/assets/stats.svg" width="100%" alt="99 MCP Tools · 4275 Tests (0 Python · 4275 Unity · 0 Live) · 80–95% Batch Savings">

<img src="docs/assets/divider-wave.svg" width="100%" alt="">

## Features

- **Token Optimization** — `batch` compresses 5–20 calls into one (80–95% savings), deferred tool schemas, per-session cost analytics
- **In-Unity Chat** — 5 CLI backends (Claude, Antigravity, Kimi, Codex, OpenCode), no API key needed, typed context chips (`[hierarchy:/Player]`, `[script:Health.cs]`), per-turn undo, domain-reload safe
- **Code Intelligence** — Roslyn-powered `find_references`, `compile_preflight`, `semantic_at`
- **PlayTest DSL** — 21 commands: `MOVE`, `ASSERT`, `WAIT_UNTIL`, `INVOKE`, `SNAPSHOT`, `SIMULATE`
- **Multi-Scene Management** — Load multiple scenes, inspect/edit across scenes, move/copy objects between loaded scenes, unified `object_diff` for cross-scene comparison
- **Scene CRUD & Tools** — `scene` actions (open_additive, close, set_active, list), hierarchy inspection, query syntax, diff tracking, checkpoint/restore
- **Animation & Timeline** — clips, key management, Timeline assets, Animator states/transitions
- **VFX & Particles** — particle system CRUD, 11 module presets, shader graph integration
- **Multi-View Screenshots** — 4-panel grid (Front/Left/Top/Iso), bounding-box overlay, visual regression
- **Multi-Project Ports** — each Unity project auto-assigns a unique TCP port (9500–9599), CLI and Chat get isolated slots
- **Capability Gating** — TIER1 core always on; 8 category toggles per-session
- **Cross-Platform** — Windows, macOS, Linux — binary resolution, lockfile, and venv per platform
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
<summary><b>v0.50.2</b> — 2026-06-21 — **WizardConfigWriter visibility** — changed class and `GitInstallUrl` from …</summary>

**WizardConfigWriter visibility** — changed class and `GitInstallUrl` from `internal` to `public` for cross-assembly access from …

</details>

<details>
<summary><b>v0.50.1</b> — 2026-06-21 — **Update Cache Loop** — `UpdateChecker` now clears EditorPrefs cache after …</summary>

**Update Cache Loop** — `UpdateChecker` now clears EditorPrefs cache after successful Level Up (v0.50.0 regression).

</details>

<details>
<summary><b>v0.50.0</b> — 2026-06-21 — **Wizard Fallback** — Setup Wizard detects missing backends (e.g., no Claude …</summary>

**Wizard Fallback** — Setup Wizard detects missing backends (e.g., no Claude Code) and provides next-best-option UI (v0.47.1).

</details>

<details>
<summary><b>v0.47.0</b> — 2026-06-21 — Replaces USD cost display with input/output token counts + context window fill %</summary>

Replaces USD cost display with input/output token counts + context window fill %

</details>

<details>
<summary><b>v0.46.0</b> — 2026-06-21 — **Polygon2D** — Immutable 2D polygon (XZ plane), winding-number …</summary>

**Polygon2D** — Immutable 2D polygon (XZ plane), winding-number point-in-polygon test (nonzero fill rule), AABB bounds computation, CSV …

</details>

<details>
<summary>Older releases</summary>

- **v0.45.0** — 2026-06-20 — **InstallSourceDetector** — Detects `file:` (local Git clone) vs `git:` (UPM …
- **v0.44.1** — 2026-06-20 — **Fix: Codex Windows path crash** — TOML `command` now uses literal strings …
- **v0.44.0** — 2026-06-20 — LevelUpPanel: 4-state machine (Idle→Animating→Done→Diff) with XP bar + sparkles …
- **v0.43.0** — 2026-06-20 — Remove tundra.digestcache deletion (SIGABRT in RegisterAssemblyDefinition)
- **v0.42.0** — 2026-06-20 — **Setup Wizard One-Button Install** — 3-screen flow (Welcome → PickBackend → …
- **v0.41.4** — 2026-06-20 — **@Mention Autocomplete** — Type `@` in Chat input to trigger autocomplete …
- **v0.41.0** — 2026-06-20 — **Session Handoff (Chat↔CLI)** — Button "→ CLI" in Chat copies resume command …
- **v0.40.1** — 2026-06-19 — **Fix: Chat duplicate TCP connections** — Claude Chat no longer spawns …
- **v0.40.0** — 2026-06-19 — **One-Liner Installation** — `curl | bash` (macOS/Linux) or `iex (iwr).Content` …
- **v0.38.0** — 2026-06-19 — **External MCP Server Support in Chat:**
- **v0.37.0** — 2026-06-18 — **Bridge Stability & Reload Recovery (v0.36.0):**
- **v0.36.0** — 2026-06-18 — **Media Preview Redesign:**
- **v0.35.0** — 2026-06-17 — **Inline Media Preview Bubbles** — Phase 2 lazy-load media panel in chat:
- **v0.34.6** — 2026-06-17 — **Binary Resolver — macOS zsh PATH sourcing** — Changed `bash -lc` to `zsh …
- **v0.34.0** — 2026-06-17 — **Plugin Extensibility API** — New public interfaces for plugins to extend chat …
- **v0.33.0** — 2026-06-16 — **Codex Silent Abort Fix (Plugin v0.33.0)** — Fixes hung turns when Codex tools …
- **v0.32.0** — 2026-06-16 — **run_tests Fire-and-Forget Protocol (Server v0.32.0)** — `run_tests(mode)` now …
- **v0.31.1** — 2026-06-16 — **run_tests Domain Reload Disconnect Recovery (Server v0.31.1)** — Fixes silent …
- **v0.31.0** — 2026-06-16 — **Security Hardening (Gate A: release blocker)** — CodeExecutor.SecurityScan …
- **v0.30.4** — 2026-06-16 — **Per-Backend Model Selector (Plugin v0.30.4)** — Dropdown in MCPChatWindow …
- **v0.30.3** — 2026-06-16 — **Gemini CLI Backend (Plugin v0.30.1, v0.30.2, v0.30.3)** — Third CLI backend …
- **v0.29.38** — 2026-06-15 — **Codex Interactive User Input (Plugin v0.29.38)** — Codex CLI can now show …
- **v0.29.37** — 2026-06-15 — **Claude Interactive User Input (Plugin v0.29.37, Server)** — Claude CLI …
- **v0.29.11** — 2026-06-15 — **Interactive Permission Protocol Fix (Plugin v0.29.11, Sprint 1C)** — Fixes …
- **v0.29.2** — 2026-06-15 — **Chat Assembly Split (Plugin v0.29.2)** — `UnityMCP.Editor.Chat` split into …
- **v0.27.4** — 2026-06-14 — **Reload Recovery Package (Plugin + Server v0.27.4)** — Independent UPM package …
- **v0.26.0** — 2026-06-13 — **Test Quality Audit (Server + Plugin v0.26.0)** — Systematic cleanup of test …
- **v0.25.13** — 2026-06-12 — **UTF-8 Encoding Round-3 (Server + Plugin v0.25.13)** — **(C1: Python test I/O …
- **v0.25.12** — 2026-06-12 — **UTF-8 Everywhere (Server + Plugin v0.25.12)** — **(Round 1)** Python file I/O …
- **v0.25.0** — 2026-06-12 — **Multi-Scene CRUD + Diff (Plugin v0.25.0)** — Cross-scene `transfer_object` …
- **v0.24.1** — 2026-06-12 — **Port Re-Discovery on Reconnect (Server v0.24.1)** — UnityBridge now …
- **v0.24.0** — 2026-06-12 — **Multi-Scene Hierarchy Support (Plugin v0.24.0)** — `get_hierarchy` now …
- **v0.23.13** — 2026-06-11 — **SettingsNavController Hardening (Plugin v0.23.13)** — Timer-based animated …
- **v0.23.0** — 2026-06-11 — **Reconnect Recovery: Zombie Detection + SO_REUSEPORT + TCP Probe (Server + …
- **v0.22.1** — 2026-06-11 — **Crash Logging for Unhandled Server Exceptions** — Python MCP server now …
- **v0.22.0** — 2026-06-11 — **Multi-Project Port Configuration (Plugin + Server v0.22.0)** — Unity projects …
- **v0.21.0** — 2026-06-11 — **Cross-Platform Windows/Linux Support (Plugin v0.21.0 + Server)** — Plugin now …
- **v0.20.7** — 2026-06-10 — Reload-resume re-sends the full-path chip payload, not short-name mentions (task#10)
- **v0.20.6** — 2026-06-10 — Full-path chip payload + always-raw "Show LLM payload" inspector for every turn type
- **v0.20.0** — 2026-06-10 — Chip-unification Phase 1 — delete SceneNameLinker path, unified @-mention rendering
- **v0.19.2** — 2026-06-10 — Chat reload double-bubble MAJOR + drag-drop crash guard + clean test console
- **v0.19.1** — 2026-06-10 — P0/P1 chat UX hardening — ResetTurnFlags DRY, bubble dedup, backend restore race
- **v0.19.0** — 2026-06-10 — Chat UX F27–F30 — Domain reload + external drag/drop + input height + backend cleanup
- **v0.18.0** — 2026-06-10 — Chat UX F20–F26 — Stop button, reload survival, AutoScroll, dropdown persist, @Object dedup, direct Clear, drag/drop MonoScript
- **v0.17.36** — 2026-06-06 — Settings Hub redesign — central hub UI + circuit-node header animation + Claude foldout grouping
- **v0.17.34** — 2026-06-06 — F25 Phase 2 settings hub — unique thematic header animations per sub-window
- **v0.17.28** — 2026-06-06 — F23 settings split — 3 focused EditorWindows + Chat event hook
- **v0.17.20** — 2026-06-06 — 40-architect test audit — 299 new tests total, 3 P0+P1 bug fixes
- **v0.17.18** — 2026-06-06 — F20–F22 bugfixes — select-all, @mention search, orphan bold
- **v0.17.17** — 2026-06-05 — F15a-F19 chip redesign — linker disable, leading-space guard, context menus
- **v0.17.14** — 2026-06-05 — F13–F14 inline-chip architecture + bare-name normalizer + review fixes
- **v0.17.2** — 2026-06-05 — inline context chips + review fixes (regex + staleness + test DRY)
- **v0.17.0** — 2026-06-05 — full-project code review sprint — 12 waves of fixes across Python + C#
- **v0.16.0** — 2026-06-05 — F12 chat UX overhaul — composed inline-chip field + response pills + session clear
- **v0.15.8** — 2026-06-05 — inline-chips + extensible chip-kind registry — F11
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
- **v0.7.0** — 2026-06-04 — F4 deferred schema + reload-survival + auto-selection
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
