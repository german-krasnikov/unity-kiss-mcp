# Feature: TCP Bridge

## Overview

TCP communication between Python MCP Server and Unity Editor Plugin. Includes heartbeat (sole reconnect mechanism), compile state probing, single-slot connection, and exclusive lockfile.

## Architecture (для Architect)

```
Python (AsyncIO)                          C# (Unity)
┌────────────────────┐                   ┌──────────────────┐
│ ConnectionSlot     │                   │ MCPServer         │
│  └─ UnityBridge    │ ←── TCP:9500 ──→ │  ├─ HandleClient  │
│                     │                   │  ├─ ProcessQueue  │
│ CompileStateProbe   │                   │  ├─ PortDiscovery │
│ Lockfile (fcntl)    │                   │  └─ StateFile     │
│ CrashLogger         │                   │                   │
│ Heartbeat (15s/5s/  │                   │ going_away event  │
│   2s, reconnect)    │                   │ SO_KEEPALIVE      │
│                     │                   │ Per-cmd timeouts  │
└────────────────────┘                   └──────────────────┘
```

### Protocol

```
[4 bytes: uint32 BE length][UTF-8 JSON payload]
```

Max message size: 10MB.

### Request Format

```json
{"id": "a1b2", "cmd": "get_hierarchy", "args": {"depth": 3}}
```

### Response Format

```json
{"id": "a1b2", "ok": true, "data": "..."}
{"id": "a1b2", "ok": false, "err": "Not found"}
```

### Event Frame (server → client, no id)

```json
{"ev": "going_away", "reason": "domain_reload"}
```

## Implementation Notes (для Developer)

### Python Client (bridge.py)

**UnityBridge** — single TCP connection to one Unity instance.

Key features:
- Socket options: `TCP_NODELAY`, `SO_KEEPALIVE` (macOS: idle=60s, interval=10s, count=3; detects dead peers within ~90s)
- Heartbeat: 15s interval `_raw_ping()` (bypasses retry machinery), 3 consecutive failures OR `is_process_dead()` → close. Disconnected polling: 5s if probe busy, 2s otherwise.
- `_raw_ping()`: lightweight ping (default timeout=10s, heartbeat calls with 20s) that acquires lock, sends framed message directly on socket, validates response ID match. Prevents heartbeat from consuming RPC responses.
- Reconnect: cooldown `MIN_RECONNECT_INTERVAL=2.0s` gates frequency; `_reconnect_cooldown_ok()` check prevents thundering herd. Heartbeat uses probe for TIMING only, not as gate for reconnect.
- `DomainReloadError`: raised when Unity sends `going_away` event frame → immediate close (no wait), triggers `mark_recompile_issued()` state probe update, fast reconnect
- `_ensure_heartbeat()`: called on every `send()`, auto-restarts heartbeat task if it died (safety net)
- `send()` retry logic: idle errors get 1 grace attempt; busy (domain reload / probe) gets full retries with exponential backoff (capped at 8s). Session deadline `SESSION_TIMEOUT=120s` prevents infinite retries.
- Lock per connection for thread safety

### ConnectionSlot (connection_slot.py)

Single-connection manager (replaces former multi-connection BridgeManager):
- `connect(port, host)` — create/connect a bridge (closes previous if any)
- `close()` — stop heartbeat and close bridge
- `bridge` property — the single UnityBridge instance
- `connected` property — shortcut for bridge.connected
- No `reconnect()` method — reconnection handled by UnityBridge heartbeat loop

### Port Discovery & TCP Probe (server_filtering.py) — v0.23.0

Port discovery reads `~/.unity-mcp/ports/{pid}.port` files. **v0.23.0:** Adds `_tcp_probe(port, timeout=0.2)` — quick TCP handshake to verify port actually listens before returning. Filters out stale discovery files (port written but server not yet bound, or server crashed leaving orphan file). Candidates prioritized: env UNITY_MCP_PORT → CWD project path match → newest mtime → default 9500.

