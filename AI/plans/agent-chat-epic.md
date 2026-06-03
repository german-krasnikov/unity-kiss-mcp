# Agent Chat Epic — Build Spec (corrected)

Source: 27-agent design workflow (`wrh63klzc`) + adversarial fatal-flaw pass + CLI facts verified
against installed `claude 2.1.161` on 2026-06-03. Where the synthesized plan and the adversarial
review disagreed, the verified-against-code answer wins (recorded inline).

Build order: **F2 → F3 → F1 → F4** (risk-ascending). NO push. Files < 200 lines. Pure-logic files
(`*Parser`, `*Builder`, `*Spec`, `*Registry`, `*Data`) carry ZERO `UnityEngine` deps and are
NUnit-tested. Every slice: TDD (test first) → `run_tests` in Unity → code-reviewer → next.

## Verified CLI facts (decisive — checked, not assumed)
- `--agent <agent>` **EXISTS** ("Agent for the current session. Overrides the 'agent' setting").
  → F1 sub-agent routing is real. `--agents <json>` also exists (inline defs).
- `--permission-prompt-tool` **DOES NOT EXIST** in 2.1.161. F4 mid-turn approve/deny via that flag
  is impossible. `--permission-mode {default,plan,acceptEdits,bypassPermissions}`,
  `--allowedTools`, `--disallowedTools`, `--resume`, `--input-format stream-json` all exist.
- Help hints `--permission-mode … works with --input-format=stream-json` → a bidirectional
  stream-json `control_request`/`can_use_tool` channel MAY exist (SDK `canUseTool` path). This is
  the ONLY route to true mid-turn pause. **Unconfirmed → gated behind a live spike (see F4).**
- `codex` CLI **not installed** (`which codex` fails) → Codex backend deferred, NOT built.

---

