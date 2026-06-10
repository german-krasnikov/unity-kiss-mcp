# ARCH-1: Minimal-Diff / Maximum-Reuse Architecture

Perspective: minimum new code, maximum reuse of existing mechanisms.

## 1. Decisions D1-D10

### D1: Tool surface — new `sync_unity` tool, NOT expand `recompile`

New Python tool `sync_unity` in existing `tools/code_intel.py` (co-located with `await_compile`).
`recompile` C# command stays but returns richer status (epoch). `await_compile` becomes the
internal engine of `sync_unity` after enhancement. Rationale: `recompile` already registered in
CommandRouter:267 and gating tables; adding a new C# command `sync` alongside it minimizes
blast radius vs mutating `recompile` semantics that LLM tools depend on. `scene.py:125`
`recompile()` wrapper gets deprecation docstring pointing to `sync_unity`.

### D2: Python/C# split — C# triggers + persists, Python orchestrates + waits

Hard constraint (plan file): C# handler is synchronous. Design:
- C# `sync` command: `Refresh()` (+ `Client.Resolve()` if `resolve=true` arg), returns
  `"sync_started|epoch={N}"` or `"already_compiling|epoch={N}"` or `"idle_no_changes"`.
  Epoch = `SessionState.SetInt("MCP_SyncEpoch", N)` — survives reload (ref: SS scope correct per
  SS docs HIGH).
- Python `sync_unity` tool: sends `sync` -> polls `sync_status` (enhanced `compile_status`) ->
  handles DomainReloadError -> declares done after both-signals gate.
- No new wait primitive in C# -- reuse the poll pattern from `await_compile` with epoch awareness.

### D3: Trigger semantics — `Refresh()` always, `RequestScriptCompilation` never

Per ref SS1: scripted `Refresh()` imports AND queues compile for dirty scripts. Per SS2:
`RequestScriptCompilation(None)` is no-op without dirty scripts; `CleanBuildCache` has IN-93874
bug on U6000. Per SS12 decision table: for external edits, "Refresh() alone imports AND queues
compilation; Request adds nothing." Our trigger: `AssetDatabase.Refresh()` only. This matches
current `recompile` (CommandRouter:267) -- zero change to trigger logic.

### D4: `Client.Resolve()` — conditional, arg-driven, no spike needed

Per ref SS4 issue 1248326 (By Design): `Refresh()` alone does NOT update package registration.
"If scope unknown, call both." Design: `sync` command accepts `resolve` bool arg (default false).
Python `sync_unity` passes `resolve=true` when bump happened. No spike needed -- the official
guidance is unambiguous. `Client.Resolve()` is fire-and-forget void; results via
`registeredPackages` (fires after reload), but we already wait for reload via reconnect so this
is free.

### D5: Version bump owner — Python `scripts/bump_version.py`

Python owns the bump because: (a) it knows WHAT changed (file writes happen Python-side),
(b) `package.json` is outside Unity process (no self-modify race), (c) atomic tmp-rename is
trivial in Python, (d) C# self-modifying its own package.json triggers the very reload we're
trying to control -- chicken-egg. The bump is a simple `json.load` -> increment patch ->
`json.dump` with `os.replace()` (atomic on POSIX, near-atomic on Windows). R17 satisfied.

### D6: Chat wait mechanism — poll via `EditorApplication.update`, NOT events

Chat's `TryResumePendingTurn` already fires from `afterAssemblyReload` (ChatProcess:25,38).
The gap (plan R15): no `isCompiling/isUpdating` gate. Fix: add 2-line gate at Drain.cs:63
`if (EditorApplication.isCompiling || EditorApplication.isUpdating) { delayCall += TryResume; return; }`.
This reuses the existing resume path, no new SyncHelper primitive in C#. The "shared primitive"
(R14) is the enhanced `sync` command + `sync_status` -- chat calls Refresh directly (already does
via `_needsRefresh`), and the resume gate is the isCompiling/isUpdating check. Both sides
converge on the same state-file + epoch mechanism.

### D7: Compile error policy — report, never rollback

Terminal on error (ref SS9: no reload on failure, old assemblies live). `sync_unity` returns
verbatim errors via `get_compile_errors` + editor_log corroboration. No rollback -- that's
the LLM agent's job. Matches current `await_compile` behavior.

### D8: Idempotency detection — state-file + compile_status fast-path

