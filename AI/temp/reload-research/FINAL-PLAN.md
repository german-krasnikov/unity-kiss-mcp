# FINAL PLAN: sync_unity — Unified Unity Reload API

Synthesis of ARCH-1 (minimal-diff), ARCH-2 (clean-architecture), ARCH-3 (chat-first).
Self-contained spec for senior-developer. Read this + `AI/reload-reference.md` (ref-§N).

---

## 1. Decisions D1–D10 (final, one line each)

**D1 Tool surface.** New `sync_unity` Python tool in `tools/sync.py`. `recompile` stays as-is (backward compat, no alias/deprecation — changing it breaks existing LLM tool-call caches). `await_compile` stays; internally enhanced with epoch awareness. Source: consensus all three + KISS (don't break what works).

**D2 Python/C# split.** C# `sync` command: synchronous trigger, returns `sync_ack|epoch=N|will_compile=bool`. Python owns the wait-loop (polls `sync_status` + reconnect + corroboration). Source: consensus (hard constraint from ref-§13).

**D3 Trigger.** `AssetDatabase.Refresh()` always. `RequestScriptCompilation` never (ref-§2: no-op without dirty scripts; §2 IN-93874 risk). No `ForceUpdate` flag by default (it reimports unchanged files — wasteful). Source: ARCH-1/ARCH-3.

**D4 Client.Resolve.** Called BEFORE `Refresh()` when `resolve=true`. Python passes `resolve=true` when bump happened. No spike needed — ref-§4 "By Design" is unambiguous. Source: consensus.

**D5 Version bump.** Python-side `scripts/bump_version.py`, atomic `os.replace()`. Called BEFORE `sync` command. Source: consensus.

**D6 Chat wait.** Event-gated via `SyncHelper.IsCompileClean` (static bool, SessionState-backed). `TryResumePendingTurn` gates on it; if not clean, reschedules via `delayCall` (bounded retries). Chat does NOT poll `isCompiling` (provably racy per ref-§3). Source: ARCH-2/ARCH-3. ARCH-1's 2-line `isCompiling` gate rejected — ref-§3 proves false-gap, event-driven strictly correct.

**D7 Compile errors.** Report verbatim, never rollback. Source: consensus.

**D8 Idempotency.** `sync` C# returns `will_compile=false` when `Refresh()` produces no dirty scripts. Python skips wait-loop → fast path (<5s for R5). Source: consensus.

**D9 Epoch.** Monotonic int in `SessionState("MCP_SyncEpoch")`. Written to state file as 4th line. `sync_status` returns `epoch=N|state=S[|dur=X][|err=...]`. Python declares done only when epoch matches + state=ready + reconnect + 0 errors. Source: consensus.

**D10 Focus.** `osascript`/`open -a Unity` is dead. Scripted `Refresh()` is unconditional (ref-§5). Source: consensus.

---

## 2. Key Divergence Resolutions

### 2.1 C# Primitive Form (main dispute)

**Decision:** SyncHelper as a **public static class** (~100 lines) with an **ISyncOps injectable seam**. No interface on SyncHelper itself (static class, not instance). This is ARCH-2's design minus UnitySyncOps as a separate file (inline as a nested class).

**Why not ARCH-1 (no new class):** R14 requires chat to use the same primitive. Without SyncHelper, chat gates on raw `isCompiling` which ref-§3 proves racy. The `sync` command handler + `TryResumePendingTurn` would duplicate compile-state logic.

**Why not ARCH-3 (state machine with 5 states):** Overengineered. SyncHelper doesn't need Compiling/CompileDone/Reloading states — Python tracks these via poll. C# only needs: `CurrentEpoch`, `IsCompileClean` (for chat gate), `OnSyncComplete`/`OnSyncFailed` events. The "state machine" is implicit in the event subscriptions, not an explicit enum.

**CS0122 trap:** SyncHelper, ISyncOps, and the nested UnitySyncOps must all be `public`. Tests assembly accesses them directly.

### 2.2 recompile/await_compile fate

**Decision:** Both stay as-is. `recompile` remains `{ Refresh(); return "ok"; }` — it's a fire-and-forget tool for LLMs, not a sync primitive. `await_compile` gets epoch awareness internally (reads epoch from `sync_status` when available, falls back to old `compile_status` path). No deprecation, no alias. Source: ARCH-1 stability argument.

### 2.3 _needsRefresh debounce timing

**Decision:** Move to TurnDone (ARCH-1/ARCH-3), NOT mid-stream. Verified against `Drain.cs:174-178`: currently `_needsRefresh` fires `Refresh(ForceUpdate)` inside `DrainAndRender()` after every code-edit result. With 5 consecutive Write tool calls, this triggers 5 partial compiles → phantom CS errors. Fix: accumulate flag, single `Refresh()` at TurnDone after `ReloadGuard.OnTurnFinished()`. Actual code path: `HandleEvent(TurnDone)` → `ReloadGuard.OnTurnFinished()` → then check `_needsRefresh` → `SyncHelper.TriggerSync(resolve: false)`.

### 2.4 sync_status vs enhanced compile_status

**Decision:** New `sync_status` command (not mutating `compile_status` — preserves wire format for existing callers). Source: ARCH-1/ARCH-2.

---

## 3. Architecture

### 3.1 Components

```
C# (unity-plugin/Editor/):
  SyncHelper.cs           NEW ~100 lines — epoch, trigger, events, ISyncOps seam, IsCompileClean
  CommandRouter.cs:267    MODIFY — register sync + sync_status commands
  CommandRouter.cs:421    MODIFY — add sync_status to IsAllowedDuringCompile
  CompileNotifier.cs      MODIFY — add fail discriminator
  MCPServer.cs:80-88      MODIFY — fix false-ready race
  MCPServer.cs:130        MODIFY — Windows ExclusiveAddressUse
  MCPServer.cs:502-505    MODIFY — atomic state write
  Chat/MCPChatWindow.Drain.cs  MODIFY — resume gate + debounce
  Chat/ReloadGuard.cs     MODIFY — DisallowAutoRefresh + SessionState marker + InitializeOnLoad rebalance
  Chat/ChatProcess.cs:34  MODIFY — fix misleading comment

Python (server/src/unity_mcp/):
  tools/sync.py           NEW ~100 lines — sync_unity tool + poll loop
  tools/code_intel.py     MODIFY — epoch awareness in await_compile
  tools/scene.py:126      MODIFY — fix docstring
  unity_state.py          MODIFY — parse epoch (4th line)
  scripts/bump_version.py NEW ~45 lines — atomic patch bump
```

### 3.2 SyncHelper Public API (C#)

```csharp
namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    public static class SyncHelper
    {
        // --- State (SessionState-backed, survives reload) ---
        public static int    CurrentEpoch  { get; }    // MCP_SyncEpoch
        public static bool   IsCompileClean { get; }   // true when last sync completed successfully OR no sync pending

        // --- Events (for chat subscription; re-subscribe in InitializeOnLoad) ---
        public static event Action         OnSyncComplete;  // post-reload (new domain) or no-op sync
        public static event Action<string> OnSyncFailed;    // compile errors — arg = error text

        // --- Injectable seam (tests replace with mocks) ---
        public static ISyncOps Ops { get; set; }   // defaults to UnitySyncOps instance

        // --- Called from CommandRouter ---
        public static string TriggerSync(bool resolve);
        // Returns: "sync_ack|epoch=N|will_compile=true" or "sync_ack|epoch=N|will_compile=false"

        public static string GetSyncStatus();
        // Returns: "epoch=N|state=ready" / "epoch=N|state=compiling|dur=X" /
        //          "epoch=N|state=failed|err=..." / "epoch=N|state=idle"

        // --- Test seam ---
        public static void ResetForTest();
    }

    public interface ISyncOps
    {
        void Refresh();
        void Resolve();
        bool IsCompiling { get; }
        bool IsUpdating  { get; }
        bool ScriptCompilationFailed { get; }
    }

    // Nested or separate file, public for CS0122
    public sealed class UnitySyncOps : ISyncOps { /* thin wrappers around Unity APIs */ }
}
```

### 3.3 Protocol Contract (text, pipe-delimited)

```
# sync command → response
sync_ack|epoch=3|will_compile=true
sync_ack|epoch=3|will_compile=false

# sync_status command → response
epoch=3|state=ready
epoch=3|state=compiling|dur=4.2
epoch=3|state=failed|err=Assets/Foo.cs(12,5): error CS0103...
epoch=3|state=idle        # no sync triggered this session

# state file (4 lines):
ready                     # or compiling/reloading/compile_failed
1718000000.0              # unix timestamp
12345                     # PID
3                         # epoch (NEW — 4th line)
```

### 3.4 Sequence (ref-§13 mapped)

```
Python: bump_version.py → os.replace package.json
  │
  ├─ bridge.send("sync", {resolve: bumped})
  │    └─ C# TriggerSync: epoch++ → Resolve? → Refresh() → return sync_ack  [§13.2]
  ├─ compilationStarted → state file "compiling\n...\n...\nepoch"           [§13.3]
  ├─ compilationFinished (fires on FAIL too):
  │   FAIL → "compile_failed", OnSyncFailed, terminal                       [§13.4]
  │   OK → DO NOT write "ready" (R-4 fix)
  ├─ beforeAssemblyReload → going_away + "reloading"                        [§13.5]
  ├─ [domain swap]                                                           [§13.6]
  ├─ InitializeOnLoad → SyncHelper reads epoch → afterAssemblyReload
  │   → IsCompileClean=true, OnSyncComplete, state file "ready...epoch"     [§13.7]
  └─ Python: DomainReloadError → reconnect → poll sync_status
       epoch match + state=ready + corroborate → "sync clean"               [§13.8-9]
```

**will_compile:** SyncHelper checks `Ops.IsCompiling` on same frame after Refresh(). Imprecise but Python handles it via epoch polling anyway.

### 3.5 Five Known Races — Closure

| Race | Closed by |
|------|-----------|
| R-1 isCompiling false-gap | Epoch match in sync_status; never trust isCompiling alone |
| R-2 Failed-compile-no-reload | SyncHelper checks `scriptCompilationFailed` in `compilationFinished`; Python sees `state=failed`, no reconnect-wait |
| R-3 Stale "ready" from previous cycle | Epoch in SessionState+state file; Python matches before accepting |
| R-4 False-"ready" window | MCPServer.cs:80-88 fix: remove `compilationFinished→delayCall→ready`; "ready" only from post-reload `StartAsync:146` |
| R-5 Unbounded reload hang | Python 120s timeout + `is_process_dead()` fast-fail |

---

## 4. File List (~210 lines total diff)

**New files:** `SyncHelper.cs` (~100), `tools/sync.py` (~100), `scripts/bump_version.py` (~45).

**C# modifications:**
- `CommandRouter.cs:267,421` — register sync+sync_status, add to IsAllowedDuringCompile (+9)
- `CompileNotifier.cs:18-24` — scriptCompilationFailed → "idle-failed|dur" (+5)
- `MCPServer.cs:80-88` — remove compilationFinished→delayCall→"ready" (-5,+3)
- `MCPServer.cs:130` — `#if UNITY_EDITOR_WIN` ExclusiveAddressUse (+3)
- `MCPServer.cs:502-505` — atomic File.Move + epoch 4th line (+4,-2)
- `Drain.cs:63` — SyncHelper.IsCompileClean gate + delayCall reschedule max 30 (+8)
- `Drain.cs:174-178` — _needsRefresh → TurnDone, use SyncHelper.TriggerSync (+6,-4)
- `ReloadGuard.cs` — DisallowAutoRefresh + SessionState marker + InitializeOnLoad rebalance (+19)
- `ChatProcess.cs:34` — fix misleading comment (1)

**Python modifications:**
- `code_intel.py:60-107` — epoch-aware via sync_status (+10)
- `scene.py:126` — fix docstring (1)
- `unity_state.py` — epoch field, parse 4th line (+8)
- `gating.py` — add sync_unity to tier (+1)

---

## 5. §14 Coverage (12/14 fixed, 1 OK, 2 deferred)

**Fixed:** CommandRouter:267, scene.py:126, code_intel.py:60-107, CompileNotifier:18-24, MCPServer:80-88, MCPServer:130, MCPServer:502-505, Drain:61-131, Drain:164-167, ReloadGuard:31+50, ChatProcess:34-35.
**OK (no change):** MCPServer:474-485 (OnBeforeReload confirmed correct).
**Deferred (low risk):** bridge_heartbeat.py:39-54 (flat backoff, loopback only), editor_log_parser.py:22-46 (-logFile detection).

---

## 6. R23 Per-OS Branches

Only MCPServer.cs has per-OS branches: `#if UNITY_EDITOR_WIN` ExclusiveAddressUse (:130), File.Move atomic fallback (:502). `editor_log_parser.py` already per-OS. `bump_version.py` uses `os.replace()` (cross-platform). SyncHelper.cs and tools/sync.py have **no** per-OS code.

---

## 7. Test Plan P1–P4 (TDD order for developer)

### Step 0: Spike (do FIRST, before any implementation)

On 6000.3.0b7 in unity-test-project:
1. Call `Client.Resolve()` then `Refresh()` — observe single vs double reload cycle.
2. Call `File.Move(src, dst, true)` (3-arg overload) — verify it compiles (CoreCLR).
3. Verify `[InitializeOnLoad]` on SyncHelper fires before/alongside MCPServer's (log order).

If any spike fails, report to user before proceeding.

### Step 1: P2 C# EditMode — SyncHelper foundation

File: `unity-plugin/Editor/Tests/SyncHelperTests.cs` (~10 tests)

Seam: `SyncHelper.Ops = new MockSyncOps(...)` where `MockSyncOps : ISyncOps`.

| # | Red test | Green impl | TDD cycle |
|---|----------|-----------|-----------|
| 1 | `Epoch_Survives_SessionState_Roundtrip` | SessionState Set/Get in SyncHelper.CurrentEpoch | Foundation |
| 2 | `TriggerSync_Increments_Epoch` | TriggerSync body: epoch++ | Core |
| 3 | `TriggerSync_Calls_Refresh` | TriggerSync calls Ops.Refresh() | Core |
| 4 | `TriggerSync_Calls_Resolve_Before_Refresh_When_Requested` | if resolve: Ops.Resolve() before Refresh (mock records call order) | Core |
| 5 | `TriggerSync_Returns_WillCompile_True_When_Compiling` | Mock Ops.IsCompiling=true after Refresh → parse response | Core |
| 6 | `TriggerSync_Returns_WillCompile_False_When_Idle` | Mock Ops.IsCompiling=false → parse response | Core |
| 7 | `GetSyncStatus_Returns_Epoch_And_State` | GetSyncStatus() parses to "epoch=N|state=..." | Status |
| 8 | `CompileFailed_Fires_OnSyncFailed` | Simulate compilationFinished with ScriptCompilationFailed=true → event fired | Error path |
| 9 | `IsCompileClean_False_During_Compile` | Simulate compilationStarted → IsCompileClean == false | Chat gate |
| 10 | `IsCompileClean_True_After_Reload` | Simulate afterAssemblyReload path → IsCompileClean == true | Chat gate |

Run order: 1→2→3→4→5→6→7→8→9→10. After each red→green, run full suite.

### Step 2: P2 C# EditMode — CommandRouter + CompileNotifier

| # | Red test | Green impl |
|---|----------|-----------|
| 11 | `Sync_Command_Registered` (in existing CommandRouter tests or new) | Register "sync" + "sync_status" in CommandRouter |
| 12 | `SyncStatus_Allowed_During_Compile` | Add to IsAllowedDuringCompile |
| 13 | `CompileNotifier_Reports_Failed` (in existing or new) | Add scriptCompilationFailed check → "idle-failed|dur" |

### Step 3: P3 C# EditMode — Chat resume gate + debounce

File: `unity-plugin/Editor/Chat/Tests/ResumeGateTests.cs` (~6 tests)

Seam: Extract compile-clean check into testable path. SyncHelper.Ops = mock. SyncHelper.ResetForTest() between tests.

| # | Red test | Green impl |
|---|----------|-----------|
| 14 | `Resume_Blocked_While_Not_Clean` | Gate in TryResumePendingTurn on SyncHelper.IsCompileClean |
| 15 | `Resume_Proceeds_When_Clean` | IsCompileClean=true → dispatch proceeds |
| 16 | `Resume_Retry_Bounded_At_30` | 31 calls with IsCompileClean=false → gives up |
| 17 | `NeedsRefresh_Not_Acted_MidStream` | _needsRefresh=true during DrainAndRender → no Refresh call |
| 18 | `NeedsRefresh_Acted_At_TurnDone` | TurnDone + _needsRefresh → SyncHelper.TriggerSync called |
| 19 | `ReloadGuard_Pairs_Disallow_With_Lock` | OnTurnStarted → DisallowAutoRefresh + Lock both called |
| 20 | `ReloadGuard_SessionState_Rebalance` | Set marker, simulate InitializeOnLoad → ForceUnlock + marker cleared |

### Step 4: MCPServer.cs fixes (no new tests — covered by existing + Step 2)

Fix false-ready (:80-88), ExclusiveAddressUse (:130), atomic write (:502-505), epoch 4th line.

### Step 5: P1 Python unit tests

File: `server/tests/test_sync.py` (~10 tests)

Pattern: `_make_send` from `test_await_compile.py`. Mock `_send` at module level in `tools/sync.py`.

| # | Red test | Green impl | TDD cycle |
|---|----------|-----------|-----------|
| 21 | `test_idempotent_noop_fast_path` | sync_ack will_compile=false → skip poll, return fast | Simplest happy |
| 22 | `test_both_signals_required_for_clean` | sync_status ready+epoch match BUT editor_log stale → not clean until both | Core gate |
| 23 | `test_epoch_race_no_premature_idle` | sync_status returns epoch=2 when expected=3 → keep polling | Epoch |
| 24 | `test_reconnect_after_domain_reload` | DomainReloadError on first sync_status → sleep → retry → clean | Reconnect |
| 25 | `test_compile_failed_no_reload_wait` | sync_status state=failed → return errors immediately, no reconnect loop | Fail path |
| 26 | `test_compile_errors_verbatim` | state=ready+epoch match, get_compile_errors returns errors → errors in result | Error |
| 27 | `test_stale_dll_blocks_false_clean` | editor_log.corroborate returns stale warning → surfaced | Corroboration |
| 28 | `test_timeout_returns_partial` | Always compiling → timeout message | Timeout |
| 29 | `test_unity_dead_fails_fast` | ConnectionError on all calls → fast error | Dead process |
| 30 | `test_standalone_server_degrades` | No bridge → ToolError | Graceful |

Run order: 21→22→23→24→25→remaining.

### Step 6: P2 Python bump tests

File: `server/tests/test_bump_version.py` (~3 tests)

| # | Red test | Green impl |
|---|----------|-----------|
| 31 | `test_bump_patch_increments` | 0.20.3 → 0.20.4; read back matches | 
| 32 | `test_bump_atomic_no_partial` | concurrent read during write sees valid JSON |
| 33 | `test_bump_idempotent_on_second_call` | second bump → 0.20.5 (not 0.20.4) |

### Step 7: P1 Python — enhance await_compile

| # | Red test (in existing test_await_compile.py) | Green impl |
|---|----------|-----------|
| 34 | `test_await_compile_uses_sync_status_epoch` | When sync_status available, use epoch-aware wait |

### Step 8: P4 Live tests (ALWAYS LAST)

File: `server/tests/test_sync_live.py` (mark `@pytest.mark.live`)

| # | Test | Gate |
|---|------|------|
| 35 | `test_live_sync_full_cycle` | Write .cs → sync_unity → type visible |
| 36 | `test_live_sync_compile_error_then_fix` | Bad .cs → errors → fix → clean |
| 37 | `test_live_reconnect_transparent` | Sync triggers reload → reconnect works |
| 38 | `test_live_noop_sync_fast` | No changes → <5s |
| 39 | `test_live_plugin_bump_re_resolve` | Touch plugin .cs → sync_unity(bump=true) → version echo bumped |
| 40 | `test_live_dll_freshness` | After sync → editor_log freshness = True |

---

## 8. Developer Checklist

### Run order (strict)
pytest unit → bump package.json to 0.21.0 → `open -a Unity` → C# EditMode → C# PlayMode → `pytest -m "live"`.

### Known traps
- **CS0122**: SyncHelper/ISyncOps/UnitySyncOps must be `public`. Grep `CS0122` in BOTH dll Csc outputs.
- **Stale domain**: bump package.json version → `open -a Unity` → verify TestResults.xml run-count.
- **Run failed first**: filter param before full suite re-run.
- **5 pre-existing reds**: don't block on them.
- **Plugin version**: 0.21.0 (minor bump for new API surface).

### State file format change
4th line epoch is backward-compatible: Python parses only if `len(lines) >= 4`.

---

## 9. Open Questions → Spike Tests (Step 0)

| Question | Spike | If fails |
|----------|-------|----------|
| `Client.Resolve()` + `Refresh()` ordering: single or double reload? | Call both on 6000.3.0b7, observe | If double: call Resolve first, use reconnect as confirm for both |
| `File.Move(src, dst, true)` compiles on Unity 6000 CoreCLR? | Try in editor script | If not: use existing Delete+Move with try/catch |
| `[InitializeOnLoad]` order SyncHelper vs MCPServer | Log both static ctors | If MCPServer first: SyncHelper must NOT depend on MCPServer state; use SessionState only |

Developer runs these 3 spikes FIRST. If any blocks, report to user before proceeding with main work.