### CompileStateProbe (compile_state.py)

Simplified detector for Unity C# compile/domain-reload:
- **State file**: reads `~/.unity-mcp/state/port-{port}.state` via `unity_state.py` (ready/compiling/reloading/restarting)
- `is_process_dead()` — cross-checks PID from port file
- `has_strong_busy_signal()` — state file (authoritative) then lock file fallback
- `_lock_file_exists()` — checks Unity's BeeDriver Lock file

### Lockfile (lockfile.py) — v0.23.0 Zombie Detection

Exclusive lock per port at `~/.unity-mcp/server-{port}.lock`:
- Uses `fcntl.flock` (POSIX exclusive)
- **Zombie Process Detection (v0.23.0):** New `_is_zombie(pid)` check via `/proc/{pid}/stat` (Linux) or `ps -p` status (macOS/Windows). Stale processes with zombie state are no longer treated as "live" — server startup proceeds without waiting for process cleanup. Fixes `-32000 (server error)` when a previous server process became a zombie.
- Auto-kills stale `unity_mcp` process (SIGTERM + poll)
- Prevents multiple MCP servers on same Unity instance

### Crash Logging (crash_log.py)

Append-only JSONL crash log for unhandled exceptions:
- `log_crash(exc, *, log_dir=None)`: module-level function that writes `{"ev":"crash", "exc":"Type", "msg":"...", "tb":"...", "t":timestamp}` to `crash.jsonl` (defaults to `~/.unity-mcp/crash.jsonl`)
- Auto-creates parent dir, silent on I/O failures
- Integrated into `main()`: outer try/except catches `BaseException` → calls `log_crash()` → re-raises (preserves clean shutdown for `KeyboardInterrupt`, `SystemExit`, EPIPE)
- **CrashLogger class**: JSONL append-only logger with rotation (500 entries max, 15MB size limit) — logs disconnect, reconnect events (older feature). Separate from module-level `log_crash()` used for unhandled server exceptions.

### C# Server (MCPServer.cs) — v0.23.0 SO_REUSEPORT Recovery

- TCP listener on port 9500 (configurable via `UNITY_MCP_PORT` env var)
- Max message size: 10MB
- SO_KEEPALIVE with platform-specific tuning (idle=60s, interval=10s, count=3; relaxed from 10s/5s to survive macOS App Nap timer coalescing)
- **SO_REUSEPORT (v0.23.0, macOS/Linux only):** Enables port reuse for rapid reconnect after server crash or process termination. Windows doesn't require it (already has soft TIME_WAIT). Prevents "address already in use" during recovery without waiting for kernel TIME_WAIT timer.
- Single client mode: new connection disconnects previous
- Client generation tracking: prevents stale handlers from clearing shared state
- Lifecycle hardening: `IsRunning` property guarded with try/catch for ObjectDisposedException; `Stop()` wraps listener teardown with try/catch; `OnBeforeReload()` wrapped with try/catch
- Socket shutdown: `Shutdown(Both)` before `Stop()` in OnBeforeReload and Stop (TCP_NODELAY + shutdown both directions → faster port release)

**Bind retry:**
- 5 retry attempts on EADDRINUSE (port in TIME_WAIT after crash/domain reload)
- Linear backoff: 500ms × attempt number
- Re-registration of watchdog + heartbeat callback on success

**Watchdog (Cycle 16+):**
- Separate `EditorApplication.update` callback (WatchdogTick)
- Monitors server liveness, restarts if dead within 5 seconds
- Properly unregistered in Stop(), OnQuit(), OnBeforeReload()
- Re-registered in StartAsync() after bind succeeds

**Port discovery:**
- Writes `~/.unity-mcp/ports/{pid}.port` (port, project path, project name)
- Python auto-discovers port from these files

**State file:**
- Writes `~/.unity-mcp/state/port-{port}.state`
- States: `ready`, `compiling`, `reloading`, `restarting` (new in Cycle 16)
- "restarting": written when compilationFinished but server not yet running; indicates startup in progress (TCP client should wait)

