"""Shared test helpers."""
from unittest.mock import AsyncMock, MagicMock, Mock


def make_mock_bridge(connected: bool = True):
    """Create a minimal UnityBridge mock with all required async methods."""
    b = MagicMock()
    b.connect = AsyncMock()
    b.close = AsyncMock()
    b.send = AsyncMock(return_value={"ok": True})
    b.connected = connected
    b.stop_heartbeat = MagicMock()
    return b


def make_writer():
    """Create a fresh writer mock with all required async/sync methods."""
    writer = AsyncMock()
    writer.write = Mock()
    writer.close = Mock()
    writer.wait_closed = AsyncMock()
    writer.drain = AsyncMock()
    writer.is_closing = Mock(return_value=False)
    writer.get_extra_info = Mock(return_value=None)  # no real socket in tests
    return writer


def make_idle_probe():
    """CompileStateProbe mock that reports idle (not busy). Required fields
    for _should_give_up: has_strong_busy_signal=False, has_project=True."""
    from unity_mcp.compile_state import CompileStateProbe
    p = MagicMock(spec=CompileStateProbe)
    p.is_unity_busy.return_value = False
    p.has_strong_busy_signal.return_value = False
    p.is_process_dead.return_value = False
    p.estimated_remaining_s.return_value = 5.0
    p.has_project = True
    p.mark_recompile_issued = MagicMock()
    return p


def ping_response():
    """Returns (header, payload) for a ping/pong response — needed by _reconnect."""
    import json, struct
    r = {"id": "ping", "ok": True, "data": "pong"}
    p = json.dumps(r).encode()
    return struct.pack("!I", len(p)), p


def version_response(proto: int = 3):
    """Returns (header, payload) for a get_version response — needed by _reconnect."""
    import json, struct
    r = {"id": "ver", "ok": True, "data": f"proto:{proto}|plugin:test|stamp:test"}
    p = json.dumps(r).encode()
    return struct.pack("!I", len(p)), p


def reconnect_preamble(proto: int = 3):
    """Returns flat list of bytes chunks for _reconnect: ping_hdr, ping_pay, ver_hdr, ver_pay."""
    ph, pp = ping_response()
    vh, vp = version_response(proto)
    return [ph, pp, vh, vp]


def csharp_created(path: str) -> str:
    """Returns 'Created {path}' (no-parent form).

    Production also emits 'Created {path}\\n--- parent ---\\n{subtree}' when
    created with a parent — not covered by this helper. Use raw string for that case.
    """
    return f"Created {path}"


def csharp_schema(name: str, fields: dict) -> str:
    body = "\n".join(f"  {k}: {v}" for k, v in fields.items())
    return f"Schema: {name}\n{body}\n"


def csharp_runtime_field(k: str, v) -> str:
    return f"{k}={v}"
