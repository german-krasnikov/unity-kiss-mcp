import asyncio
import json
import os
import select
import socket
import struct
import time
from typing import Callable, Optional

from unity_mcp.bridge_socket import (
    DomainReloadError,
    _apply_socket_options,
    _TCP_KEEPALIVE_DARWIN,
    _TCP_KEEPINTVL_DARWIN,
    _TCP_KEEPCNT_DARWIN,
)
from unity_mcp.bridge_heartbeat import HeartbeatMixin
from unity_mcp.compile_state import CompileStateProbe
from unity_mcp.crash_log import CrashLogger
from unity_mcp.metrics import METRICS

# Re-export so existing `from .bridge import DomainReloadError` keeps working
__all__ = [
    "UnityBridge", "DomainReloadError",
    "MIN_RECONNECT_INTERVAL",
    "_apply_socket_options",
    "_TCP_KEEPALIVE_DARWIN", "_TCP_KEEPINTVL_DARWIN",
]

CONNECT_TIMEOUT = float(os.environ.get("UNITY_MCP_CONNECT_TIMEOUT", "5.0"))
SESSION_TIMEOUT = float(os.environ.get("UNITY_MCP_SESSION_TIMEOUT", "120.0"))
MAX_RETRIES = int(os.environ.get("UNITY_MCP_MAX_RETRIES", "3"))
MIN_RECONNECT_INTERVAL = float(os.environ.get("UNITY_MCP_MIN_RECONNECT_INTERVAL", "2.0"))


