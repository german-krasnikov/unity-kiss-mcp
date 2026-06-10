# ARCH-3: Chat-First / UX-Reliability Perspective

Perspective: the primary consumer of `sync_unity` is the in-Unity chat (agent edits .cs, needs reliable refresh+reload+resume without hangs or phantom errors). MCP tool `sync_unity` is the second consumer of the same primitive.

## 1. Decisions D1--D10

**D1 Tool surface.** New `sync_unity` MCP tool; `recompile` becomes a thin alias (`sync_unity` with `resolve=false`). `await_compile` stays but internally delegates to the shared Python wait-loop. Rationale: `recompile` callers expect "fire and confirm"; `sync_unity` adds version-bump + resolve + both-signals. Alias avoids breaking existing prompts.

**D2 Python/C# orchestration split.** C# provides a NEW `sync` command handler: `Refresh()` (+ optional `Client.Resolve()`), returns `"sync_ack|epoch=N"` immediately. The wait-loop lives entirely in Python (polls `compile_status` + state-file + reconnect). Chat (pure C#) uses a different mechanism: event-driven `SyncHelper` that subscribes to `compilationFinished` / `afterAssemblyReload` and signals via a `SessionState`-persisted flag (no polling). Both share the same epoch token and state-file contract.

**D3 Trigger semantics.** `sync` command = `AssetDatabase.Refresh()` unconditionally (works unfocused, ignores prefs per ref:SS5). If `resolve=true`, prepend `Client.Resolve()` (ref:SS4 issue 1248326). `RequestScriptCompilation` is NOT called -- `Refresh()` alone imports+compiles external edits (ref:SS2); `Request(None)` is a no-op when no scripts are dirty, `CleanBuildCache` is too expensive and has IN-93874 risk.

**D4 Client.Resolve placement.** Called BEFORE `Refresh()` inside the same `sync` handler when `resolve=true`. Version-bump changes `package.json` which is metadata, so Resolve is needed (ref:SS4). For the common case (agent edits .cs inside the plugin), `resolve=false` is correct -- `Refresh()` scans `Packages/` mount and picks up .cs changes in `file:` packages without Resolve (ref:SS4 line "Refresh scans BOTH Assets/ and Packages/").

**D5 Version-bump owner.** Python-side `scripts/bump_version.py` (or inline in `tools/sync.py`). Reason: bump must happen BEFORE the TCP `sync` command (the file write must be flushed to disk first). Python reads `unity-plugin/package.json`, increments patch, writes atomically (tmp+rename). C# never self-modifies. Bump is conditional: only when `resolve=true` AND version hasn't already been bumped this session.

**D6 Chat wait mechanism.** Event-driven, NOT polling. Rationale: ref:SS3 proves polling `isCompiling`/`isUpdating` is provably racy (3 false-gap mechanisms). Chat lives in C# -- it HAS access to events.

Design: `SyncHelper` (new static class, `[InitializeOnLoad]`) subscribes to `compilationStarted`, `compilationFinished`, `afterAssemblyReload`. Exposes `SyncHelper.IsCompileClean` (bool, SessionState-backed, survives reload). State machine: `Idle -> Compiling (compilationStarted) -> CompileDone (compilationFinished, check scriptCompilationFailed) -> [if OK] Reloading (beforeAssemblyReload) -> Ready (afterAssemblyReload, new domain) | [if FAIL] Error (terminal)`.

Chat's `TryResumePendingTurn` gates on `SyncHelper.IsCompileClean` instead of raw `isCompiling`. If not clean, schedules `EditorApplication.delayCall` retry (bounded, max 30 retries ~ 30s).

**D7 Compile-error policy.** Report, never rollback. Errors from `assemblyCompilationFinished` are aggregated into `CompileErrorCapture` (existing). Chat surfaces them via `InjectCompileErrors` (existing path). Python `sync_unity` returns errors verbatim. No rollback -- the agent/LLM decides what to do.

**D8 Idempotency detection.** Python checks state-file timestamp + epoch before sending `sync`. If state is already "ready" with epoch >= requested, returns immediately (<100ms). C# side: if `!EditorApplication.isCompiling && !EditorApplication.isUpdating`, `Refresh()` is still called (it's a no-op when nothing changed, ref:SS1), but returns `sync_ack|epoch=N|noop=true`. Python sees `noop=true` and skips the wait-loop. R5 satisfied: no-op < 5s.

