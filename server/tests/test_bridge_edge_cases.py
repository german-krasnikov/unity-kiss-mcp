"""Edge cases tests for UnityBridge."""
import asyncio
import json
import struct
from unittest.mock import AsyncMock, Mock, patch, MagicMock
import pytest
from unity_mcp.bridge import UnityBridge
from helpers import make_writer, make_idle_probe, ping_response


@pytest.mark.asyncio
async def test_send_empty_args(mock_connection):
    """send() works with empty args dict."""
    mock_reader, mock_writer = mock_connection

    response = {"id": "0001", "ok": True, "data": "OK"}
    resp_payload = json.dumps(response).encode("utf-8")
    resp_header = struct.pack("!I", len(resp_payload))

    mock_reader.readexactly = AsyncMock(side_effect=[resp_header, resp_payload])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()

        result = await bridge.send("test_cmd", {})

        assert result["ok"] is True
        written = mock_writer.write.call_args[0][0]
        payload = written[4:]
        message = json.loads(payload.decode("utf-8"))
        assert message["args"] == {}


@pytest.mark.asyncio
async def test_send_unicode_args(mock_connection):
    """send() handles unicode (cyrillic, emoji) in args."""
    mock_reader, mock_writer = mock_connection

    response = {"id": "0001", "ok": True, "data": "OK"}
    resp_payload = json.dumps(response).encode("utf-8")
    resp_header = struct.pack("!I", len(resp_payload))

    mock_reader.readexactly = AsyncMock(side_effect=[resp_header, resp_payload])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()

        result = await bridge.send("test", {"name": "Объект 😀", "text": "Привет"})

        assert result["ok"] is True
        written = mock_writer.write.call_args[0][0]
        payload = written[4:]
        message = json.loads(payload.decode("utf-8"))
        assert message["args"]["name"] == "Объект 😀"


@pytest.mark.asyncio
async def test_read_response_invalid_json(mock_connection, monkeypatch):
    """Invalid JSON payload → ConnectionError (caught and retried like corrupt frame)."""
    import unity_mcp.bridge as bmod
    monkeypatch.setattr(bmod, "SESSION_TIMEOUT", 0.1)
    mock_reader, mock_writer = mock_connection

    bad_payload = b"not json"
    header = struct.pack("!I", len(bad_payload))

    mock_reader.readexactly = AsyncMock(side_effect=[header, bad_payload])

    idle_probe = make_idle_probe()
    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test", {})


@pytest.mark.asyncio
async def test_read_response_zero_length(mock_connection, monkeypatch):
    """Zero-length payload → ConnectionError (caught and retried like corrupt frame)."""
    import unity_mcp.bridge as bmod
    monkeypatch.setattr(bmod, "SESSION_TIMEOUT", 0.1)
    mock_reader, mock_writer = mock_connection

    header = struct.pack("!I", 0)
    empty_payload = b""

    mock_reader.readexactly = AsyncMock(side_effect=[header, empty_payload])

    idle_probe = make_idle_probe()
    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test", {})


@pytest.mark.asyncio
async def test_read_response_exactly_10mb(mock_connection):
    """_read_response accepts exactly 10MB (boundary)."""
    mock_reader, mock_writer = mock_connection

    response = {"id": "0001", "ok": True, "data": "OK"}
    resp_payload = json.dumps(response).encode("utf-8")
    header = struct.pack("!I", 10_000_000)

    mock_reader.readexactly = AsyncMock(side_effect=[header, resp_payload])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()

        result = await bridge.send("test", {})
        assert result["ok"] is True


@pytest.mark.asyncio
async def test_read_response_exceeds_10mb(mock_connection, monkeypatch):
    """Oversized message (>10MB) raises ConnectionError."""
    import unity_mcp.bridge as bmod
    monkeypatch.setattr(bmod, "SESSION_TIMEOUT", 0.1)
    mock_reader, mock_writer = mock_connection

    idle_probe = make_idle_probe()

    header = struct.pack("!I", 10_000_001)
    mock_reader.readexactly = AsyncMock(side_effect=[header])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test", {})


@pytest.mark.asyncio
async def test_send_rejects_oversized_payload(mock_connection):
    """send() raises ConnectionError before writing if payload > 10MB."""
    mock_reader, mock_writer = mock_connection

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()

        big_args = {"data": "x" * 10_000_001}
        with pytest.raises(ConnectionError, match="too large"):
            await bridge.send("test", big_args)


