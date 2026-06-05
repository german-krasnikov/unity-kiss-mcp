import asyncio
import json
import struct
import time

from unity_mcp.bridge_socket import DomainReloadError


class HeartbeatMixin:
    """Heartbeat loop and reconnection scheduling. No __init__.

    Expected instance attributes (set by UnityBridge.__init__):
      _heartbeat_task, _heartbeat_interval, _ping_failures,
      _last_reconnect_at, _min_reconnect_interval,
      _lock, _probe, _writer, _counter, _reader
    """

    def start_heartbeat(self, interval: float = 15.0) -> None:
        if self._heartbeat_task is not None and not self._heartbeat_task.done():
            return
        self._heartbeat_interval = interval
        self._heartbeat_task = asyncio.ensure_future(self._heartbeat_loop(interval))

    def stop_heartbeat(self) -> None:
        if self._heartbeat_task is not None:
            self._heartbeat_task.cancel()
            self._heartbeat_task = None

    async def _heartbeat_loop(self, interval: float) -> None:
        while True:
            try:
                await self._heartbeat_tick(interval)
            except asyncio.CancelledError:
                return
            except Exception:
                # Safety net: never let heartbeat task die silently.
                await asyncio.sleep(5.0)

    async def _heartbeat_tick(self, interval: float) -> None:
        """Single heartbeat iteration. Separated for safety-net wrapping."""
        if not self.connected:
            busy = self._probe_busy()
            wait = 5.0 if busy else 2.0
            await asyncio.sleep(wait)
            if self._reconnect_cooldown_ok():
                async with self._lock:
                    if self.connected:
                        return
                    try:
                        await self._reconnect()
                        self._ping_failures = 0
                    except Exception:
                        pass
            return
        await asyncio.sleep(interval)
        if self._lock.locked():
            return
        try:
            await self._raw_ping(timeout=5.0)
            self._ping_failures = 0
        except DomainReloadError:
            self._probe.mark_recompile_issued()
            async with self._lock:
                await self.close()
            self._ping_failures = 0
        except Exception:
            self._ping_failures += 1
            if self._probe.is_process_dead() or self._ping_failures >= 3:
                async with self._lock:
                    await self.close()
                self._ping_failures = 0

    def _reconnect_cooldown_ok(self) -> bool:
        """True if enough time elapsed since last reconnect."""
        return (time.monotonic() - self._last_reconnect_at) >= self._min_reconnect_interval

    async def _raw_ping(self, timeout: float = 5.0) -> None:
        """Send ping directly on socket, bypassing send() retry machinery."""
        async with self._lock:
            if not self.connected:
                raise ConnectionError("Not connected")
            self._counter += 1
            ping_id = f"hb{self._counter:04x}"
            payload = json.dumps({"id": ping_id, "cmd": "ping", "args": {}}).encode("utf-8")
            self._writer.write(struct.pack("!I", len(payload)) + payload)
            await self._writer.drain()
            resp = await asyncio.wait_for(self._read_response(), timeout=timeout)
            if resp.get("id") != ping_id:
                raise ConnectionError(f"Heartbeat ID mismatch: {resp.get('id')} != {ping_id}")