If `compile_status` returns `idle|X` AND state file says `ready` AND no pending epoch mismatch,
`sync_unity` returns `"no changes (Xs)"` in <5s (R5). The `sync` C# command itself detects
"no dirty files" via: if `!EditorApplication.isCompiling` after `Refresh()` returns AND no
`compilationStarted` fires within one `delayCall` tick, return `"idle_no_changes"`.

### D9: Epoch/token protocol

New C#: `SyncEpochTracker` (tiny static class, ~30 lines). Increments epoch in `sync` command,
persists in `SessionState` (survives reload -- ref SS3: correct scope). Enhanced `compile_status`
becomes `sync_status`: returns `"compiling|dur|epoch=N"` or `"idle|dur|epoch=N"` or
`"failed|dur|epoch=N"`. Python side: remembers expected epoch, only declares done when
epoch matches (closes race R-1: stale "ready" from previous cycle). Text format, pipe-delimited.

### D10: Focus dependency — `osascript`/`open -a Unity` DIES completely

Per ref SS5: scripted `Refresh()` is unconditional on prefs/focus/DisallowAutoRefresh. The
unfocused editor ticks enough to service it (Rider proof, SS5). Our `sync` command runs
`Refresh()` in-process via TCP dispatch to main thread. Focus is irrelevant. Remove `osascript`
recipe from CLAUDE.md (doc-keeper scope). `open -a Unity` only survives as "make Unity visible"
UX convenience, never as a refresh mechanism.

## 2. Architecture

### Components (all existing files, minimal new)

```
Python side (server/src/unity_mcp/):
  tools/code_intel.py    # sync_unity() + enhanced await_compile() — ~40 new lines
  tools/scene.py:125     # recompile() deprecation docstring — 1 line
  editor_log.py          # unchanged (corroboration already works)
  compile_state.py       # unchanged (state probe already reads state file)
  unity_state.py         # add epoch field to UnityState — ~5 lines
  scripts/bump_version.py  # NEW ~40 lines — atomic patch bump

C# side (unity-plugin/Editor/):
  SyncEpochTracker.cs    # NEW ~35 lines — epoch counter + SessionState + sync_status
  CommandRouter.cs:267   # replace recompile handler, add sync + sync_status commands
  CompileNotifier.cs     # add fail/epoch fields — ~10 lines delta
  MCPServer.cs:80-88     # fix false-ready race — move success "ready" to post-reload path
  MCPServer.cs:130       # Windows ExclusiveAddressUse — 3 lines
  MCPServer.cs:502-505   # atomic state-file write — 2 lines
  MCPServer.cs:285-288   # add sync_status to fast-path list
  Chat/MCPChatWindow.Drain.cs:63  # isCompiling gate on resume — 3 lines
  Chat/MCPChatWindow.Drain.cs:174 # debounce _needsRefresh to turn-end — move existing code
  Chat/ReloadGuard.cs:31,50       # add DisallowAutoRefresh + SessionState marker — ~15 lines
```

### Sequence (mapped to SS13 handshake)

```
Python: bump_version.py (if plugin changed) → writes package.json atomically
  ↓
Python: sync_unity tool → bridge.send("sync", {resolve: bumped?})
  ↓
C# sync handler (main thread, CommandRouter):
  1. epoch = SyncEpochTracker.Increment()           [SS13 step 2]
  2. if resolve: Client.Resolve()                    [SS13 step 2, SS4/SS12]
  3. AssetDatabase.Refresh()                         [SS13 step 2, SS1/SS5]
  4. return "sync_started|epoch={epoch}"
  ↓
C# compilationStarted → state file "compiling"       [SS13 step 3, MCPServer:79]
  ↓
C# compilationFinished:                               [SS13 step 4]
  if scriptCompilationFailed → state "compile_failed|epoch=N"  [SS9 discriminator]
  if OK → (DO NOT write "ready" here — SS13 race R-4)
  ↓
C# beforeAssemblyReload → going_away + "reloading"   [SS13 step 5, MCPServer:474]
  ↓
[domain swap]                                          [SS13 step 6]
  ↓
C# [InitializeOnLoad] → rebind listener →             [SS13 step 7]
  afterAssemblyReload → WriteStateFile("ready")
  SyncEpochTracker reads epoch from SessionState (survived reload)
  ↓
Python: DomainReloadError caught → sleep → reconnect  [SS13 step 7-8]
  poll sync_status → "idle|dur|epoch=N"
  corroborate via editor_log (both-signals R2)         [SS13 step 8]
  epoch matches expected → declare synced              [SS13 step 9]
```

### How the 5 known races close

**R-1 (isCompiling false-gap):** Never trust `isCompiling` alone. Epoch in SessionState +
sync_status response. Python waits for epoch match, not flag transition.