@pytest.mark.asyncio
async def test_disconnect_during_header_read(mock_connection, monkeypatch):
    """IncompleteReadError during header read → ConnectionError after grace retry."""
    import unity_mcp.bridge as bmod
    monkeypatch.setattr(bmod, "SESSION_TIMEOUT", 0.1)
    mock_reader, mock_writer = mock_connection

    mock_reader.readexactly = AsyncMock(side_effect=[
        asyncio.IncompleteReadError(b"", 4),
    ])

    idle_probe = make_idle_probe()
    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test", {})


@pytest.mark.asyncio
async def test_disconnect_during_payload_read(mock_connection, monkeypatch):
    """IncompleteReadError during payload read → ConnectionError after grace retry."""
    import unity_mcp.bridge as bmod
    monkeypatch.setattr(bmod, "SESSION_TIMEOUT", 0.1)
    mock_reader, mock_writer = mock_connection

    response = {"id": "0001", "ok": True, "data": "OK"}
    resp_payload = json.dumps(response).encode("utf-8")
    resp_header = struct.pack("!I", len(resp_payload))

    mock_reader.readexactly = AsyncMock(side_effect=[
        resp_header,
        asyncio.IncompleteReadError(b"", len(resp_payload)),
    ])

    idle_probe = make_idle_probe()
    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test", {})


@pytest.mark.asyncio
async def test_concurrent_sends(mock_connection):
    """Multiple concurrent send() calls are serialized by lock."""
    mock_reader, mock_writer = mock_connection

    def make_response(msg_id):
        resp = {"id": msg_id, "ok": True, "data": f"Response {msg_id}"}
        payload = json.dumps(resp).encode("utf-8")
        header = struct.pack("!I", len(payload))
        return [header, payload]

    responses = []
    for i in range(1, 6):
        responses.extend(make_response(f"{i:04x}"))

    mock_reader.readexactly = AsyncMock(side_effect=responses)

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()

        tasks = [bridge.send(f"cmd{i}", {}) for i in range(5)]
        results = await asyncio.gather(*tasks)

        assert len(results) == 5
        for result in results:
            assert result["ok"] is True


@pytest.mark.asyncio
async def test_reconnect_preserves_counter(mock_connection):
    """_reconnect() does not reset _counter."""
    mock_reader, mock_writer = mock_connection

    response1 = {"id": "0001", "ok": True, "data": "OK"}
    resp1_payload = json.dumps(response1).encode("utf-8")
    resp1_header = struct.pack("!I", len(resp1_payload))

    # _reconnect() increments counter once for ping ID (rc0002), so cmd2 gets 0003
    response2 = {"id": "0003", "ok": True, "data": "OK"}
    resp2_payload = json.dumps(response2).encode("utf-8")
    resp2_header = struct.pack("!I", len(resp2_payload))

    ping_hdr, ping_pay = ping_response()
    mock_reader.readexactly = AsyncMock(side_effect=[
        resp1_header, resp1_payload,
        ping_hdr, ping_pay,
        resp2_header, resp2_payload,
    ])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()

        await bridge.send("cmd1", {})
        assert bridge._counter == 1

        await bridge._reconnect()
        assert bridge._counter == 2  # ping consumed counter slot

        await bridge.send("cmd2", {})
        assert bridge._counter == 3


@pytest.mark.asyncio
async def test_close_when_not_connected():
    """close() when never connected does not crash."""
    bridge = UnityBridge()
    await bridge.close()  # Should not raise


@pytest.mark.asyncio
async def test_close_when_writer_fails(mock_connection):
    """close() handles exception in wait_closed."""
    mock_reader, mock_writer = mock_connection

    mock_writer.wait_closed = AsyncMock(side_effect=Exception("Close failed"))

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()

        await bridge.close()  # Should not raise

        assert bridge._writer is None
        assert bridge._reader is None


