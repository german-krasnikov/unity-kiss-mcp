#!/usr/bin/env python3
"""relay_probe.py — real-world test driver for chat_relay.py.
No Unity required. Spawns relay inline or connects to existing one.

Usage:
  cd server
  python scripts/relay_probe.py                    # spawns relay, runs all scenarios
  RELAY_PORT=59123 python scripts/relay_probe.py   # use existing relay
  python scripts/relay_probe.py s5                 # run one scenario by name
"""
import asyncio
import json
import os
import struct
import sys
import time

PY = sys.executable
_SRC = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src")


# ─── Transport ────────────────────────────────────────────────────────────────

async def _send(port: int, cmd: str, args: dict, *, timeout=5.0) -> dict:
    """Single-shot framed JSON request on a fresh TCP connection."""
    r, w = await asyncio.wait_for(
        asyncio.open_connection("127.0.0.1", port), timeout=timeout)
    req = json.dumps({"id": "1", "cmd": cmd, "args": args}).encode()
    w.write(struct.pack("!I", len(req)) + req)
    await w.drain()
    hdr  = await asyncio.wait_for(r.readexactly(4), timeout=timeout)
    body = await asyncio.wait_for(
        r.readexactly(struct.unpack("!I", hdr)[0]), timeout=timeout)
    w.close()
    await w.wait_closed()
    return json.loads(body)


async def _start_relay():
    """Spawn chat_relay.py, parse port from stdout. Returns (port, proc)."""
    env = {**os.environ, "PYTHONPATH": _SRC}
    proc = await asyncio.create_subprocess_exec(
        PY, "-m", "unity_mcp.chat_relay",
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.DEVNULL,
        env=env,
    )
    line = (await asyncio.wait_for(proc.stdout.readline(), timeout=5)).decode().strip()
    port = int(line.split(":")[1])
    print(f"[relay] started pid={proc.pid} port={port}")
    return port, proc


async def _drain_events(port: int, after_seq: int = -1) -> list[tuple[int, str]]:
    resp = await _send(port, "events", {"after_seq": after_seq})
    data = resp.get("data", "")
    if not data:
        return []
    parts = data.split("\n")
    out = []
    for i in range(0, len(parts) - 1, 2):
        try:
            out.append((int(parts[i]), parts[i + 1]))
        except ValueError:
            break
    return out


async def _wait_events(port: int, after_seq: int = -1, *, min_count=1,
                       poll_sec=0.1, timeout=10.0) -> list[tuple[int, str]]:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        evs = await _drain_events(port, after_seq)
        if len(evs) >= min_count:
            return evs
        await asyncio.sleep(poll_sec)
    raise TimeoutError(f"Expected {min_count} events after seq={after_seq} within {timeout}s")


async def _spawn(port: int, argv: list[str], binary: str | None = None) -> dict:
    b = binary or PY
    return await _send(port, "spawn", {
        "binary": b, "argv": argv, "env_set": {}, "env_strip": []
    })


def _pid_from(data: str) -> int:
    for part in data.split("|"):
        if "pid=" in part:
            return int(part.split("pid=")[1].split()[0].strip())
    raise ValueError(f"No pid= in: {data!r}")


# ─── Scenarios ────────────────────────────────────────────────────────────────

async def s1(port: int):
    """Basic echo: spawn cat, send line, read back via events."""
    print("\n=== S1: Basic echo ===")
    r = await _spawn(port, [], binary="/bin/cat")
    assert r["ok"], f"spawn failed: {r}"
    await _send(port, "send", {"line": "hello relay"})
    evs = await _wait_events(port, min_count=1)
    assert any("hello relay" in t for _, t in evs), f"echo missing, got: {evs}"
    await _send(port, "kill", {})
    print("  PASS")


async def s2(port: int):
    """Buffer overflow: 600 lines > maxlen=500. Silent drop, no crash."""
    print("\n=== S2: Buffer overflow (B2 — deque maxlen=500) ===")
    script = "for i in range(600): print(f'line_{i}', flush=True)"
    r = await _spawn(port, ["-c", script])
    assert r["ok"], r
    await asyncio.sleep(2.0)
    evs = await _drain_events(port, -1)
    seqs = [s for s, _ in evs]
    print(f"  Got {len(evs)} events, seq range: {seqs[0] if seqs else '?'}..{seqs[-1] if seqs else '?'}")
    assert len(evs) <= 500, f"Deque overflow: got {len(evs)}"
    for i in range(1, len(seqs)):
        assert seqs[i] == seqs[i - 1] + 1, f"Seq gap: {seqs[i-1]}..{seqs[i]}"
    dropped = 600 - len(evs)
    print(f"  {dropped} events dropped silently — client has NO way to detect this")
    if dropped > 0:
        print("  BUG B2: silent data loss confirmed")
    print("  PASS (no crash)")


