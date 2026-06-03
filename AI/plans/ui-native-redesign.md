# UI-Native Redesign — Build Spec (reconciled)

Source: two UI/UX expert agents (visual-fidelity lens + IA/layout/motion lens), reconciled by
orchestrator. Serves user Msg2 (chat window) + Msg3 (Status window + bottom status-bar pill).

Constraints: files < 200 lines; pure-logic files NUnit-tested; NDA-clean; Russian thinking / English
code; minimal change, break nothing. Do NOT touch README.md or docs/assets/*.svg (WIP). Theme: use
`var(--unity-colors-*)` everywhere chrome is painted; semaphore status colors stay literal but tuned
for both skins.

VERIFY-status theme vars (confirm live in UI Toolkit Debugger; a wrong name renders TRANSPARENT, not
an error): `--unity-colors-button-background-pressed`, `--unity-colors-highlight-background-hover`.
If transparent, fall back to the nearest OK neighbor.

---

## A. Chat window (`MCPChatWindow.cs` / `.Selector.cs` / `.Drain.cs` / `.FlowBar.cs` / `.uss`)

### A1. Remove the header entirely
- Delete `BuildToolbar()` (cs:71-79) and its `root.Add(BuildToolbar())` call (cs:44).
- Delete `_modePill` field (cs:19) and all references (SetMode cs:118-120).
- Window tab title `GetWindow<MCPChatWindow>("MCP Chat")` stays (that's the tab, correct). No in-window title.

### A2. Cost → tokens only
- Delete `_totalCostUsd` field (cs:15). Keep `_inputTokens`, `_outputTokens`.
- Rename `_costBadge` (cs:19) → `_tokenReadout`.
- New pure helper (TDD, new file `TokenFormat.cs`, zero Unity deps):
  `Abbr(int n) => n >= 1000 ? $"{n/1000f:0.0}k" : n.ToString();`  + `TokenFormatTests`.
- Drain.cs:55-59 — replace the cost block:
  - guard `if (ev.CostUsd > 0f)` → `if (ev.InputTokens > 0 || ev.OutputTokens > 0)`
  - body: `_inputTokens += ev.InputTokens; _outputTokens += ev.OutputTokens;`
    `_tokenReadout.text = $"↑ {TokenFormat.Abbr(_inputTokens)}  ↓ {TokenFormat.Abbr(_outputTokens)}";`
  - `ev.CostUsd` stays on the struct (unused) — no struct change.

### A3. Single bottom footer (replaces `.input-area` + `.input-actionbar`)
New element tree for the composer (root children top→bottom): `chat-scroll` (now FIRST child),
`resize-handle`, then the composer footer `.input-area` (keep class name to reuse drag-handle wiring):
```
input-area  (composer)
├─ flowbar            (TOP edge — activity line, see A5)
├─ obj-chip-strip     (unchanged)
├─ chat-input         (multiline, unchanged)
└─ footer-bar  (.footer-bar — replaces .input-actionbar)
   ├─ agent-selector  (.agent-selector .footer-selector)  flex-shrink:1; min-width:0; max-width:140px
   ├─ mode-segment    (.mode-segment) [Ask][Agent] segmented  flex-shrink:0
   ├─ footer-spacer   (.footer-spacer) flex-grow:1
   ├─ token-readout   (.token-readout) "↑ 1.2k  ↓ 840"  flex-shrink:0
   ├─ ss-btn          (.chat-btn .chat-btn--screenshot)  flex-shrink:0
   └─ send-btn        (.chat-btn .chat-btn--send)  flex-shrink:0
```
- Move `BuildAgentSelector()` (Selector.cs) call from the deleted toolbar into the footer-bar.
- FlowBar moves from "under toolbar" to first child of the composer (`input-area`), full width.
- The selector `min-width:0` is MANDATORY (UIToolkit won't shrink a DropdownField below content
  width otherwise → Send pushed off-screen at 320px min). Live-verify fit at exactly 320px width.

### A4. Mode toggle = native segmented control (kills the redundant pill)
- Wrap the two `MakeModeBtn` buttons in `.mode-segment`:
  `flex-direction:row; border:1px var(--unity-colors-button-border); border-radius:4px; overflow:hidden;`
- `.mode-toggle-btn`: remove individual `border-radius` (uss:296) and `margin-right` (uss:300);
  set `border-width:0`; add `border-right:1px var(--unity-colors-button-border)` on the FIRST (Ask)
  button only; height 20px. Active button keeps `--active` (checked bg + highlight text).
- Store the two button refs (fields `_askBtn`, `_agentBtn`) so `SetMode` (cs:113) can swap `--active`
  between them (the deleted pill no longer reflects state). SetMode keeps `_agentMode` flip +
  backend recreate (115-117); delete the `_modePill.*` lines (118-120).

### A5. FlowBar: track + traveling chip (the "impressive" animation)
Current bug: the whole 2px bar translates ±100% → slides fully off-screen (looks like a glitch).
Fix = fixed track with `overflow:hidden` + an inner fill chip that sweeps on-screen.
- `BuildFlowBar` (FlowBar.cs:16): add one child `_flowFill` (class `flowbar__fill`).
- `OnActivityChanged` (FlowBar.cs:24): on Sending/Receiving add `flowbar--active` to track + set chip
  color class (`flowbar__fill--sending` / `--receiving`); reset chip to `--a`. On Idle remove
  `--active` (track fades to opacity 0) + clear color + `--a/--b`.
- `TickFlowBarSweep` (FlowBar.cs:40): toggle `--a`/`--b` on `_flowFill` (not the track).
- Schedule (cs:66): `Every(800)` → `Every(950)` (slightly longer than the 0.9s transition so each leg
  completes — current 800<800 retriggers mid-flight = stutter).
- Color crossfade Sending→Receiving is free: add `background-color` to the chip's transition-property;
  the existing `FirstToken()` phase flip swaps the class → smooth highlight→amber morph.
USS:
```css
.flowbar { height: 2px; flex-shrink: 0; overflow: hidden; opacity: 0;
           transition-property: opacity; transition-duration: 0.3s; }
.flowbar--active { opacity: 1; }
.flowbar__fill { position: absolute; top: 0; bottom: 0; width: 33%; border-radius: 1px;
                 transition-property: translate, background-color; transition-duration: 0.9s;
                 transition-timing-function: ease-in-out; translate: -120% 0; }
.flowbar__fill--sending   { background-color: var(--unity-colors-highlight-background); }
.flowbar__fill--receiving { background-color: #e0a050; }
.flowbar__fill--a { translate: -40% 0; }
.flowbar__fill--b { translate: 220% 0; }
```
Delete the old `.flowbar--idle/--sending/--receiving/--sweep-a/--sweep-b` rules (uss:602-629) and the
`PhaseClasses` array contents change to drive the chip — keep idle = opacity 0 (F2 contract intact:
motion bound to `_activity.Phase`, do NOT break Send/FirstToken/Done/Fail wiring).

### A6. Delete typing-dots (single activity signal)
FlowBar now covers the full lifecycle (send→receive). Two indicators = noise. Remove:
- `_typingDots`, `_dotCount` fields; CreateGUI lines 56-58 + `TickDots` schedule (cs:65);
- `TickDots` method (Drain.cs:86-91); every `_typingDots.style.display = ...` line in Drain.cs
  (26, 51, 60, 66, 77) and cs (142, 161).
- `_waitingReply` may become unused after this — if so, delete it too (check Drain dead-process guard
  cs:22-27 still compiles; it only needs the Phase check + Fail()).

### A7. Buttons + dividers native (Expert A)
- `.chat-btn`: `border-radius` 5px→3px; REMOVE `-unity-font-style: bold`; add
  `.chat-btn:active { background-color: var(--unity-colors-button-background-pressed); }` (VERIFY var).
- `.mode-toggle-btn`: `border-radius` 4px→3px (then overridden to 0 inside the segment, A4).
- `.chat-btn--send`: add `color: var(--unity-colors-highlight-text);` (contrast on the blue fill).
  Keep highlight-background fill (the only native-legal primary emphasis; used on one button).
- ONE divider only: `.input-area` (composer) `border-top` → color `var(--unity-colors-default-border)`
  (was `toolbar-border` — the "too similar" faint line); bg → `var(--unity-colors-window-background)`
  (flush with transcript). REMOVE `border-top` from `.resize-handle` (uss:164-165) and from
  `.input-actionbar`/`.footer-bar`. Add `.resize-handle:hover { background-color:
  var(--unity-colors-toolbar_button-background-hover); }` so the drag-grip is distinct from the
  passive divider (grip + resize cursor + bg shift = 3 affordance signals).
- `.token-readout`: reuse `.cost-badge` base (uss:50) — `font-size:10px; color:
  var(--unity-colors-label-text); margin-left:8px;`. Rename the selector.

---

## B. MCP Status window (`MCPStatus.uss`) — kill navy, go theme-native (Expert A)
Chrome → theme vars; semaphore (orb/halo/word) → literal, tuned for both skins.
| Selector | New value |
|---|---|
| `.mcp-root` bg | `var(--unity-colors-window-background)` (was `#1a1a2e`) |
| `.brand` color | `var(--unity-colors-label-text)` (was `#888899`) |
| `.status-sub` color | `var(--unity-colors-label-text)` (was `#888899`) |
| `.status-word--down` color | `var(--unity-colors-error-text)` (was `#e94560`) |
| orb/halo/`.status-word--up`/`--listen` | KEEP literals (`#1f7a5c`/`#3ad29f`/`#8a6512`/`#e8a23a`/`#6e2b3a`) |
| `.mcp-btn` | strip to just `margin: 3px;` — let native `.unity-button` paint it (delete bg/border/color/radius/transition/font-size) |
| `.mcp-btn:hover` | DELETE the rule (native hover takes over) |
Keep `MCPStatusWindow.cs` element tree + beat schedules + the `orb--*/halo--*/status-word--*` class
contract in `RefreshState()` untouched.

---

## C. Bottom status-bar pill (`MCPStatusBarWidget.cs`)

### C1. LEFT placement, no overlap (Expert B)
- `BuildPill` (cs:84-87): remove `position:Absolute`, `right`, `top`, `bottom`. Set
  `flexShrink = 0; alignItems = Center; alignSelf = Center; marginLeft = 4; marginRight = 6;`
  (keep `flexDirection = Row`).
- `TryInject` (cs:46): `root.Add(_pillContainer)` → `root.Insert(0, _pillContainer)` (leftmost child →
  pushes Unity's own widgets right → no overlap by construction).
- DEV-ONLY: log `root.childCount` + each child `name`/type once in TryInject to confirm the root is a
  flex container and the pill positions correctly (status bar can't be screenshot-verified via MCP —
  needs the user's eyes). Remove the log before final commit.

### C2. Persistence hardening (Expert B)
Root cause of "inconsistent": tree rebuild WITHOUT assembly reload (docking/maximize/play-mode)
detaches the pill; `_injected=true` latch blocks re-inject.
- Guard (cs:38): `if (_injected) return;` → `if (_injected && _pillContainer?.panel != null) return;`
- Add a panel-independent self-heal on `EditorApplication.update`, throttled to ~1/sec via
  `EditorApplication.timeSinceStartup`: if `_pillContainer == null || _pillContainer.panel == null`
  call `TryInject()`. (The container's own `schedule` stops when detached, so it can't self-heal —
  hence a global ticker.) Keep existing `beforeAssemblyReload += Cleanup`.

### C3. Pulse semantics — motion = activity, healthy = steady (Expert B, reconciled: NO server change)
User complaint: "blinks incorrectly." Current Up flips opacity 1.0↔0.35 every 600ms (~1.7Hz hard
blink = reads as alert). Fix:
| State | Animation | Opacity |
|---|---|---|
| Up (connected) | STEADY (no pulse) | 1.0 constant |
| Listen (no client) | gentle eased breathe | 0.85 ↔ 0.6 |
| Down (stopped) | steady dim | 0.5 constant |
- `PulseTick` (cs:56): keep the schedule for label refresh + state poll, but only TOGGLE opacity for
  Listen; for Up set 1.0 once + early-return (like the current Down early-return at cs:61), for Down
  set 0.5 once + early-return.
- Make the breathe eased (not a hard jump): set an inline transition ONCE in `BuildPill`:
  `_pill.style.transitionProperty = new List<StylePropertyName>{ "opacity" };`
  `_pill.style.transitionDuration = new List<TimeValue>{ new TimeValue(0.6f, TimeUnit.Second) };`
  (or the equivalent inline API). Slow the schedule to `Every(900)` so the breathe is calm.
- Drop the "serving/in-flight breathe" idea (would need a new `MCPServer.IsServing` hook → the TCP
  server is fragile, leave it alone). Up=steady fully resolves the user's complaint.

### C4. Skin-aware pill colors (Expert A) — inline, no USS
Replace `ApplyPillColor` (cs:111-129) switch with `EditorGUIUtility.isProSkin`-branched, LOW-ALPHA
tint backgrounds (so the native status-bar shade reads through — Unity's own counters aren't solid
blocks) + vivid semaphore text:
```
Up:     text pro #45D99E / light #1A8057 ; bg same hue @ alpha 0.16 (pro) / 0.14 (light)
Listen: text pro #ECA83D / light #996B0C ; bg same hue @ alpha 0.16 / 0.14
Down:   text pro #ED4D66 / light #B31F33 ; bg same hue @ alpha 0.16 / 0.14
```
(Use `Color` literals; exact floats from Expert A's table.)

---

## TDD targets (real, the rest is structural/USS verified by compile + no-regression + visual)
- `TokenFormat.Abbr(int)` — new pure helper + `TokenFormatTests` (boundaries: 0, 999, 1000, 1234, 12345).

## Deferred (flagged, NOT in this epic — user listed only 3 surfaces)
- `MCPSettings.uss` has the IDENTICAL navy clash (`#1a1a2e`/`#2a2a3e`/`#444466`/`#ccccff` in header,
  `.preset-btn`, `.search-field`). Same treatment as §B will be needed for full consistency. Surface
  to user for greenlight; do not implement here.

## Verification protocol (compile-guard double-run)
focus Unity (osascript) → sleep ~20s → `get_compile_errors` → `run_tests` (FIRST run contaminated by
domain-reload race → ignore ~204 "Unity is compiling" spurious failures) → `run_tests` AGAIN with NO
edits between (clean). Expect the established 5 pre-existing failures, ZERO new regressions, +N for
TokenFormatTests. Status-bar placement/pulse + chat visual = describe for the user (status bar can't
be MCP-screenshotted).