@pytest.mark.asyncio
async def test_send_after_close_auto_reconnects():
    """send() after close() auto-reconnects instead of crashing."""
    response = {"id": "0001", "ok": True, "data": "OK"}
    resp_payload = json.dumps(response).encode("utf-8")
    resp_header = struct.pack("!I", len(resp_payload))

    ping_hdr, ping_pay = ping_response()
    new_reader = AsyncMock()
    new_reader.readexactly = AsyncMock(side_effect=[ping_hdr, ping_pay, resp_header, resp_payload])
    new_writer = make_writer()

    first_reader = AsyncMock()
    first_writer = make_writer()

    with patch("asyncio.open_connection", side_effect=[
        (first_reader, first_writer),
        (new_reader, new_writer),
    ]):
        bridge = UnityBridge()
        await bridge.connect()
        await bridge.close()

        assert not bridge.connected
        result = await bridge.send("test", {})
        assert result["ok"] is True
        assert bridge.connected


@pytest.mark.asyncio
async def test_send_auto_reconnects_when_never_connected():
    """send() without prior connect() auto-reconnects."""
    response = {"id": "0001", "ok": True, "data": "OK"}
    resp_payload = json.dumps(response).encode("utf-8")
    resp_header = struct.pack("!I", len(resp_payload))

    ping_hdr, ping_pay = ping_response()
    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=[ping_hdr, ping_pay, resp_header, resp_payload])
    writer = make_writer()

    with patch("asyncio.open_connection", return_value=(reader, writer)) as mock_open:
        bridge = UnityBridge()
        # Never call connect() — send() should handle it
        assert not bridge.connected

        result = await bridge.send("test", {})

        assert result["ok"] is True
        assert bridge.connected
        # open_connection called once by auto-reconnect
        mock_open.assert_called_once()


@pytest.mark.asyncio
async def test_connected_property(mock_connection):
    """connected property reflects actual state."""
    mock_reader, mock_writer = mock_connection

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()

        assert not bridge.connected

        await bridge.connect()
        assert bridge.connected

        mock_writer.is_closing = Mock(return_value=True)
        assert not bridge.connected

        await bridge.close()
        assert not bridge.connected


@pytest.mark.asyncio
async def test_max_retries_exhausted(mock_connection):
    """send() raises ConnectionError on IncompleteReadError (circuit breaker, no retry)."""
    mock_reader, mock_writer = mock_connection

    mock_reader.readexactly = AsyncMock(side_effect=asyncio.IncompleteReadError(b"", 4))

    idle_probe = make_idle_probe()

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((asyncio.IncompleteReadError, ConnectionError)):
            await bridge.send("test", {})


@pytest.mark.asyncio
async def test_send_timeout_raises():
    """send() raises ConnectionError when Unity doesn't respond (circuit breaker, no retry)."""
    idle_probe = make_idle_probe()

    def create_hanging_mock():
        reader = AsyncMock()
        writer = make_writer()
        async def hang(n):
            await asyncio.Future()  # never resolves
        reader.readexactly = AsyncMock(side_effect=hang)
        return (reader, writer)

    with patch("unity_mcp.bridge.asyncio.open_connection",
               return_value=create_hanging_mock()):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((TimeoutError, ConnectionError), match="Unity not responding"):
            await bridge.send("test", {}, timeout=0.01)


@pytest.mark.asyncio
async def test_concurrent_sends_with_dead_connection():
    """Concurrent sends when writer=None don't race on reconnect.

    Design note: counter increment is sync (outside I/O lock) so IDs are
    assigned before any task yields. I/O lock serialises reconnect + write +
    read. The mock echoes whatever ID it received, making the test
    order-independent — no hardcoded "0001"/"0002"/"0003".
    """
    # Queue of pre-built (header, payload) response pairs, filled on drain()
    response_queue: asyncio.Queue = asyncio.Queue()
    # Partial state for readexactly: None means next call is a header read
    pending_payload: list[bytes | None] = [None]

    async def fake_readexactly(n: int) -> bytes:
        if pending_payload[0] is not None:
            # Second call in pair: return the payload
            pay = pending_payload[0]
            pending_payload[0] = None
            return pay
        # First call in pair (n==4): get next response pair from queue
        hdr, pay = await response_queue.get()
        pending_payload[0] = pay
        return hdr

    frames_buf: list[bytes] = []

    def fake_write(data: bytes) -> None:
        frames_buf.append(data)

    async def fake_drain() -> None:
        # Parse accumulated writes, build echo responses, enqueue them
        buf = b"".join(frames_buf)
        frames_buf.clear()
        offset = 0
        while offset + 4 <= len(buf):
            length = struct.unpack("!I", buf[offset:offset + 4])[0]
            offset += 4
            if offset + length > len(buf):
                break
            frame = buf[offset:offset + length]
            offset += length
            msg = json.loads(frame)
            resp = {"id": msg["id"], "ok": True, "data": "pong" if msg.get("cmd") == "ping" else "OK"}
            pay = json.dumps(resp).encode("utf-8")
            await response_queue.put((struct.pack("!I", len(pay)), pay))

    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=fake_readexactly)
    writer = make_writer()
    writer.write = Mock(side_effect=fake_write)
    writer.drain = AsyncMock(side_effect=fake_drain)

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        assert not bridge.connected

        # Fire 3 concurrent sends — all must succeed without AttributeError
        results = await asyncio.gather(
            bridge.send("cmd1", {}),
            bridge.send("cmd2", {}),
            bridge.send("cmd3", {}),
        )

        assert len(results) == 3
        for r in results:
            assert r["ok"] is True