**R-2 (failed-compile-no-reload):** `sync_status` returns `"failed|dur|epoch=N"` -- Python
detects failure immediately, never enters reconnect-wait loop. CompileNotifier enhanced with
`scriptCompilationFailed` check (SS9 discriminator).

**R-3 (stale "ready"):** Fix MCPServer:80-88. Move success-path `WriteStateFile("ready")` from
`compilationFinished+delayCall` to after-reload path only (MCPServer static ctor's
`afterAssemblyReload` / `StartAsync` already writes "ready" at :146). Delete the delayCall
block entirely.

**R-4 (false-"ready" window):** Same fix as R-3. The `compilationFinished` handler no longer
writes "ready" -- it can only write "compile_failed" on error. "ready" comes exclusively from
post-reload `StartAsync:146`.

**R-5 (unbounded reload hang):** Python-side timeout in `sync_unity` (default 120s, matching
SESSION_TIMEOUT). State-probe `is_process_dead()` for fast fail. Editor.log markers as
secondary watchdog signal.

## 3. Files -- exact changes

| File | Change | Lines delta |
|------|--------|-------------|
| `server/src/unity_mcp/tools/code_intel.py` | Add `sync_unity()` tool (~35 lines), enhance `await_compile` with epoch param (~10 lines), register sync_unity | +45 |
| `server/src/unity_mcp/tools/scene.py:125-127` | Deprecation docstring on `recompile()` | +2 |
| `server/src/unity_mcp/unity_state.py:17-18,30-38` | Add `epoch` field to `UnityState`, parse 4th line if present | +8 |
| `server/scripts/bump_version.py` | NEW: atomic patch bump of package.json | +40 |
| `unity-plugin/Editor/SyncEpochTracker.cs` | NEW: epoch counter + GetSyncStatus() | +35 |
| `unity-plugin/Editor/CommandRouter.cs:267` | Replace bare `Refresh()` with sync handler; add `sync_status` registration | +15, -1 |
| `unity-plugin/Editor/CommandRouter.cs:421-424` | Add `sync_status` to `IsAllowedDuringCompile` list | +1 |
| `unity-plugin/Editor/CompileNotifier.cs:18-24` | Add `scriptCompilationFailed` discriminator to status | +5 |
| `unity-plugin/Editor/MCPServer.cs:80-88` | Remove `compilationFinished`->`delayCall`->"ready"; on fail write "compile_failed" | -6, +3 |
| `unity-plugin/Editor/MCPServer.cs:130` | `#if UNITY_EDITOR_WIN` ExclusiveAddressUse | +3 |
| `unity-plugin/Editor/MCPServer.cs:285-295` | Add `sync_status` to TCP fast-path | +5 |
| `unity-plugin/Editor/MCPServer.cs:502-505` | Atomic state write: `File.Move(tmp, path, overwrite: true)` on net6+ or try/catch | +2, -2 |
| `unity-plugin/Editor/Chat/MCPChatWindow.Drain.cs:63` | isCompiling/isUpdating gate on TryResumePendingTurn | +3 |
| `unity-plugin/Editor/Chat/MCPChatWindow.Drain.cs:174-178` | Move `_needsRefresh` Refresh to TurnDone handler (debounce) | +4, -4 |
| `unity-plugin/Editor/Chat/ReloadGuard.cs:29-34,46-51` | Add DisallowAutoRefresh + SessionState lock-marker + InitializeOnLoad rebalance | +15 |
| **Total** | | **~+180 lines** |

## 4. SS14 Coverage

| SS14 row (file:line) | Fixes? | Notes |
|---|---|---|
| CommandRouter.cs:267 (bare Refresh returns "ok") | YES | `sync` returns `"sync_started\|epoch=N"`; `recompile` stays for backward compat but docstring says "use sync_unity" |
| scene.py:126 (false docstring) | YES | Deprecation docstring |
| code_intel.py:60-107 (await_compile false "idle") | YES | Epoch-aware: waits for epoch match + reconnect + both-signals |
| CompileNotifier.cs:18-24 (no fail marker) | YES | Add `scriptCompilationFailed` check to status |
| MCPServer.cs:80-88 (false-ready race) | YES | Delete compilationFinished->delayCall->ready; "ready" only from post-reload StartAsync |
| MCPServer.cs:130 (ReuseAddress Windows) | YES | `#if UNITY_EDITOR_WIN` ExclusiveAddressUse |
| MCPServer.cs:474-485 (OnBeforeReload) | NO (already OK) | Confirmed correct per SS3/SS8 |
| MCPServer.cs:502-505 (non-atomic write) | YES | File.Move overwrite or delete-in-catch |
| Drain.cs:61-131 (no isCompiling gate) | YES | 3-line gate at top of TryResumePendingTurn |
| Drain.cs:164-167 (mid-turn Refresh under lock) | PARTIAL | Move to TurnDone (debounce); full fix (batch-end only) deferred |
| ReloadGuard.cs:31,50 (Lock without Disallow) | YES | Add DisallowAutoRefresh + SessionState marker + InitializeOnLoad rebalance |
| ChatProcess.cs:34-35 (misleading comment) | YES | Fix comment text |
| bridge_heartbeat.py:39-54 (flat backoff) | NO (out of scope) | Low risk per SS14; optional improvement |
| editor_log_parser.py:22-46 (-logFile override) | NO (out of scope) | Doc-level concern only |