**D9 Epoch protocol.** Monotonic int stored in `SessionState` ("MCP_SyncEpoch"). Incremented by `sync` command handler. Persisted to state-file alongside state string: `"compiling|epoch=7"`, `"ready|epoch=7"`. Python sends epoch in `sync` args, receives it back, then only accepts state-file "ready" with matching epoch. Closes the race: stale "ready" from a previous cycle can't satisfy a new sync request.

**D10 Focus-dependency.** `open -a Unity` / `osascript activate` is DEAD. Scripted `Refresh()` is unconditional on all 3 OSes (ref:SS5, explicit: "The Asset Database always performs a refresh if AssetDatabase.Refresh is called, regardless of this method"). No focus hack needed. The `sync` command dispatches to main thread via the existing `_mainThreadQueue` in `MCPServer.cs`, which ticks on `EditorApplication.update` -- unfocused editor still ticks this loop.

## 2. Architecture

### Components

```
SyncHelper.cs (NEW, ~120 lines)
  - [InitializeOnLoad], static
  - State machine: Idle/Compiling/CompileDone/Reloading/Ready/Error
  - SessionState keys: MCP_SyncEpoch, MCP_SyncState, MCP_SyncErrors
  - Events: compilationStarted/compilationFinished/beforeAssemblyReload/afterAssemblyReload
  - Public API: TriggerSync(resolve) -> epoch; IsCompileClean; LastErrors; CurrentEpoch
  - Injectable seam: Func<bool> IsCompiling (default: EditorApplication.isCompiling)

CommandRouter.cs:267 (MODIFIED)
  - `sync` command replaces `recompile`
  - Calls SyncHelper.TriggerSync(resolve), returns "sync_ack|epoch=N[|noop=true]"

MCPServer.cs:80-88 (MODIFIED)
  - Remove delayCall "ready" write from compilationFinished
  - "ready" is written ONLY from SyncHelper post-reload path (afterAssemblyReload)

CompileNotifier.cs (MODIFIED, minor)
  - Add fail marker: "idle-failed|dur" when scriptCompilationFailed is true

tools/sync.py (NEW, ~100 lines)
  - sync_unity tool: optional bump -> send "sync" cmd -> wait-loop (poll compile_status
    + state-file epoch match + reconnect on DomainReloadError) -> return errors or clean

tools/code_intel.py:60-107 (MODIFIED)
  - await_compile delegates to shared wait-loop from sync.py

MCPChatWindow.Drain.cs:61-131 (MODIFIED)
  - TryResumePendingTurn: gate on SyncHelper.IsCompileClean
  - If not clean: delayCall retry with bounded counter

MCPChatWindow.Drain.cs:164-167 (MODIFIED)
  - _needsRefresh -> debounced: accumulate flag, single Refresh at turn-end (TurnDone)
  - Moves from mid-streaming to HandleEvent(TurnDone) AFTER ReloadGuard.OnTurnFinished()

ReloadGuard.cs (MODIFIED)
  - Add DisallowAutoRefresh/AllowAutoRefresh paired with Lock/Unlock (ref:SS6 safe pattern)
  - Add SessionState marker "MCP_ReloadLocked" for cross-reload rebalance
  - [InitializeOnLoad] in SyncHelper checks marker and calls ForceUnlock if orphaned
```

### Chat end-to-end scenario: agent edits .cs -> compile -> reload -> resume

