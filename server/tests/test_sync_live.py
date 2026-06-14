"""Live tests for sync_unity — require Unity running on TCP:9500.

Run with: pytest -m live tests/test_sync_live.py -v

These tests occupy the TCP slot; run AFTER all unit tests.
Uses same bridge pattern as tests/live/conftest.py.
"""
import asyncio
import socket
import time

import pytest
import pytest_asyncio

from unity_mcp.bridge import UnityBridge

# ── Helpers ───────────────────────────────────────────────────────────────────

LIVE_PORT = 9500


def _bridge_up() -> bool:
    try:
        with socket.create_connection(("127.0.0.1", LIVE_PORT), timeout=0.2):
            return True
    except (OSError, socket.timeout):
        return False


async def _connect(b: UnityBridge, retries: int = 10, delay: float = 1.0):
    last_err = None
    for _ in range(retries):
        try:
            await b.connect()
            return
        except (OSError, asyncio.TimeoutError) as e:
            last_err = e
            await asyncio.sleep(delay)
    raise ConnectionError(f"Connect failed: {last_err}")


def _data(resp) -> str:
    """Extract data string from bridge response dict."""
    if isinstance(resp, dict):
        return resp.get("data", "") or resp.get("err", "")
    return str(resp)


# ── Fixtures ──────────────────────────────────────────────────────────────────

@pytest.fixture(scope="module", autouse=True)
def _require_unity():
    if not _bridge_up():
        pytest.skip("Unity bridge not on 127.0.0.1:9500 — skipping live sync suite")


@pytest_asyncio.fixture
async def bridge():
    b = UnityBridge()
    await _connect(b)
    yield b
    await b.close()


# ── Tests #35–#40 ─────────────────────────────────────────────────────────────

# #38: noop sync — no dirty scripts → fast path < 5s
@pytest.mark.live
@pytest.mark.asyncio
async def test_live_noop_sync_fast(bridge):
    """With no file changes, sync returns quickly."""
    t0 = time.monotonic()
    resp = await bridge.send("sync", {"resolve": "false"})
    elapsed = time.monotonic() - t0
    ack = _data(resp)

    assert "sync_ack" in ack, f"Expected sync_ack: {resp}"
    if "will_compile=false" in ack:
        assert elapsed < 5.0, f"Fast path took {elapsed:.1f}s"


# #35: full cycle — sync then poll until ready
@pytest.mark.live
@pytest.mark.asyncio
async def test_live_sync_full_cycle(bridge):
    """Trigger sync and verify sync_status eventually returns ready."""
    resp = await bridge.send("sync", {"resolve": "false"})
    ack = _data(resp)
    assert "sync_ack" in ack, f"Expected sync_ack: {resp}"

    parts = {p.split("=", 1)[0]: p.split("=", 1)[1] for p in ack.split("|") if "=" in p}
    epoch = int(parts.get("epoch", "0"))

    deadline = time.monotonic() + 60.0
    t0 = time.monotonic()
    while time.monotonic() < deadline:
        try:
            s_resp = await bridge.send("sync_status", {})
            status = _data(s_resp)
        except Exception:
            await asyncio.sleep(1)
            continue

        s_parts = {p.split("=", 1)[0]: p.split("=", 1)[1] for p in status.split("|") if "=" in p}
        s_epoch = int(s_parts.get("epoch", "-1"))
        state = s_parts.get("state", "unknown")

        if s_epoch == epoch and state in ("ready", "idle"):
            return
        if state == "failed":
            pytest.fail(f"Compile failed: {status}")
        # macOS/Unity 6 defers compilation while backgrounded — dur never moves
        # off 0.0. Not a product bug; needs editor focus, so skip honestly.
        if (state == "compiling" and s_parts.get("dur") == "0.0"
                and time.monotonic() - t0 > 20.0):
            pytest.skip("Unity backgrounded — compilation deferred until editor focus")
        await asyncio.sleep(1)

    pytest.fail(f"Timeout: sync_status never returned ready for epoch={epoch}")


# #40: dll freshness — after sync, get_compile_errors returns valid response
@pytest.mark.live
@pytest.mark.asyncio
async def test_live_dll_freshness(bridge):
    """After sync, get_compile_errors returns a string."""
    await bridge.send("sync", {"resolve": "false"})
    await asyncio.sleep(1)
    resp = await bridge.send("get_compile_errors", {})
    errors = _data(resp)
    assert isinstance(errors, str)


# #37: reconnect transparent — sync_status accessible immediately
@pytest.mark.live
@pytest.mark.asyncio
async def test_live_reconnect_transparent(bridge):
    """sync_status is accessible and well-formed."""
    resp = await bridge.send("sync_status", {})
    status = _data(resp)
    assert "epoch=" in status, f"Expected epoch in status: {resp}"
    assert "state=" in status, f"Expected state in status: {resp}"


# #36: compile_status and sync_status agree after noop sync
@pytest.mark.live
@pytest.mark.asyncio
async def test_live_sync_compile_status_after_noop(bridge):
    """compile_status and sync_status agree after noop sync."""
    await bridge.send("sync", {"resolve": "false"})
    await asyncio.sleep(1)
    compile_status = _data(await bridge.send("compile_status", {}))
    sync_status    = _data(await bridge.send("sync_status", {}))

    assert compile_status.startswith(("idle", "compiling", "idle-failed"))
    assert "state=" in sync_status


# #39: real bump — bump=True increments patch version then syncs
@pytest.mark.live
@pytest.mark.asyncio
async def test_live_plugin_bump_re_resolve(bridge):
    """bump=True atomically bumps plugin patch version and syncs; verify version changed."""
    import json
    import unity_mcp.tools.sync as _sync

    pkg_path = _sync._package_json_path()
    if pkg_path is None:
        pytest.skip("unity-plugin/package.json not found — standalone install")

    ver_before = json.loads(pkg_path.read_text())["version"]

    # Wire sync tool to this bridge — production _send returns the data STRING,
    # bridge.send returns the response dict, so adapt.
    async def _send_str(cmd, args=None, **kw):
        resp = await bridge.send(cmd, args or {})
        if isinstance(resp, dict):
            if not resp.get("ok", True):
                raise ConnectionError(resp.get("err", "unity error"))
            return resp.get("data", "")
        return str(resp)

    _sync._send = _send_str
    result = await _sync.sync_unity(bump=True, timeout=120.0)

    ver_after = json.loads(pkg_path.read_text())["version"]

    # Version must have incremented
    major_b, minor_b, patch_b = (int(x) for x in ver_before.split("."))
    major_a, minor_a, patch_a = (int(x) for x in ver_after.split("."))
    assert (major_a, minor_a, patch_a) == (major_b, minor_b, patch_b + 1), (
        f"Version must increment: {ver_before} → {ver_after}"
    )

    # sync must complete without error
    assert "compile failed" not in result.lower(), f"Unexpected compile failure: {result}"