**Score: 11/14 fixed, 1 confirmed OK, 2 deferred (low risk).**

## 5. R23 -- Per-OS branches

Per SS16, the sync core is OS-independent (in-process TCP, no focus hacks). Per-OS branches:

| Where | What | Guarded by |
|---|---|---|
| MCPServer.cs:130 | `ExclusiveAddressUse=true` | `#if UNITY_EDITOR_WIN` |
| MCPServer.cs:502 | State-file atomic write: .NET 6+ `File.Move(overwrite:true)` works cross-platform; pre-6 fallback: try `File.Delete` + `File.Move` in try/catch (existing code, Windows race accepted as low-probability) | Runtime check or `#if NET6_0_OR_GREATER` -- but Unity 6000 uses CoreCLR so net6+ is available |
| editor_log_parser.py:22-46 | Already per-OS (darwin/win32/linux) -- no change needed | Existing `sys.platform` branches |
| scripts/bump_version.py | `os.replace()` -- POSIX atomic, Windows near-atomic (replaces target) | Cross-platform stdlib |
| CLAUDE.md | Remove `osascript` recipe | Doc change only |

No new per-OS code paths in the sync flow itself.

## 6. Test Plan (mapped to P1-P4)

### P1 -- Python unit (server/tests/test_sync_unity.py)

Reuse `_make_send` pattern from `test_await_compile.py`. Mock `_send` at module level.

| Test | What's mocked | Asserts |
|---|---|---|
| `test_both_signals_required_for_clean` | send returns idle+epoch match, editor_log.corroborate returns stale-dll warn | NOT "sync clean" until both clear |
| `test_stale_dll_blocks_false_clean` | send returns idle, corroborate returns stale | Contains "[warn: stale]" |
| `test_epoch_race_no_premature_idle` | send returns idle with WRONG epoch, then correct | Polls until epoch match |
| `test_reconnect_after_domain_reload` | DomainReloadError on first call, then idle+epoch | "sync clean" |
| `test_timeout_returns_partial_status` | Always "compiling" | "timeout" in result |
| `test_compile_errors_verbatim` | idle+epoch, errors="CS0001..." | Errors in result |
| `test_idempotent_noop_fast_path` | send returns "idle_no_changes" immediately | Result in <1 poll |
| `test_unity_dead_fails_fast` | ConnectionError on all calls | Fast error, no 120s hang |
| `test_compile_failed_no_reload_wait` | send returns "failed\|dur\|epoch=N" | Errors returned, no reconnect-wait |

TDD order: `test_idempotent_noop_fast_path` -> `test_epoch_race` -> `test_both_signals` ->
`test_reconnect` -> `test_compile_failed` -> rest.

### P2 -- Bump + trigger (split: Python + C# EditMode)

**Python** (`server/tests/test_bump_version.py`):

| Test | Asserts |
|---|---|
| `test_bump_patch_atomic_idempotent` | 0.20.7 -> 0.20.8; second call -> 0.20.9; no partial file on concurrent read |
| `test_bump_only_when_plugin_changed` | bump() called only when plugin files touched (mock check) |

**C# EditMode** (`unity-plugin/Editor/Tests/SyncEpochTrackerTests.cs`):

| Test | Asserts |
|---|---|
| `test_sync_helper_calls_refresh` | Injectable ops seam: assert Refresh called (mock `Action refreshOp`) |
| `test_sync_request_returns_will_compile` | Returns "sync_started\|epoch=1" |
| `test_epoch_survives_domain_reload` | SessionState.GetInt after SetInt (simulated -- SS survives reload by contract) |
| `test_sync_status_returns_epoch` | GetSyncStatus includes epoch field |
| `test_compile_failed_status` | After mock scriptCompilationFailed=true, status = "failed\|..." |