```
1. Agent (claude -p) calls MCP tool "Write" -> modifies Assets/Health.cs
2. ChatProcess stdout -> DrainAndRender -> HandleToolRecord -> _needsRefresh = true
   (DEBOUNCED: flag set, NOT acted on mid-stream)
3. More tool calls may arrive in same turn (batch edits)
4. TurnDone event arrives -> HandleEvent(TurnDone):
   a. ReloadGuard.OnTurnFinished() -> UnlockReloadAssemblies + AllowAutoRefresh
   b. _needsRefresh == true -> SyncHelper.TriggerSync(resolve=false)
      -> AssetDatabase.Refresh() -> compile starts async
   c. SyncHelper state: Idle -> Compiling (compilationStarted fires)
5. compilationFinished fires (old domain):
   a. SyncHelper checks scriptCompilationFailed
   b. SUCCESS: state -> CompileDone, errors="" 
   c. FAIL: state -> Error, errors captured, NO reload coming
      -> CompileAutoFix.OnErrorsDetected if chat has retries left
6. [SUCCESS path] beforeAssemblyReload fires:
   a. SyncHelper writes SessionState "MCP_SyncState=Reloading"
   b. MCPServer.OnBeforeReload: going_away, state-file "reloading|epoch=N"
   c. ChatProcess.KillOrphan: kill claude -p, save PendingTurnState
   d. ReloadGuard: SavePendingState (turn already done, so no in-flight state)
7. [Domain reload -- managed state dies, native counters survive]
8. New domain: [InitializeOnLoad] fires:
   a. SyncHelper: read SessionState, if state was Reloading -> set Ready
   b. SyncHelper: check orphaned ReloadGuard lock marker -> rebalance
   c. MCPServer.StartAsync (via delayCall): bind listener, state-file "ready|epoch=N"
   d. ChatProcess.TriggerResume -> MCPChatWindow.TryResumePendingTurn
9. TryResumePendingTurn:
   a. CHECK: SyncHelper.IsCompileClean? 
      - YES -> proceed to resume (restore chips, re-send turn)
      - NO -> schedule delayCall retry (max 30)
   b. On resume: ReloadGuard.OnTurnStarted (for the new resumed turn)
```

### Mapping to ref:SS13 handshake + closing 5 known races

| SS13 Race | How closed |
|-----------|-----------|
| isCompiling false-gap | Never polled. SyncHelper uses events only. Chat gates on `SyncHelper.IsCompileClean` (SessionState, set by event handlers). Python polls `compile_status` but also requires state-file epoch match AND reconnect. |
| Failed-compile-no-reload | SyncHelper.compilationFinished checks `scriptCompilationFailed`. State -> Error, no reload wait. Python checks error response before entering reconnect-wait. |
| Epoch staleness | Monotonic epoch in SessionState. State-file includes epoch. Python matches epoch before accepting "ready". |
| False-"ready" window (MCPServer:80-88) | FIXED: remove delayCall "ready" write from compilationFinished. "ready" written only from post-reload SyncHelper (afterAssemblyReload path) or MCPServer.StartAsync (no-compile case). |
| Unbounded reload hang | Python: 60s default timeout. Chat: 30 delayCall retries (~30s). Both report timeout, never infinite wait. |

### Chat-specific races (additional)

| Race | Mitigation |
|------|-----------|
| Resume fires during second compile (double-import) | Gate on SyncHelper.IsCompileClean; if compiling, retry via delayCall |
| _needsRefresh mid-turn phantom errors | Debounced: Refresh only at TurnDone after ReloadGuard unlock |
| ReloadGuard native lock orphaned across reload | SessionState marker + InitializeOnLoad rebalance |
| claude -p killed mid-stream, turn state lost | PendingTurnState v5 saves before reload; orphan-kill is symmetric |

## 3. Files