@pytest.mark.asyncio
async def test_reconnect_inside_lock_prevents_writer_none():
    """Reconnect inside lock ensures writer is set before write()."""
    response = {"id": "0001", "ok": True, "data": "OK"}
    resp_payload = json.dumps(response).encode("utf-8")
    resp_header = struct.pack("!I", len(resp_payload))

    ping_hdr, ping_pay = ping_response()
    reader = AsyncMock()
    writer = make_writer()
    reader.readexactly = AsyncMock(side_effect=[ping_hdr, ping_pay, resp_header, resp_payload])

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        # Simulate: bridge created but never connected (lifespan failed)
        assert bridge._writer is None

        result = await bridge.send("test", {})
        assert result["ok"] is True
        # Writer must be set after auto-reconnect
        assert bridge._writer is not None


@pytest.mark.asyncio
async def test_response_id_mismatch_raises(mock_connection):
    """Mismatched ID → ConnectionError('Response ID mismatch') immediately."""
    wrong_response = {"id": "wrong_id", "ok": True, "data": "x"}

    idle_probe = make_idle_probe()

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()
        with patch.object(bridge, "_read_response", AsyncMock(return_value=wrong_response)):
            with pytest.raises(ConnectionError, match="Response ID mismatch"):
                await bridge.send("test", {})


@pytest.mark.asyncio
async def test_probe_raises_does_not_crash_send():
    """Probe methods that raise must not crash send() (degrade-open)."""
    from unity_mcp.compile_state import CompileStateProbe

    exploding_probe = MagicMock(spec=CompileStateProbe)
    exploding_probe.is_unity_busy.side_effect = RuntimeError("probe exploded")
    exploding_probe.has_strong_busy_signal.side_effect = RuntimeError("probe exploded")
    exploding_probe.estimated_remaining_s.side_effect = RuntimeError("probe exploded")
    exploding_probe.mark_recompile_issued.side_effect = RuntimeError("probe exploded")
    exploding_probe.is_process_dead.side_effect = RuntimeError("probe exploded")
    exploding_probe.has_project = True

    async def open_connection_side_effect(*args, **kwargs):
        reader = AsyncMock()
        writer = make_writer()
        async def hang(n):
            await asyncio.Future()
        reader.readexactly = AsyncMock(side_effect=hang)
        return (reader, writer)

    with patch("unity_mcp.bridge.asyncio.open_connection",
               side_effect=open_connection_side_effect):
        bridge = UnityBridge(probe=exploding_probe)
        await bridge.connect()

        with pytest.raises((TimeoutError, ConnectionError), match="Unity not responding"):
            await bridge.send("test", {}, timeout=0.01)


@pytest.mark.asyncio
async def test_concurrent_sends_routes_per_caller(mock_unity_server):
    """Each caller's response correctly identified by msg_id (not just 'all return ok')."""
    bridge = UnityBridge(host="127.0.0.1", port=mock_unity_server.port)
    await bridge.connect()

    results = await asyncio.gather(*[
        bridge.send(f"cmd_{i}", {"marker": f"m{i}"}) for i in range(5)
    ])

    # mock_unity_server echoes as "echo:cmd_i" in data field
    for i, r in enumerate(results):
        assert f"cmd_{i}" in str(r), f"Caller {i} got wrong response: {r}"

    await bridge.close()