### P3 -- Chat C# EditMode

| Test | File | Asserts |
|---|---|---|
| `test_resume_gated_on_compiling` | `Tests/ResumeCompileGateTests.cs` | TryResumePendingTurn returns early when isCompiling mock=true; `Func<bool>` injected |
| `test_reloadguard_disallow_auto_refresh` | `Tests/ReloadGuardTests.cs` (existing) | OnTurnStarted calls both Lock + Disallow; OnTurnFinished calls both Unlock + Allow |
| `test_reloadguard_session_state_rebalance` | same | After simulated reload (ResetForTest + re-init), counter=0 |

### P4 -- Live (pytest -m live)

| Test | What | Gate |
|---|---|---|
| `test_live_sync_full_cycle` | Write .cs -> sync_unity -> type visible | assert "sync clean" |
| `test_live_sync_compile_error_then_fix` | Write bad .cs -> sync -> errors -> fix -> sync -> clean | No stale errors |
| `test_live_reconnect_transparent` | sync with plugin code change -> reconnect works | Connected after |
| `test_live_noop_sync_fast` | sync without changes -> <5s | Timing assert |
| `test_live_plugin_bump_re_resolve` | Touch plugin .cs -> sync_unity(bump=true) -> get_version echo shows new code | **Main regression test** |
| `test_live_dll_freshness_ground_truth` | After sync, editor_log.check_dll_freshness == True | Freshness confirmed |

### TDD order for developer

1. P1 Python unit tests (red) -> implement `sync_unity` + epoch-aware `await_compile`
2. P2 Python bump tests (red) -> implement `bump_version.py`
3. P2 C# SyncEpochTracker tests (red) -> implement SyncEpochTracker.cs + CommandRouter changes
4. P3 Chat tests (red) -> implement Drain.cs gate + ReloadGuard fixes
5. MCPServer.cs fixes (R-3, R-4, Windows) -- covered by existing tests + new P2
6. P4 live tests last

## 7. Risks and Open Questions

| Risk | Mitigation | Spike needed? |
|---|---|---|
| `Client.Resolve()` + `Refresh()` ordering: does Resolve block Refresh or vice versa? | Resolve is fire-and-forget void (ref SS4). Call Resolve BEFORE Refresh -- worst case: two reloads (Resolve triggers one, Refresh triggers another) | Spike on 6000.3.0b7: call both, observe single vs double reload |
| `compilationFinished` delayCall removal breaks existing "ready" on no-compile Refresh | `StartAsync:146` already writes "ready" after bind. If Refresh triggers no compile (no dirty scripts), no `compilationStarted` fires, state stays "ready" from previous boot. No regression. | Verify: Refresh with no dirty scripts -> state file stays "ready" |
| SessionState epoch persistence: does SessionState survive domain reload on 6000.3 beta? | SS is documented HIGH for reload survival. CompileNotifier already uses it (working). | No spike needed |
| `File.Move(src, dst, overwrite: true)` -- Unity 6000 CoreCLR supports .NET 6+ overload? | Unity 6000.0+ uses CoreCLR (confirmed). Fallback: existing delete+move pattern is acceptable for state file (low-frequency writes, atomic enough). | Compile test on 6000.3 |
| `_needsRefresh` move to TurnDone: does deferring Refresh break mid-turn file visibility? | Files are already on disk (Python wrote them). Refresh just tells Unity to notice. Deferring to TurnDone means Unity picks up ALL files at once -- actually better (avoids phantom partial-compile errors per SS14 row). | Manual test: multi-file edit turn, verify all files compiled together |
| ReloadGuard DisallowAutoRefresh: adds ref-counted native counter that survives reload | SessionState marker + `[InitializeOnLoad]` rebalance pattern from ref SS6. If marker present on reload, call `AllowAutoRefresh()` + `UnlockReloadAssemblies()` to restore balance. | No spike -- pattern is documented HIGH |
| IN-93874 CleanBuildCache no-op on U6000 | We don't use `RequestScriptCompilation` at all (D3). Not our problem. | None |
| Version-echo: `get_version` fast-path returns hardcoded "1.0" (MCPServer:287), not package version | Need new `get_plugin_version` command or fix fast-path to read from package.json. R18 requires version-echo. Add to SyncEpochTracker: read `PackageInfo.FindForAssembly()` or parse package.json. | Implementation detail, no spike |