| File | Change | Lines |
|------|--------|-------|
| `unity-plugin/Editor/SyncHelper.cs` | NEW: state machine, epoch, event subscriptions, IsCompileClean, injectable seam | ~120 |
| `unity-plugin/Editor/CommandRouter.cs:267` | Replace `recompile` lambda with `sync` handler calling SyncHelper.TriggerSync | ~10 delta |
| `unity-plugin/Editor/MCPServer.cs:80-88` | Remove delayCall "ready" from compilationFinished; defer to SyncHelper | ~8 delta |
| `unity-plugin/Editor/MCPServer.cs:130` | `#if UNITY_EDITOR_WIN ExclusiveAddressUse=true` | ~3 delta |
| `unity-plugin/Editor/MCPServer.cs:502-505` | Atomic state-file write: `File.Move(tmp, path, overwrite: true)` on .NET 4.x+ | ~2 delta |
| `unity-plugin/Editor/CompileNotifier.cs:18-24` | Add fail discriminator: check `scriptCompilationFailed` -> "idle-failed\|dur" | ~5 delta |
| `unity-plugin/Editor/Chat/MCPChatWindow.Drain.cs:61-80` | Gate TryResumePendingTurn on SyncHelper.IsCompileClean, delayCall retry | ~15 delta |
| `unity-plugin/Editor/Chat/MCPChatWindow.Drain.cs:164-177` | Move Refresh from mid-stream to TurnDone; accumulate _needsRefresh, act at end | ~10 delta |
| `unity-plugin/Editor/Chat/ReloadGuard.cs:28-50` | Add DisallowAutoRefresh/AllowAutoRefresh; SessionState lock marker | ~20 delta |
| `unity-plugin/Editor/Chat/ChatProcess.cs:34-35` | Fix misleading comment about reload guard recovery | ~2 delta |
| `server/src/unity_mcp/tools/sync.py` | NEW: sync_unity tool, shared wait-loop with epoch+both-signals | ~100 |
| `server/src/unity_mcp/tools/code_intel.py:60-107` | Refactor await_compile to delegate to sync.py wait-loop | ~20 delta |
| `server/src/unity_mcp/tools/scene.py:126` | Fix docstring; optionally chain sync internally | ~5 delta |
| `scripts/bump_version.py` | NEW: atomic patch-bump of package.json | ~40 |

## 4. SS14 Coverage

| SS14 Row | File:line | Fixes? | Note |
|----------|-----------|--------|------|
| CommandRouter.cs:267 bare Refresh | YES | `sync` returns epoch, no false "ok" |
| scene.py:126 false docstring | YES | Fix docstring, chain sync |
| code_intel.py:60-107 false-idle | YES | Delegates to epoch+both-signals wait |
| CompileNotifier.cs:18-24 no fail marker | YES | "idle-failed" discriminator |
| MCPServer.cs:80-88 false-ready | YES | Remove delayCall "ready"; post-reload only |
| MCPServer.cs:130 ReuseAddress Windows | YES | ExclusiveAddressUse |
| MCPServer.cs:474-485 OnBeforeReload | OK | No change needed (confirmed correct) |
| MCPServer.cs:502-505 non-atomic write | YES | File.Move overwrite |
| Drain.cs:61-131 no isCompiling gate | YES | SyncHelper.IsCompileClean gate |
| Drain.cs:164-167 mid-turn Refresh | YES | Debounced to TurnDone |
| ReloadGuard.cs:31,50 no DisallowAutoRefresh | YES | Safe pattern + SessionState marker |
| ChatProcess.cs:34-35 false comment | YES | Fix comment |
| bridge_heartbeat.py:39-54 flat interval | NO | Low risk on loopback; optional future improvement |
| editor_log_parser.py:22-46 -logFile gap | NO | Doc-level; detection is nice-to-have, not blocking |

12 of 14 fixed. 2 deferred (low risk, non-blocking).

## 5. R23 Cross-Platform

Per-OS branches introduced:

| Location | What | macOS | Windows | Linux |
|----------|------|-------|---------|-------|
| `MCPServer.cs:130` | Socket option | ReuseAddress (existing) | ExclusiveAddressUse=true | ReuseAddress (existing) |
| `MCPServer.cs:504` | Atomic write | File.Move (POSIX rename) | File.Move(overwrite:true) or File.Replace | File.Move (POSIX rename) |
| `SyncHelper.cs` | None | Identical | Identical | Identical |
| `tools/sync.py` | None | Identical | Identical | Identical |

The sync core is OS-independent (in-process TCP, no focus hacks). Only MCPServer infrastructure has per-OS branches. Editor.log paths already handled correctly in `editor_log_parser.py`.

## 6. Test Plan

### TDD Order

1. P2 C# EditMode: SyncHelper state machine (most foundational)
2. P3 C# EditMode: Chat resume gate + debounced refresh
3. P1 Python unit: sync_unity wait-loop
4. P4 Live: full-cycle integration

### P1 -- Python unit tests (mock `_send`)

File: `server/tests/test_sync.py` (~15 tests)

