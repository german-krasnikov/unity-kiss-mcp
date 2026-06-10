# ARCH-2: Clean Architecture / SyncHelper Primitive

Perspective: one testable SyncHelper primitive (C#) + symmetric Python orchestrator, injectable ops seam, clear contracts between layers. Testability > diff size, but KISS (no future-proofing abstractions).

## 1. Decisions D1-D10

**D1: Tool surface** -- New `sync_unity` tool (Python). `recompile` stays as-is (lightweight fire-and-forget Refresh for backward compat). `await_compile` is refactored to use the shared epoch+state-file gate internally but keeps its public API. `sync_unity` = `sync` C# command + Python orchestration loop. Rationale: existing callers of `recompile` and `await_compile` must not break; `sync_unity` is the new "do-it-right" primitive that chains trigger+wait+verify.

**D2: Python/C# orchestration split** -- C# `sync` handler: synchronous, triggers `Refresh()` (+ optional `Client.Resolve()`), writes epoch to SessionState, returns `sync_ack epoch=N will_compile=bool`. Python `sync_unity`: sends `sync`, then polls `sync_status` (new command that returns epoch+state+errors) in a loop with ConnectionError/DomainReloadError retry. Wait-loop lives entirely in Python per the hard constraint (ref-S13). C# never blocks.

**D3: Trigger semantics** -- `Refresh()` always (unconditional on prefs/focus per ref-S5). `RequestScriptCompilation(None)` only when `force=true` arg is passed AND Refresh produced no dirty scripts (avoiding redundant double-compile). Default: `Refresh()` alone covers external .cs edits (ref-S2: "Request alone never sees externally-edited un-imported .cs files"). `ForceUpdate` flag on Refresh: only when `force=true` (ref-S1 ImportAssetOptions).

**D4: Client.Resolve placement** -- `sync` command accepts optional `resolve=true` arg. When set, calls `Client.Resolve()` BEFORE `Refresh()` (ref-S4 decision table: "unknown scope -> call both"). Python `sync_unity` sets `resolve=true` when `bump=true` was used (package.json changed = metadata change). Spike needed (ref-S15 #5) to verify ordering on 6000.3.0b7 -- if registeredPackages fires AFTER reload, Python should just use the reconnect as the confirm signal.

**D5: Version-bump owner** -- Python-side `scripts/bump_version.py` (standalone, importable). Reasons: (a) C# self-modifying its own package.json triggers the very reload it's trying to orchestrate -- race; (b) Python has full filesystem access and can do atomic tmp+rename; (c) bump happens BEFORE `sync` command -- clean separation. Idempotent: reads current version, bumps patch only if source files changed since last bump (compares git diff against last tagged version), writes atomically via tmp file + `os.replace()` (POSIX atomic, Windows atomic since Python 3.3). The `sync_unity` tool calls bump internally when `bump=true`.

**D6: Chat wait mechanism** -- Chat's `TryResumePendingTurn` (Drain.cs:63) gates on `SyncHelper.IsIdle` (new static property combining `!isCompiling && !isUpdating && !SyncHelper.IsSyncing`). If not idle, schedules `delayCall` retry (imperfect per ref-S3 false-gap, but strictly better than current no-gate). Chat does NOT use its own polling loop -- it subscribes to `SyncHelper.OnSyncComplete` event (fired from `afterAssemblyReload` path in new domain). This is the SAME primitive the TCP `sync` command writes through.

**D7: Compile-error policy** -- Report, never rollback. `sync_unity` returns verbatim compile errors from `get_compile_errors` + editor_log corroboration. Failed compile = terminal state per ref-S9 ("NO reload, old assemblies live"). Python reports errors and stops -- no automatic retry. Rationale: rollback requires knowing what to roll back to, which is the caller's responsibility.

**D8: Idempotency detection** -- `sync_status` returns current epoch. If Python's `sync` command returns `will_compile=false` (no dirty scripts detected by Refresh), Python checks state file = "ready" and skips the wait loop entirely. Cost: one TCP round-trip (~2ms). Satisfies R5 (no-op sync <5s).

**D9: Epoch/token protocol** -- C# `SyncHelper` owns an `int _epoch` in SessionState (key `"MCP_SyncEpoch"`). Incremented atomically on every `sync` trigger. Written to state file alongside state name. Python reads epoch from `sync_ack` and from `sync_status` responses. Declares done ONLY when: epoch matches AND state="ready" AND connection alive AND zero compile errors. Closes race R1 (stale "ready" confusion) from ref-S13.

**D10: Focus dependency** -- `open -a Unity` / `osascript activate` is DEAD. Scripted `Refresh()` is unconditional on focus/prefs (ref-S5, confirmed HIGH). Our in-process TCP dispatch already runs on the editor main loop (unfocused editor ticks service it -- Rider proof ref-S5). No per-OS focus hacks needed. Doc-keeper removes all osascript references from CLAUDE.md/skills.

## 2. Architecture

### Components

```
Python side:
  tools/sync.py          -- sync_unity tool + sync_status polling loop (~120 lines)
  scripts/bump_version.py -- atomic idempotent version bumper (~60 lines)
  (existing) editor_log.py, compile_state.py, unity_state.py -- reused as-is

C# side:
  SyncHelper.cs (NEW)    -- single primitive: trigger + epoch + state + events (~130 lines)
  (existing) CompileNotifier.cs -- reused (compile timing)
  (existing) MCPServer.cs -- state file writer (patched: false-ready fix)
  (existing) CommandRouter.cs -- register sync/sync_status commands
```

### SyncHelper API Surface (C#)

```csharp
namespace UnityMCP.Editor
{
    // Public: tests need access (CS0122 constraint). No internal-only types.
    public static class SyncHelper
    {
        // --- State ---
        public static int Epoch { get; }              // SessionState-backed, survives reload
        public static bool IsSyncing { get; }          // true between trigger and reload-complete/fail
        public static bool IsIdle { get; }             // !isCompiling && !isUpdating && !IsSyncing

        // --- Events (for Chat subscription) ---
        public static event Action OnSyncComplete;     // fired post-reload (new domain) or on no-op
        public static event Action<string> OnSyncFailed; // compile errors

        // --- Ops Seam (injectable for tests) ---
        public static ISyncOps Ops { get; set; }       // defaults to real Unity APIs

        // --- Trigger (called from CommandRouter) ---
        public static string Trigger(bool resolve, bool force);
        // Returns: "sync_ack epoch=N will_compile=bool"

        // --- Status (called from CommandRouter) ---
        public static string GetStatus();
        // Returns: "epoch=N state=ready|compiling|reloading|failed err=..."
    }

    // Injectable ops seam -- tests replace with mocks
    public interface ISyncOps
    {
        void Refresh(bool forceUpdate);
        void RequestCompilation(bool clean);
        void Resolve();
        bool IsCompiling { get; }
        bool IsUpdating { get; }
        bool ScriptCompilationFailed { get; }
    }

    // Production implementation (thin wrapper around Unity APIs)
    public sealed class UnitySyncOps : ISyncOps { ... }
}
```

### Data Flow: sync_unity Full Cycle

```
Python sync_unity(bump=true)
  |
  +--> bump_version.py: read package.json, bump patch, atomic write
  |
  +--> bridge.send("sync", {resolve:true, force:false})
  |       |
  |       +--> C# SyncHelper.Trigger(resolve=true, force=false)
  |       |     1. epoch++ in SessionState
  |       |     2. Client.Resolve()  [if resolve]
  |       |     3. AssetDatabase.Refresh()  [+ ForceUpdate if force]
  |       |     4. Write state file "syncing epoch=N"
  |       |     5. Return "sync_ack epoch=N will_compile=true"
  |       |
  |       +--> compilationStarted fires  (old domain, async to Trigger return)
  |       +--> SyncHelper persists epoch in SessionState
  |
  +--> Python poll loop: bridge.send("sync_status", {})
  |       |
  |       +--> C# SyncHelper.GetStatus()
  |       |     Returns "epoch=N state=compiling" / "epoch=N state=ready" / ...
  |       |
  |       +--> ConnectionError/DomainReloadError: sleep, retry (bridge reconnects)
  |       |     -- THIS IS the external "reload complete" signal (ref-S13 step 7)
  |       |
  |       +--> state=ready AND epoch=N: fetch errors via get_compile_errors
  |       +--> corroborate with editor_log (existing)
  |       +--> Return "sync clean epoch=N (Xs)" or errors
  |
  +--> [optional] version-echo: bridge.send("get_version", {})
         Verify returned version matches bumped version (R18)
```

### Mapping to ref-S13 Handshake (step-by-step)

| S13 Step | Implementation | Race Closed |
|----------|---------------|-------------|
| 1. File edits done | Caller responsibility; bump_version.py flushes | -- |
| 2. Python sends sync -> C# Refresh() | `SyncHelper.Trigger` via CommandRouter | -- |
| 3. compilationStarted -> persist epoch | SyncHelper hooks compilationStarted, epoch in SessionState | R3 (epoch survives reload) |
| 4. compilationFinished -> check fail | SyncHelper hooks compilationFinished; checks scriptCompilationFailed | R2 (failed=terminal, no wait) |
| 5. beforeAssemblyReload -> going_away | MCPServer.OnBeforeReload (existing), state file "reloading" | -- |
| 6. Domain swap | Managed state dies; SessionState + state file survive | R3 |
| 7. New domain: InitializeOnLoad -> rebind -> afterAssemblyReload | SyncHelper re-reads epoch from SessionState; fires OnSyncComplete; state file "ready" written FROM afterAssemblyReload path (not compilationFinished) | Race 4 (false-ready) |
| 8. Out-of-band corroboration | editor_log.corroborate() in Python | R2 (both-signals) |
| 9. Python declares synced | epoch match + ready + reconnected + 0 errors | R1, R2 |

### Five Known Races (ref-S13) Closure

| Race | How Closed |
|------|-----------|
| R1: isCompiling false-gap | Never poll isCompiling. Epoch+event-driven only. Python trusts sync_status state field, not flags. |
| R2: Failed-compile-no-reload | SyncHelper.compilationFinished checks scriptCompilationFailed -> writes state "failed", fires OnSyncFailed. Python sees state=failed, reports errors, stops waiting for reconnect. |
| R3: Epoch need (no-op Refresh) | Every sync increments epoch. will_compile=false -> Python checks state=ready+epoch match, skips wait. No ambiguity with stale ready. |
| R4: False-"ready" window | "ready" is NEVER written from compilationFinished. Success-path "ready" comes ONLY from afterAssemblyReload (SyncHelper [InitializeOnLoad] or afterAssemblyReload callback). MCPServer.cs:80-88 patched. |
| R5: Unbounded reload hang | Python-side timeout (default 120s). CompileStateProbe.is_process_dead() for fast-fail. State file "reloading" age > 120s = stale. |

### Contract: C# -> Python (TCP text responses)

```
sync command response:
  "sync_ack epoch=3 will_compile=true"
  "sync_ack epoch=3 will_compile=false"

sync_status command response:
  "epoch=3 state=ready"
  "epoch=3 state=compiling dur=4.2"
  "epoch=3 state=reloading"
  "epoch=3 state=failed err=Assets/Foo.cs(12,5): error CS0103 ..."
  "epoch=3 state=idle"    (no sync was triggered this session)

get_version response (existing, used for R18 version-echo):
  "0.21.0"
```

Text format, short keys, no JSON -- consistent with project token optimization principles.

## 3. Files

### New Files

| File | Size | Purpose |
|------|------|---------|
| `unity-plugin/Editor/SyncHelper.cs` | ~130 lines | Core primitive: epoch, trigger, events, ISyncOps seam |
| `unity-plugin/Editor/UnitySyncOps.cs` | ~40 lines | Production ISyncOps impl (thin Unity API wrapper) |
| `server/src/unity_mcp/tools/sync.py` | ~120 lines | sync_unity tool + poll loop |
| `server/scripts/bump_version.py` | ~60 lines | Atomic idempotent version bumper |
| `server/tests/test_sync.py` | ~180 lines | P1 Python unit tests |
| `unity-plugin/Editor/Tests/SyncHelperTests.cs` | ~120 lines | P2 C# EditMode tests |
| `unity-plugin/Editor/Tests/ResumeGateTests.cs` | ~60 lines | P3 chat gate tests |

### Modified Files

| File:line | Change | Size |
|-----------|--------|------|
| `CommandRouter.cs:267` | Register `sync` and `sync_status` commands (delegate to SyncHelper) | +5 lines |
| `CommandRouter.cs:421-424` | Add `sync_status` to IsAllowedDuringCompile | +1 line |
| `MCPServer.cs:80-88` | Remove compilationFinished -> delayCall -> WriteStateFile("ready"); move success-path "ready" to afterAssemblyReload via SyncHelper | -6/+2 lines |
| `MCPServer.cs:130` | `#if UNITY_EDITOR_WIN` ExclusiveAddressUse=true | +3 lines |
| `MCPServer.cs:502-505` | Replace File.Delete+Move with safe atomic write (File.Move with overwrite or File.Replace) | ~3 lines |
| `CompileNotifier.cs:18-24` | Add fail discriminator: "idle-failed\|dur" when scriptCompilationFailed is true | +4 lines |
| `MCPChatWindow.Drain.cs:63` | Gate TryResumePendingTurn on `SyncHelper.IsIdle` | +3 lines |
| `MCPChatWindow.Drain.cs:164-167` | Replace per-event _needsRefresh with turn-end debounced Refresh | ~5 lines |
| `ReloadGuard.cs:28-31` | Add DisallowAutoRefresh before Lock, AllowAutoRefresh after Unlock (ref-S6 safe pattern) | +4 lines |
| `ReloadGuard.cs` (new block) | Add [InitializeOnLoad] SessionState rebalance for native counter survival | +12 lines |
| `ChatProcess.cs:34-35` | Fix misleading comment about guard recovery across reload | 1 line |
| `tools/code_intel.py:60-107` | Import sync module for shared epoch check; await_compile internally uses sync_status when available (graceful fallback to old path) | ~10 lines |
| `tools/scene.py:126` | Fix recompile docstring (does NOT wait for completion) | 1 line |
| `server/src/unity_mcp/tools/gating.py` | Add sync_unity to TIER1 | 1 line |

## 4. S14 Coverage Table

| S14 Row (File:line) | Fixed? | Notes |
|---------------------|--------|-------|
| CommandRouter.cs:267 (bare Refresh returns "ok") | YES | `sync` command returns "sync_ack epoch=N will_compile=bool"; old `recompile` stays as-is for compat |
| scene.py:126 (false docstring) | YES | Fix docstring |
| code_intel.py:60-107 (false "idle" in gap) | YES | await_compile enhanced with epoch+state-file gate via sync_status |
| CompileNotifier.cs:18-24 (fail indistinguishable) | YES | Add "idle-failed" state |
| MCPServer.cs:80-88 (false-ready) | YES | Success-path "ready" moved to afterAssemblyReload only |
| MCPServer.cs:130 (ReuseAddress on Windows) | YES | ExclusiveAddressUse under #if |
| MCPServer.cs:474-485 (OnBeforeReload) | NO (already correct) | Confirmed OK per ref |
| MCPServer.cs:502-505 (non-atomic write) | YES | Atomic write fix |
| Drain.cs:61-131 (no isCompiling gate) | YES | Gated on SyncHelper.IsIdle |
| Drain.cs:164-167 (per-event Refresh) | YES | Debounced to turn-end |
| ReloadGuard.cs:31,50 (Lock without Disallow) | YES | Full safe pattern + SessionState rebalance |
| ChatProcess.cs:34-35 (misleading comment) | YES | Comment fixed |
| bridge_heartbeat.py:39-54 (flat cooldown) | NO (out of scope) | Low risk on loopback; optional enhancement |
| editor_log_parser.py:22-46 (-logFile detection) | NO (out of scope) | Doc-level concern; existing parser correct for default paths |

**Score: 12/14 fixed, 2 deferred (low risk, independent concerns).**

## 5. R23 Cross-Platform

Per ref-S16, the sync core is OS-independent (in-process TCP, no focus hacks). Per-OS branches:

| Branch | Where | What |
|--------|-------|------|
| Socket rebind | `MCPServer.cs:130` | `#if UNITY_EDITOR_WIN` -> ExclusiveAddressUse=true; keep ReuseAddress on Unix |
| State file atomic write | `MCPServer.cs:502-505` | `File.Move(tmp, path, overwrite: true)` -- .NET 5+ only; fallback: try/catch Delete+Move on .NET Framework (Unity 2021 Mono). OR: always File.Replace on Windows, rename on POSIX |
| Editor.log path | `editor_log_parser.py:22-46` | Already per-OS (darwin/win32/linux) -- no change needed |
| bump_version.py | `os.replace()` | Atomic on all 3 OS (Python 3.3+) |

No new per-OS branches in SyncHelper.cs or sync.py -- all platform variance is in existing infrastructure.

## 6. Test Plan (P1-P4 mapping)

### P1: Python unit tests (server/tests/test_sync.py)

Pattern: `_make_send` from test_await_compile.py. Mock `_send` module-level var in `tools/sync.py`.

| Test | Mocks | Verifies |
|------|-------|----------|
| `test_both_signals_required_for_clean` | send returns sync_ack+ready+reconnect, but editor_log says stale | Does NOT declare clean until both signals agree |
| `test_stale_dll_blocks_false_clean` | send returns clean, editor_log.corroborate returns stale warning | Regression pin for editor_log.py:101-103 |
| `test_epoch_race_no_premature_idle` | sync_ack epoch=3, sync_status returns epoch=2 state=ready | Waits until epoch=3 appears |
| `test_reconnect_after_self_inflicted_reload` | DomainReloadError on first sync_status, then epoch=N state=ready | Clean result after reconnect |
| `test_timeout_returns_partial_status` | sync_status always "compiling" | Returns timeout message with last known state |
| `test_compile_errors_verbatim` | sync_status state=failed with errors | Returns exact error text |
| `test_idempotent_noop_fast_path` | sync_ack will_compile=false | Skips poll loop, returns fast |
| `test_unity_dead_fails_fast` | send raises ConnectionError, probe.is_process_dead()=True | Fails immediately, no retry |
| `test_preexisting_compile_drains_first` | First sync_status: state=compiling (pre-existing), then ready | Waits for pre-existing compile to finish |
| `test_standalone_server_degrades` | No bridge connected | Graceful ToolError, not crash |

TDD order: `test_idempotent_noop_fast_path` (simplest happy path) -> `test_both_signals_required_for_clean` -> `test_epoch_race_no_premature_idle` -> `test_reconnect_after_self_inflicted_reload` -> remaining.

### P2: C# EditMode tests (unity-plugin/Editor/Tests/SyncHelperTests.cs)

Injectable ops via ISyncOps seam. Test SyncHelper static methods with mock ops.

| Test | Mocks | Verifies |
|------|-------|----------|
| `test_sync_helper_calls_refresh_then_compile` | ISyncOps mock tracking call order | Refresh before RequestCompilation; Resolve before Refresh when resolve=true |
| `test_sync_request_returns_will_compile` | ISyncOps.IsCompiling returns true after Refresh | Response contains "will_compile=true" |
| `test_epoch_survives_domain_reload` | Direct SessionState read/write | Epoch value persists across simulated reload (SessionState.SetInt/GetInt round-trip) |
| `test_state_file_busy_recognition` | Real file I/O to temp path | State file written with "compiling", read back, parsed correctly |
| `test_sync_noop_when_no_changes` | ISyncOps.IsCompiling returns false after Refresh | will_compile=false |
| `test_failed_compile_fires_event` | ISyncOps.ScriptCompilationFailed=true | OnSyncFailed event fired with errors |

TDD order: `test_epoch_survives_domain_reload` (foundation) -> `test_sync_helper_calls_refresh_then_compile` -> `test_sync_request_returns_will_compile` -> remaining.

### P3: Chat C# EditMode tests (unity-plugin/Editor/Tests/ResumeGateTests.cs)

| Test | Mocks | Verifies |
|------|-------|----------|
| `test_resume_gated_on_compiling` | SyncHelper.Ops with IsCompiling=true | TryResumePendingTurn does NOT dispatch; reschedules |
| `test_drain_uses_shared_sync_primitive` | Verify _needsRefresh replaced with SyncHelper call | No direct AssetDatabase.Refresh in Drain |
| `test_reloadguard_interplay` | ReloadGuard.ResetForTest() seam | Lock+Disallow paired; unlock+Allow paired |
| `test_compile_errors_surface_to_resumed_turn` | SyncHelper.OnSyncFailed subscription | Error surfaces in resumed turn UI |

### P4: Live tests (server/tests/live/test_sync.py, `pytest -m live`)

| Test | What it does |
|------|-------------|
| `test_live_sync_full_cycle` | Write new .cs -> sync_unity -> verify type visible via get_component |
| `test_live_sync_compile_error_then_fix` | Write bad .cs -> sync_unity -> get errors -> fix -> sync_unity -> clean |
| `test_live_reconnect_transparent` | sync_unity that triggers reload -> verify reconnect happened |
| `test_live_noop_sync_fast` | sync_unity with no changes -> assert <5s |
| `test_live_plugin_bump_re_resolve` | Touch plugin .cs -> sync_unity(bump=true) -> get_version -> verify new version |
| `test_live_dll_freshness_ground_truth` | After sync -> editor_log.check_dll_freshness -> True |

### TDD Development Order (for developer)

1. SyncHelper.cs with ISyncOps seam + epoch in SessionState (P2 foundation tests)
2. UnitySyncOps.cs (thin wrapper, no tests needed -- delegation only)
3. CommandRouter registration of sync/sync_status
4. tools/sync.py with _make_send pattern (P1 tests -- RED first)
5. MCPServer.cs false-ready fix (move "ready" to afterAssemblyReload path)
6. CompileNotifier.cs fail discriminator
7. Drain.cs resume gate + Refresh debounce (P3 tests)
8. ReloadGuard.cs safe pattern (P3 interplay test)
9. bump_version.py + integration into sync_unity
10. Live tests last (P4)

## 7. Risks and Open Questions

| Risk | Mitigation | Spike? |
|------|-----------|--------|
| `Client.Resolve()` is fire-and-forget void; registeredPackages fires AFTER reload (ref-S4) -- Python can't distinguish "Resolve done, no changes" from "Resolve pending" | Treat reconnect as the confirm signal; Resolve is best-effort pre-Refresh. If no registeredPackages fires, Refresh alone handles .cs content. | Spike on 6000.3.0b7: call Resolve then Refresh, verify compile+reload happen |
| `afterAssemblyReload` ordering vs `[DidReloadScripts]` (ref-S3 C2: community-only) | SyncHelper uses `afterAssemblyReload` (docs-backed after InitializeOnLoad). Never depends on DidReloadScripts position. | Empirical test ref-S15 #7 if chat ever needs DidReloadScripts |
| IN-93874: CleanBuildCache may no-op on Unity 6.0 (ref-S2) | Default path uses Refresh, not RequestCompilation. CleanBuildCache only used with force=true; verify assemblyCompilationFinished fired. | Spike ref-S15 #3 on 6000.3.0b7 |
| ISyncOps seam must be public (Tests.dll CS0122) | Interface + impl both in `UnityMCP.Editor` namespace, public access. `[assembly: InternalsVisibleTo]` already set for TestProject but insufficient for interface-based mocking. | None -- public by design |
| `File.Move(overwrite: true)` requires .NET Core; Unity 2021 Mono uses .NET Framework | Use conditional: `#if NET5_0_OR_GREATER` File.Move(overwrite:true) `#else` Delete+Move with try/catch | None -- known pattern |
| SyncHelper static state: multiple concurrent sync calls | Epoch is monotonic; second sync while first is in-flight just increments epoch. Python's epoch-match gate means it tracks the LATEST sync only. Prior sync callers see epoch mismatch and either timeout or re-sync. Acceptable: concurrent syncs are a user error. | None |
| ReloadGuard SessionState rebalance: what key to use, avoid clash | Use `"MCP_ReloadLockDepth"` in SessionState. InitializeOnLoad reads it; if >0, calls UnlockReloadAssemblies that many times + AllowAutoRefresh. Then clears. | None |
| Chat subscribes to SyncHelper.OnSyncComplete but event handlers die with domain | Re-subscribe in MCPChatWindow.CreateGUI or OnEnable (EditorWindow lifecycle survives reload via serialization). ChatProcess.TriggerResume already fires from afterAssemblyReload -- chain SyncHelper.OnSyncComplete there. | None |