class UnityBridge(HeartbeatMixin):
    """TCP client for Unity Editor communication."""

    def __init__(self, host: str = "127.0.0.1", port: Optional[int] = None,
                 probe: Optional[CompileStateProbe] = None,
                 port_discoverer: Optional[Callable[[], int]] = None):
        self._host = host
        try:
            self._port = port or int(os.environ.get("UNITY_MCP_PORT", "9500"))
        except ValueError:
            self._port = 9500
        self._reader = None
        self._writer = None
        self._counter = 0
        self._lock = asyncio.Lock()
        self._probe: CompileStateProbe = probe if probe is not None else CompileStateProbe(
            CompileStateProbe.autodetect_project_path(), port=self._port
        )
        self._first_failure_ts: Optional[float] = None
        self._on_reconnect_callbacks: list = []
        self._crash_log = CrashLogger()
        self._heartbeat_task: Optional[asyncio.Task] = None
        self._heartbeat_interval: float = 15.0
        self._ping_failures: int = 0
        self._last_reconnect_at: float = 0.0
        self._min_reconnect_interval: float = MIN_RECONNECT_INTERVAL
        self._port_discoverer: Optional[Callable[[], int]] = port_discoverer

    def add_reconnect_callback(self, fn) -> None:
        self._on_reconnect_callbacks.append(fn)

    async def connect(self):
        self._reader, self._writer = await asyncio.wait_for(
            asyncio.open_connection(self._host, self._port),
            timeout=CONNECT_TIMEOUT,
        )
        _apply_socket_options(self._writer.get_extra_info("socket"))

    def _probe_busy(self) -> bool:
        try:
            return self._probe.has_strong_busy_signal()
        except Exception:
            return False

    def _ensure_heartbeat(self) -> None:
        """Restart heartbeat if task died unexpectedly."""
        if self._heartbeat_task is not None and self._heartbeat_task.done():
            self._heartbeat_task = asyncio.ensure_future(self._heartbeat_loop(self._heartbeat_interval))

    async def send(self, cmd: str, args: dict, timeout: float = 30.0) -> dict:
        self._ensure_heartbeat()
        self._counter += 1
        msg_id = f"{self._counter:04x}"
        payload = json.dumps({"id": msg_id, "cmd": cmd, "args": args}).encode("utf-8")
        if len(payload) > 10_000_000:
            raise ConnectionError(f"Outbound payload too large: {len(payload)} bytes (max 10MB)")
        header = struct.pack("!I", len(payload))
        session_deadline = time.monotonic() + SESSION_TIMEOUT

        attempt = 0
        result = None
        while attempt <= MAX_RETRIES:
            if time.monotonic() > session_deadline:
                raise TimeoutError(f"Session deadline ({SESSION_TIMEOUT}s) exceeded")

            try:
                async with self._lock:
                    if not self.connected:
                        await self._reconnect()
                    self._writer.write(header + payload)
                    await self._writer.drain()
                    result = await asyncio.wait_for(
                        self._read_response(), timeout=timeout)
            except (ConnectionRefusedError, asyncio.TimeoutError, ConnectionError,
                    asyncio.IncompleteReadError, OSError, json.JSONDecodeError,
                    RuntimeError) as e:
                async with self._lock:
                    await self.close()
                if self._first_failure_ts is None:
                    self._first_failure_ts = time.monotonic()
                if isinstance(e, DomainReloadError):
                    self._probe.mark_recompile_issued()
                busy = isinstance(e, DomainReloadError) or self._probe_busy()
                self._crash_log.log_disconnect(cmd=cmd, retry=attempt,
                                               error_type=type(e).__name__,
                                               unity_busy=busy, port=self._port)
                # Retry: only if busy (domain reload) OR first grace attempt
                if attempt < MAX_RETRIES and (busy or attempt < 1):
                    if time.monotonic() >= session_deadline:
                        raise TimeoutError(f"Session deadline ({SESSION_TIMEOUT}s) exceeded") from e
                    attempt += 1
                    delay = min(2 ** attempt, 8.0) if busy else 1.0
                    await asyncio.sleep(delay)
                    continue
                raise ConnectionError(self._describe_failure(cmd, e)) from e

            if result.get("id") != msg_id:
                async with self._lock:
                    await self.close()
                raise ConnectionError(
                    f"Response ID mismatch: expected {msg_id}, got {result.get('id')}")

            # Unity retry hint (compilation busy)
            if not result.get("ok") and result.get("retry"):
                if attempt < MAX_RETRIES:
                    await asyncio.sleep(result["retry"] / 1000)
                    attempt += 1
                    continue
                return result

            if self._first_failure_ts is not None:
                outage = time.monotonic() - self._first_failure_ts
                METRICS.observe("recompile.duration_ms", outage * 1000)
                self._crash_log.log_reconnect(outage_s=outage, retries=attempt,
                                              port=self._port)
                self._first_failure_ts = None
            return result
        return result  # type: ignore[return-value]

    def _describe_failure(self, cmd: str, exc: Exception) -> str:
        try:
            if self._probe.is_process_dead():
                return f"Unity crashed (process dead). Restart Unity. Port :{self._port}"
        except Exception:
            pass
        try:
            if self._probe.is_unity_busy():
                rem = self._probe.estimated_remaining_s()
                return (f"Unity busy: C# compilation/domain reload in progress "
                        f"(~{rem:.0f}s left). Retry in a moment.")
        except Exception:
            pass
        return f"Unity not responding (process dead? port wrong?). Check :{self._port}."

    async def _read_response(self) -> dict:
        header = await self._reader.readexactly(4)
        length = struct.unpack("!I", header)[0]
        if length == 0 or length > 10_000_000:
            raise ConnectionError(
                f"Protocol desync: length prefix {length} (0x{length:08X}) — reconnecting"
            )
        payload = await self._reader.readexactly(length)
        data = json.loads(payload.decode("utf-8"))
        if data.get("ev") == "going_away":
            raise DomainReloadError(f"Unity domain reload: {data.get('reason', 'unknown')}")
        return data

    async def _reconnect(self):
        await self.close()
        if self._port_discoverer is not None:
            try:
                new_port = self._port_discoverer()
                if new_port != self._port:
                    self._port = new_port
                    self._probe = CompileStateProbe(
                        CompileStateProbe.autodetect_project_path(), port=new_port)
            except Exception:
                pass
        reader, writer = await asyncio.wait_for(
            asyncio.open_connection(self._host, self._port),
            timeout=CONNECT_TIMEOUT,
        )
        _apply_socket_options(writer.get_extra_info("socket"))
        try:
            self._counter += 1
            ping_id = f"rc{self._counter:04x}"
            ping = json.dumps({"id": ping_id, "cmd": "ping", "args": {}}).encode("utf-8")
            writer.write(struct.pack("!I", len(ping)) + ping)
            await writer.drain()
            # Temporarily wire reader so _read_response works
            self._reader = reader
            pong = await asyncio.wait_for(self._read_response(), timeout=CONNECT_TIMEOUT)
            if not pong.get("ok"):
                raise ConnectionError("Unity ping failed after reconnect")
        except BaseException:
            self._reader = None
            writer.close()
            try:
                await writer.wait_closed()
            except Exception:
                pass
            raise
        self._writer = writer
        self._first_failure_ts = None
        self._last_reconnect_at = time.monotonic()
        for cb in self._on_reconnect_callbacks:
            try:
                cb()
            except Exception:
                pass

    @property
    def connected(self) -> bool:
        if self._writer is None or self._writer.is_closing():
            return False
        sock = self._writer.get_extra_info("socket")
        if sock is not None:
            try:
                r, _, _ = select.select([sock], [], [], 0)
                if r:
                    data = sock.recv(1, socket.MSG_PEEK)
                    if not data:
                        return False
            except (OSError, ValueError, BlockingIOError):
                return False
        return True

    async def close(self):
        w = self._writer
        self._writer = None
        self._reader = None
        if w:
            sock = w.get_extra_info("socket")
            if sock is not None:
                try:
                    sock.shutdown(socket.SHUT_RDWR)
                except OSError:
                    pass
            w.close()
            try:
                await asyncio.wait_for(w.wait_closed(), timeout=2.0)
            except Exception:
                pass