- `test_sync_ack_epoch_returned`: mock send returns "sync_ack|epoch=1" -> wait-loop starts
- `test_both_signals_required`: compile_status="idle" but state-file="compiling|epoch=1" -> NOT clean
- `test_stale_epoch_rejected`: state-file "ready|epoch=0" when request epoch=1 -> keep waiting
- `test_compile_errors_returned_verbatim`: compile_status="idle-failed" -> get_compile_errors -> return errors
- `test_reconnect_after_domain_reload`: DomainReloadError mid-poll -> reconnect -> resume poll -> clean
- `test_timeout_returns_partial`: 60s elapsed -> return timeout message with last known state
- `test_noop_fast_path`: sync_ack has noop=true + state-file ready -> return immediately (< 1s)
- `test_idempotent_no_bump`: second sync without file changes -> no bump, still works
- `test_bump_atomic`: verify tmp+rename pattern, verify version incremented

Seam: `_make_send` from existing `test_await_compile.py` pattern; `_mock_state_file` helper.

### P2 -- C# EditMode: SyncHelper + trigger

File: `unity-plugin/Editor/Tests/SyncHelperTests.cs` (~12 tests)

Injectable seam: `SyncHelper.OverrideForTest(Func<bool> isCompiling, Func<bool> isUpdating, Func<bool> scriptFailed)`.

- `test_trigger_sync_returns_epoch`: TriggerSync -> epoch > 0
- `test_epoch_survives_reload`: set epoch in SessionState -> simulate [InitializeOnLoad] -> read same epoch
- `test_compilation_started_sets_compiling`: invoke compilationStarted handler -> IsCompileClean == false
- `test_compilation_finished_success_no_reload_yet`: compilationFinished + !scriptFailed -> state=CompileDone, IsCompileClean still false (reload pending)
- `test_after_reload_sets_ready`: simulate afterAssemblyReload path -> IsCompileClean == true
- `test_compilation_failed_terminal`: compilationFinished + scriptFailed=true -> state=Error, LastErrors non-empty
- `test_state_file_epoch_matches`: after TriggerSync -> read state file -> contains epoch
- `test_orphaned_lock_rebalanced`: set SessionState marker -> call InitializeOnLoad -> lock cleared
- `test_sync_command_registered`: verify "sync" in CommandRegistry
- `test_recompile_alias_works`: "recompile" -> same as sync with resolve=false

ResetForTest: `SyncHelper.ResetForTest()` clears SessionState keys + state.

### P3 -- Chat EditMode tests (DETAILED)

File: `unity-plugin/Editor/Chat/Tests/ResumeGateTests.cs` (~10 tests)

Seam: Extract `TryResumePendingTurn`'s compile-clean check into a testable gate: `internal static bool CanResumeAfterReload(Func<bool> isClean)`. Inject `SyncHelper.IsCompileClean` in production, test-supplied lambda in tests.

- `test_resume_blocked_while_compiling`: inject isClean=false -> TryResumePendingTurn returns without dispatching, schedules retry
- `test_resume_proceeds_when_clean`: inject isClean=true -> TryResumePendingTurn dispatches turn
- `test_resume_retry_bounded`: isClean stays false for 31 calls -> gives up, surfaces error
- `test_resume_retry_succeeds_on_3rd`: isClean false,false,true -> dispatches on 3rd

File: `unity-plugin/Editor/Chat/Tests/DebouncedRefreshTests.cs` (~6 tests)

- `test_needsRefresh_not_acted_mid_stream`: set _needsRefresh during DrainAndRender -> verify no Refresh call (mock via SyncHelper.TriggerSync counter)
- `test_needsRefresh_acted_at_turnDone`: TurnDone event + _needsRefresh -> SyncHelper.TriggerSync called
- `test_multiple_edits_single_refresh`: 3 code-edit results -> only 1 Refresh at TurnDone
- `test_refresh_after_unlock`: verify Refresh happens AFTER ReloadGuard.OnTurnFinished

File: `unity-plugin/Editor/Chat/Tests/ReloadGuardSafePatternTests.cs` (~5 tests)