**Domain reload handling (OnBeforeReload):**
1. Sets `_shuttingDown = true`
2. Writes "reloading" to state file
3. Sends `{"ev":"going_away","reason":"domain_reload"}` synchronously
4. Cancels CTS tokens, closes client + listener
5. Does NOT delete port file (port survives reload)
6. Re-starts via `[InitializeOnLoad]` static ctor `delayCall` after reload

**Fast-path commands** (bypass main thread dispatch):
- `ping`, `get_version`, `status`, `get_enabled_tools`

**Per-command timeouts:**
| Command | Timeout |
|---------|---------|
| `run_tests`, `run_playtest` | 130s |
| `batch` | 65s |
| `wait_until`, `move_to`, `test_step` | 30s |
| Default | 25s |

## Code Locations

- Python bridge: `server/src/unity_mcp/bridge.py`
- Python connection slot: `server/src/unity_mcp/connection_slot.py`
- Python compile probe: `server/src/unity_mcp/compile_state.py`
- Python unity state: `server/src/unity_mcp/unity_state.py`
- Python lockfile: `server/src/unity_mcp/lockfile.py` (with v0.23.0 zombie detection)
- Python crash log: `server/src/unity_mcp/crash_log.py`
- Python server filtering: `server/src/unity_mcp/server_filtering.py` (with v0.23.0 TCP probe)
- Python server wrapper: `server/src/unity_mcp/server.py` (main() crash handler)
- C#: `unity-plugin/Editor/MCPServer.cs`
- Tests: `server/tests/test_bridge.py` (37), `server/tests/test_connection_slot.py` (8), `server/tests/test_lockfile.py` (17), `server/tests/test_crash_log.py` (10), `server/tests/test_server.py` (4 new main() crash tests)

## Reconnection Strategy

**Problem:** Unity domain reload closes socket; compile/reloading blocks commands; stale state file blocks reconnect; state loss on rapid reconnect.

**Solution:** Multi-layered resilience (Cycles 14–16):

1. **Exclusive lockfile** (fcntl LOCK_EX): single MCP server per port, kill-old semantics prevent multiple processes interfering
2. **`_raw_ping()` heartbeat** (15s when connected; 5s/2s when disconnected based on probe): sole reconnect mechanism, validates ID match to prevent response misroute
3. **Reconnect cooldown** (`MIN_RECONNECT_INTERVAL=2.0s`): `_reconnect_cooldown_ok()` gate prevents thundering-herd storms
4. **Probe for timing, not gating**: compile-state heuristic used only to compute delay (5s vs 2s), NOT to block reconnect
5. **DomainReloadError immediate close**: going_away event triggers close immediately (no wait for 3 failures), fast reconnect path
6. **State file management**: MCPServer writes "ready" on `compilationFinished` handler (not just startup); prevents stale "compiling" blocking reconnect
7. **SO_KEEPALIVE**: OS-level dead peer detection (~90s: idle=60s + 3 probes at 10s intervals)
8. **Reconnect callbacks**: invalidate tool cache, re-probe capabilities
9. **CrashLogger**: JSONL append-only log at `~/.unity-mcp/crash.jsonl` (500 entries max, 15MB rotation) — logs disconnect, reconnect, exhausted events

```
send(cmd, args, timeout=30.0)
  → write frame + await response
  → on retry hint from Unity: sleep retry_ms, re-send (up to MAX_RETRIES=3)
  → idle errors get 1 grace attempt; busy (domain reload) gets full retries with exp backoff
  
_heartbeat_loop():
  when connected:
    → await 15s
    → skip if lock held (RPC in progress)
    → _raw_ping(timeout=20s) (direct socket, ID-match verify)
    → 3 failures OR is_process_dead() → close
  when disconnected:
    → await 5s (probe busy) or 2s (not busy)
    → if _reconnect_cooldown_ok() → reconnect()
  
On event frame {"ev":"going_away"}:
  → raise DomainReloadError → close immediately (no delay)
  
On TimeoutError / ConnectionError:
  → close connection
  → heartbeat picks up disconnected state → reconnect on next poll
```