async def s3(port: int):
    """Unicode + emoji + escaped backslash round-trip."""
    print("\n=== S3: Unicode/emoji ===")
    await _spawn(port, [], binary="/bin/cat")
    msgs = ["hello 👋", "こんにちは", "Привет!", "back\\slash", 'quote"here']
    for m in msgs:
        await _send(port, "send", {"line": m})
    await asyncio.sleep(0.3)
    evs = await _drain_events(port, -1)
    texts = [t for _, t in evs]
    for m in msgs:
        assert any(m in t for t in texts), f"Missing: {m!r}\nGot: {texts}"
    await _send(port, "kill", {})
    print("  PASS")


async def s4(port: int):
    """Reconnect + seq continuity: no duplicates after reconnect."""
    print("\n=== S4: Reconnect seq continuity ===")
    script = (
        "import time\n"
        "for i in range(20): print(f'ev_{i}', flush=True); time.sleep(0.05)\n"
    )
    await _spawn(port, ["-c", script])
    await asyncio.sleep(0.4)
    batch1 = await _drain_events(port, -1)
    last_seq = batch1[-1][0] if batch1 else -1
    print(f"  Batch1: {len(batch1)} events, last_seq={last_seq}")

    await asyncio.sleep(0.6)  # rest of subprocess output
    batch2 = await _drain_events(port, last_seq)
    print(f"  Batch2 (after_seq={last_seq}): {len(batch2)} events")
    for s, _ in batch2:
        assert s > last_seq, f"Duplicate seq {s} (last_seq={last_seq})"
    total = len(set([s for s, _ in batch1] + [s for s, _ in batch2]))
    print(f"  Total unique: {total}/20")
    assert total >= 15, f"Too many events lost: {total}"
    await _send(port, "kill", {})
    print("  PASS")


async def s5(port: int):
    """stdin drain stall (B1): 64KB to stdin-ignoring process."""
    print("\n=== S5: stdin drain stall (B1) ===")
    await _spawn(port, ["-c", "import time; time.sleep(60)"])
    big = "x" * (256 * 1024)  # 256KB > macOS pipe buffer (64KB) — triggers drain stall
    print(f"  Sending {len(big):,} bytes to process that ignores stdin...")
    t0 = time.monotonic()
    try:
        r = await asyncio.wait_for(_send(port, "send", {"line": big}, timeout=6.0), timeout=6.0)
        elapsed = time.monotonic() - t0
        print(f"  Responded in {elapsed:.3f}s: ok={r.get('ok')}")
        if elapsed > 1.5:
            print(f"  BUG B1: drain() blocked {elapsed:.2f}s — event loop starved during that time")
        else:
            print("  OK: pipe buffer absorbed write")
    except asyncio.TimeoutError:
        elapsed = time.monotonic() - t0
        print(f"  BUG B1 CONFIRMED: send() deadlocked after {elapsed:.2f}s")
        print("    Impact: ALL C# events polls queued behind this forever")
    finally:
        await _send(port, "kill", {})


async def s8(port: int):
    """spawn/kill/spawn: old pid dead, new pid in status."""
    print("\n=== S8: spawn/kill/spawn clean state ===")
    r1 = await _spawn(port, ["-c", "import time; time.sleep(60)"])
    pid1 = _pid_from(r1["data"])
    await _send(port, "kill", {})
    await asyncio.sleep(0.3)
    try:
        os.kill(pid1, 0)
        print(f"  BUG: pid1={pid1} still alive!")
    except ProcessLookupError:
        print(f"  OK: pid1={pid1} dead")

    r2 = await _spawn(port, ["-c", "import time; time.sleep(60)"])
    pid2 = _pid_from(r2["data"])
    st = await _send(port, "status", {})
    assert f"pid={pid2}" in st["data"], f"Bad status: {st}"
    assert pid2 != pid1
    await _send(port, "kill", {})
    print("  PASS")


async def s10(port: int):
    """Clean exit silence (B3): process exits 0 → no error event → C# hangs.
    Uses seq-fencing to ignore events from previous scenarios."""
    print("\n=== S10: Clean exit silence (B3) ===")
    # Fence: get current max seq before we start
    fence_evs = await _drain_events(port, -1)
    fence_seq = fence_evs[-1][0] if fence_evs else -1

    await _spawn(port, ["-c", "print('response done', flush=True)"])
    await asyncio.sleep(0.5)
    # Only look at events AFTER the fence
    evs = await _drain_events(port, fence_seq)
    texts = [t for _, t in evs]
    has_text  = any("response done" in t for t in texts)
    has_error = any('"is_error":true' in t for t in texts)
    print(f"  New events since fence: {len(evs)}")
    print(f"  Output text received:   {has_text}")
    print(f"  Error event injected:   {has_error}")
    st = await _send(port, "status", {})
    print(f"  Status: {st['data']}")
    if has_text and not has_error:
        print("  BUG B3 CONFIRMED: clean exit → no signal → C# spinner hangs forever")
    elif has_error:
        print("  OK: error event injected despite exit code 0")

    # Secondary observation: B8 — buffer cross-session contamination
    # When C# domain-reloads and sends after_seq=-1, it sees ALL old events
    print(f"  NOTE B8: relay buffer has {len(fence_evs)} events from prior sessions.")
    print(f"    On C# domain reload (_lastSeq=-1), those would be replayed to UI.")