## S1 (foundation) — harden arg quoting
`ChatProcess.QuoteArgs` (lines ~131-139) only wraps space-containing args, no escaping. F1 routes
user-derived agent names (from `.claude/agents/*.md` frontmatter) through it → injection/breakage.
Note: this is the **direct-exec** `ProcessStartInfo.Arguments` path, NOT the `/bin/zsh -lc` path, so
`LoginShellCommand.ShellQuoteSingle` (shell quoting) is the wrong tool. On Mono the Arguments string
is re-split by the runtime's own parser — wrap any arg containing space/quote/`$`/backtick/`\` in
double-quotes with `"` and `\` escaped. Pure helper + tests. Land before F1.

## S2 (foundation, part of F2) — `SessionInit` event
Root cause of F2 confirmed against code (see F2). Adds `ChatEventKind.SessionInit`.

---

## F2 — continuous activity animation (FIRST, ~6 lines + tests)
**Verified root cause (against code, not the plan's guess):** `ChatStreamParser.ParseSystem`
maps `system/init` → `ChatEvent.TurnDone` (line 86). `system/init` is the FIRST line Claude emits.
`Drain.HandleEvent` calls `_activity.Done()` on every `TurnDone` (line 40) → Phase → Idle
immediately → FlowBar stops. Later `FirstToken()` calls are no-ops (require Phase==Sending). That is
"animation stops after the first chunk." The real terminal event is the `result` line
(`ParseResult` → TurnDone), which stays untouched. The plan's `ToolActivity()`/debounce ideas are
no-ops (FlowBar already sweeps on any non-Idle phase; `FirstToken` already fires on tool chips).

**Fix:**
1. `ChatEvent.cs`: add `SessionInit` kind + `SessionInit(string sessionId)` factory.
2. `ChatStreamParser.cs:84-86`: `init` → `ChatEvent.SessionInit(sid)` (not TurnDone).
3. `ClaudeBackend.cs:61`: capture `SessionId` from `SessionInit` too (`|| ev.Kind==SessionInit`).
4. `MCPChatWindow.Drain.cs`: add `case SessionInit:` → no-op for activity/dots (keep Sending,
   keep `_waitingReply=true`).
5. Dead-process guard in `DrainAndRender`: `if (_backend!=null && !_backend.IsRunning &&
   _activity.Phase!=ActivityPhase.Idle) { _activity.Fail(); OnActivityChanged(); }` — so a killed/
   crashed process can't spin the bar forever.

**Tests (pure):** init→SessionInit (carries session_id, NOT terminal); result→TurnDone (terminal);
SessionInit factory shape. NO new USS, NO state-machine methods.

---

## F3 — drag-drop context chips + clickable response refs (additive, lowest risk)
- New pure `ChipData.cs` (readonly struct: `ChipKind {Scene, Asset}`, Path, DisplayName,
  `FormatForMessage()` → plain text `[scene:Path]` / `[asset:Path]`, token-cheap) + `ChipDataTests`.
- `MCPChatWindow.Chips.cs OnDragPerform`: accept any `UnityEngine.Object` (not only GameObject);
  assets via `AssetDatabase.GetAssetPath`. Type-allowlist (GameObject/Prefab/ScriptableObject/
  Material/Texture/AnimationClip) so folders/.meta don't pollute context. Path-based (NO
  GlobalObjectId — matches existing convention).
- **FATAL-FLAW FIX (plan claimed zero-work, FALSE):** `ChatRefAction.Navigate` (lines ~59-75) only
  branches `obj:`/`script:`. MUST add an `asset:` branch (`AssetDatabase.LoadAssetAtPath` +
  `EditorGUIUtility.PingObject` + `Selection.activeObject`). And `ChatLinkify.Apply` signature MUST
  gain a third asset-resolver arg (its only caller is `ChatBlockRendererRegistry.cs:61`).
- **Do NOT** auto-linkify by asset NAME (false positives on "Player"/"Default"). Only explicit
  `asset:`/`obj:`/`script:` prefixes are clickable.

## F1 — backend/agent selector dropdown (Claude + .md agents only; Codex deferred)
- `--agent <name>` is real → routing works. Enumerate `.claude/agents/*.md`.
- New pure: `AgentFrontmatterParser.cs` (line-scan `name:` between `---` fences, file-stem
  fallback, NO YAML dep) + `BackendSpec.cs` (readonly struct) + `BackendRegistry.Discover(dir)`.
- `MCPChatWindow.Selector.cs` (NEW partial — keeps `MCPChatWindow.cs` < 200) builds the dropdown.
- `ClaudeArgBuilder.Build` gains `string agentName=null` → appends `--agent <name>` (quoted via
  hardened S1 path).
- **DEFER Codex** (not installed, NDJSON schema unverified → "abstraction for the future", forbidden
  by KISS). Show a single greyed-out "Codex (coming soon)" entry only.
- **Skills:** no `-p` routing flag → OMIT. Header reads "Backend / Agent".
- **Signature coordination:** F1 and F4 both mutate `ClaudeArgBuilder.Build`. Design the final
  signature ONCE here (add `agentName` now, leave a clear seam for F4's allowlist params) so F4
  doesn't reopen it.

## F4 — permission gating (RE-SCOPED; literal mid-turn pause gated behind a spike)
**Reality (verified):** headless `claude -p` with `--allowedTools` SILENTLY blocks un-allowlisted
tools — no interactive prompt, no error text to regex. `--permission-prompt-tool` doesn't exist.
And `ClaudeArgBuilder` already emits `--allowedTools mcp__unity`, pre-approving every `mcp__unity__*`
tool — so any text-detector approach is dead on arrival.

**Primary (ship now): pre-grant allowlist UI.**
- New pure `PermissionConfig.cs` (allowed/disallowed tool patterns, EditorPrefs-backed CSV,
  `BuildAllowedToolsArg()` / `BuildDisallowedToolsArg()`).
- `MCPChatWindow.Permissions.cs` (NEW partial): a permissions button + flyout to edit patterns;
  "Apply" restarts the backend with new `--allowedTools`/`--disallowedTools` (NOT mid-turn).
- Correct tool id is `mcp__unity__<tool>` (server key is `unity`), NOT `mcp__unity-mcp__…`.

**Spike BEFORE building any mid-turn path:** run one controlled
`claude -p --input-format stream-json --output-format stream-json --permission-mode default`
against an un-allowlisted tool; observe stdout for a `control_request`/`can_use_tool` envelope and
whether a `control_response` on stdin unblocks it. IF it works → that is the true pause/approve/deny
channel (answered locally in `ChatProcess`, no MCP round-trip, no Unity single-client/deadlock
hazard). IF not → pre-grant allowlist is the final answer and the literal F4 request is
not satisfiable with CLI 2.1.161 (flag to user). The 6-architect "route through MCP
`--permission-prompt-tool`" idea is rejected: flag absent + Unity single-client re-entrancy/deadlock.

---

## Cross-cutting guards (from fatal-flaw pass)
- Process lifecycle on Mono: every backend switch / restart must hard-`Kill()` on timeout (Dispose
  ≠ kill); guard `ExitCode` behind `HasExited`. Applies to F1 switching + F4 restart.
- `MCPChatWindow.cs` is at 186 lines — F1 dropdown and F4 overlay MUST go in new partials.
- Token economy (project #1): chips emit plain text (no JSON); no name-based auto-linkify; agent
  scan cached, not re-run on every dropdown open.