## TDD Scenarios (для Developer)

### Python Client (37 tests in test_bridge.py)

**Protocol / framing:**
- `test_send_encodes_header_as_big_endian_uint32`, `test_send_encodes_payload_as_utf8_json`
- `test_send_includes_incremental_id`, `test_read_response_decodes_json`
- `test_message_too_large_raises_error`, `test_send_concurrent_unique_ids`
- `test_close_cleans_up_writer`

**Port config:**
- `test_bridge_default_port`, `test_bridge_custom_port`, `test_bridge_env_port`
- `test_bridge_explicit_port_overrides_env`, `test_bridge_invalid_env_port_falls_back_to_default`

**Retry / error handling:**
- `test_send_fails_fast_on_connection_error`, `test_send_raises_on_connection_error`
- `test_bridge_auto_retry_on_retry_hint`, `test_bridge_no_retry_on_normal_error`
- `test_send_raises_timeout_error_after_max_retries`, `test_bridge_retry_respects_max_retries`
- `test_idle_retry_gets_one_grace_attempt`

**Heartbeat:**
- `test_heartbeat_default_interval_is_15`, `test_heartbeat_detects_zombie_connection`
- `test_heartbeat_reconnects_when_disconnected`, `test_heartbeat_reconnects_when_busy`
- `test_heartbeat_stops_on_close`, `test_heartbeat_immediate_close_when_pid_dead`
- `test_heartbeat_immediate_close_on_domain_reload_error`
- `test_heartbeat_respects_reconnect_cooldown`
- `test_ensure_heartbeat_restarts_dead_task`, `test_heartbeat_survives_tick_exception`

**Raw ping:**
- `test_raw_ping_bypasses_send_retry`, `test_raw_ping_raises_on_disconnected`

**Reconnect cooldown:**
- `test_reconnect_cooldown_default_2s`, `test_reconnect_cooldown_blocks_rapid_reconnect`
- `test_reconnect_cooldown_allows_after_interval`
- `test_reconnect_callback_debounce_skips_rapid_calls`, `test_reconnect_callback_debounce_allows_after_cooldown`

**Failure description:**
- `test_describe_failure_reports_crash_when_pid_dead`

### C# Server

1. **Test_AcceptsConnection**: start → client connects successfully
2. **Test_ParsesMessage**: receive bytes → command extracted
3. **Test_DispatchesToMainThread**: command → processed on main thread
4. **Test_GoingAwayOnDomainReload**: assembly reload → event frame sent
5. **Test_StateFileUpdated**: compile/reload → state file written
6. **Test_FastPathBypassesMainThread**: ping → response without main thread dispatch

## Review Checklist (для Reviewer)

- [ ] Big-endian byte order (4-byte prefix)
- [ ] Max message size validation (10MB both sides)
- [ ] Lock on Python writes (asyncio.Lock)
- [ ] NoDelay = true on both sides
- [ ] SO_KEEPALIVE configured (idle=60s, interval=10s, count=3; ~90s dead peer detect; relaxed from 10s/5s to survive App Nap)
- [ ] Main thread dispatch via ConcurrentQueue
- [ ] Graceful shutdown (going_away event before close)
- [ ] Heartbeat reconnect logic correct
- [ ] Lockfile released on shutdown
- [ ] Port file cleaned up on exit
- [ ] State file written before compile/reload
- [ ] Heartbeat interval (15s) appropriate
- [ ] Reconnect cooldown (2s min) prevents thrashing
- [ ] Lifecycle guards: IsRunning, Stop(), OnBeforeReload() all try/catch protected

## Related

- Skill: `.claude/skills/tcp-protocol.md`
- MCP Server: `AI/mcp-server.md`
- Architecture: `AI/architecture.md`