async def s12(port: int):
    """switch kills old process, no leak."""
    print("\n=== S12: switch process leak ===")
    r1 = await _spawn(port, ["-c", "import time; time.sleep(60)"])
    pid1 = _pid_from(r1["data"])

    r2 = await _send(port, "switch", {
        "binary": PY,
        "argv": ["-c", "import time; time.sleep(60)"],
        "env_set": {}, "env_strip": [],
    })
    pid2 = _pid_from(r2["data"])
    await asyncio.sleep(0.3)

    try:
        os.kill(pid1, 0)
        print(f"  BUG: pid1={pid1} still alive after switch!")
    except ProcessLookupError:
        print(f"  OK: pid1={pid1} killed by switch")

    st = await _send(port, "status", {})
    assert f"pid={pid2}" in st["data"], f"Wrong pid in status: {st}"
    await _send(port, "kill", {})
    print("  PASS")


async def s13(port: int):
    """close_stdin causes stdin-reading process to exit."""
    print("\n=== S13: close_stdin → process exit ===")
    await _spawn(port, ["-c",
        "import sys; d=sys.stdin.read(); print(f'read {len(d)} bytes', flush=True)"])
    await asyncio.sleep(0.1)
    await _send(port, "close_stdin", {})
    await asyncio.sleep(0.5)
    evs = await _drain_events(port, -1)
    texts = [t for _, t in evs]
    assert any("read" in t for t in texts), f"No output: {texts}"
    st = await _send(port, "status", {})
    assert "dead" in st["data"] or "no_session" in st["data"], \
        f"Still alive after stdin close: {st}"
    print(f"  Status: {st['data']}")
    print("  PASS")


async def s_multi(port: int):
    """Two concurrent TCP connections both send spawn — documents race (B4)."""
    print("\n=== S-MULTI: Concurrent spawn race (B4) ===")
    async def client(n: int):
        return await _send(port, "spawn", {
            "binary": PY,
            "argv": ["-c", f"import time; time.sleep(10)"],
            "env_set": {}, "env_strip": [],
        })

    r1, r2 = await asyncio.gather(client(1), client(2))
    print(f"  Client1: {r1.get('data')}")
    print(f"  Client2: {r2.get('data')}")
    st = await _send(port, "status", {})
    print(f"  Final status: {st['data']}")
    try:
        pid1 = _pid_from(r1["data"])
        pid2 = _pid_from(r2["data"])
        if pid1 != pid2:
            # Check if both are alive
            alive = []
            for pid in (pid1, pid2):
                try:
                    os.kill(pid, 0)
                    alive.append(pid)
                except ProcessLookupError:
                    pass
            if len(alive) > 1:
                print(f"  BUG B4: {len(alive)} pids alive simultaneously: {alive}")
            else:
                print(f"  OK: only one pid survived ({alive})")
    except ValueError:
        pass
    await _send(port, "kill", {})


SCENARIOS = {
    "s1":    s1,
    "s2":    s2,
    "s3":    s3,
    "s4":    s4,
    "s5":    s5,
    "s8":    s8,
    "s10":   s10,
    "s12":   s12,
    "s13":   s13,
    "multi": s_multi,
}

# Run order: most likely to find bugs first
_DEFAULT_ORDER = ["s1", "s3", "s8", "s13", "s12", "s10", "s4", "s2", "s5", "multi"]


async def main():
    port_env = int(os.environ.get("RELAY_PORT", 0))
    relay_proc = None

    if port_env:
        port = port_env
        print(f"[relay] connecting to existing relay on port {port}")
    else:
        port, relay_proc = await _start_relay()

    filter_arg = sys.argv[1].lower() if len(sys.argv) > 1 else None
    to_run = (
        [filter_arg] if filter_arg and filter_arg in SCENARIOS
        else _DEFAULT_ORDER
    )

    passed, failed = 0, []
    for name in to_run:
        fn = SCENARIOS[name]
        try:
            await fn(port)
            passed += 1
        except Exception as e:
            print(f"  FAIL [{name}]: {e}")
            failed.append(name)
        # Reset state between scenarios
        try:
            await _send(port, "kill", {})
        except Exception:
            pass
        await asyncio.sleep(0.15)

    print(f"\n{'=' * 50}")
    print(f"Results: {passed}/{len(to_run)} passed")
    if failed:
        print(f"Failed:  {', '.join(failed)}")

    if relay_proc:
        relay_proc.terminate()


if __name__ == "__main__":
    asyncio.run(main())