- `test_lock_pairs_disallow_with_lock`: OnTurnStarted -> both Lock + Disallow called (via counter or SessionState marker)
- `test_unlock_pairs_allow_with_unlock`: OnTurnFinished -> both Unlock + Allow called
- `test_sessionstate_marker_set_on_lock`: OnTurnStarted -> SessionState "MCP_ReloadLocked" == "1"
- `test_sessionstate_marker_cleared_on_unlock`: OnTurnFinished -> SessionState "MCP_ReloadLocked" == ""
- `test_orphan_recovery_clears_marker`: set marker manually -> simulate InitializeOnLoad -> marker cleared + ForceUnlock called

### P4 -- Live tests (`pytest -m live`)

File: `server/tests/test_sync_live.py` (~6 tests, always last)

- `test_live_sync_full_cycle`: write new .cs file -> sync_unity -> type visible in get_compile_errors (clean)
- `test_live_sync_compile_error_then_fix`: write broken .cs -> sync_unity -> errors returned -> fix .cs -> sync_unity -> clean
- `test_live_reconnect_transparent`: sync_unity triggers reload -> bridge reconnects -> next command works
- `test_live_noop_fast`: sync_unity twice with no changes -> second < 5s
- `test_live_plugin_bump_re_resolve`: touch plugin .cs -> sync_unity(resolve=true, bump=true) -> version-echo returns bumped version
- `test_live_dll_freshness`: after sync -> check_dll_freshness returns True

### Manual residue

- Chat UX: "compiling..." indicator during resume delay (visual, not testable in EditMode)
- Unfocused-refresh confirmation on Windows/Linux (CI or manual)
- PlayMode interaction (queue refresh until play exits)
- Cross-Unity-version smoke (2021.3 LTS, 2022.3 LTS, 6000.3)

## 7. Risks and Open Questions

| Risk | Mitigation | Spike needed? |
|------|-----------|---------------|
| `File.Move(overwrite:true)` unavailable on .NET Framework 4.x (Unity 2021) | Check API availability; fallback to Delete+Move with retry | YES: verify on 2021.3 |
| `Client.Resolve()` is fire-and-forget void; no success callback before reload kills handler | registeredPackages fires post-reload in [InitializeOnLoadMethod]; but handler dies with domain. Use SessionState flag set in registeredPackages handler registered in InitializeOnLoad. | YES: spike on 6000.3.0b7 |
| IN-93874: CleanBuildCache no-op on Unity 6 | We don't use CleanBuildCache (D3 decision). No risk. | NO |
| SyncHelper [InitializeOnLoad] ordering vs MCPServer [InitializeOnLoad] | Both are [InitializeOnLoad]; ordering not guaranteed. SyncHelper must NOT depend on MCPServer being ready. State-file writes go through SyncHelper, MCPServer reads on StartAsync. | Verify empirically |
| ReloadGuard adding DisallowAutoRefresh may interfere with explicit Refresh() | Per ref:SS5: "Refresh is called regardless of this method". DisallowAutoRefresh only blocks AUTO refresh (focus-triggered). Our explicit Refresh() in TurnDone is unaffected. Safe. | NO |
| 30 delayCall retries in resume gate: if each fires at ~16ms (editor tick), that's only ~0.5s, not 30s | Use `EditorApplication.update` with time-based check instead of delayCall count. Check `timeSinceStartup` delta > 1s between retries. | Design decision |
| State file atomicity: POSIX rename is atomic, but File.Delete+File.Move is not on Windows | Use try/catch around read, retry once on IOException. State file is advisory (both-signals gate has TCP reconnect as primary). | NO |
| Chat process orphan-kill: KillOrphan happens in beforeAssemblyReload, before SyncHelper can set state | Correct: KillOrphan kills the PREVIOUS turn's process. The sync-triggered reload is for the NEXT turn. No conflict. | NO |

### Spikes for 6000.3.0b7

1. `File.Move(src, dst, overwrite: true)` -- does this overload exist in Unity's .NET profile?
2. `Client.Resolve()` -> `registeredPackages` event -> does it fire after Refresh+compile+reload? Can we observe it in InitializeOnLoad-registered handler?
3. [InitializeOnLoad] ordering between SyncHelper and MCPServer -- log execution order.
4. SS15 #5: end-to-end handshake smoke on beta.
