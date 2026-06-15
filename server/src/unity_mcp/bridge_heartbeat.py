import asyncio
import json
import struct
import time

from unity_mcp.bridge_socket import DomainReloadError


class ProtocolDesyncError(ConnectionError):
    """Raised when the heartbeat receives an ID-mismatched ping response.

    Unlike a dead-process detection, this indicates the TCP stream is desynchronised
    (in-flight responses crossed). The connection should be drained and re-established
    without voting the Unity process as dead — is_pid_alive must be consulted first.
    """


# P7: hard deadline = 5× STARTUP_GRACE_S. Latches even while busy so a truly
# stuck reconnect loop eventually gives up without waiting for PID death.
HARD_DEADLINE_S: float = 450.0


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
            # Track cumulative disconnected time for startup-grace deadline.
            if self._reconnect_started_at is None:
                self._reconnect_started_at = time.monotonic()

            busy = self._probe_busy()
            if busy:
                self._reconnect_started_at = time.monotonic()
            wait = 5.0 if busy else 2.0
            await asyncio.sleep(wait)

            elapsed = time.monotonic() - self._reconnect_started_at

            # P7: hard deadline — latches even while busy; prevents eternal reconnect loop.
            if elapsed > HARD_DEADLINE_S:
                self._startup_grace_expired = True
                if hasattr(self, "_on_unavailable") and self._on_unavailable:
                    self._on_unavailable()
                return

            # Check grace deadline: if elapsed > STARTUP_GRACE_S and not busy,
            # stop silently looping — next send() will surface the STOP error.
            import unity_mcp.bridge as _bm  # lazy to avoid circular at module level
            if elapsed > _bm.STARTUP_GRACE_S and not busy:
                self._startup_grace_expired = True
                return

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
        except ProtocolDesyncError:
            # ID mismatch = stream desync, not necessarily process death.
            # Drain and reconnect without voting the process dead.
            async with self._lock:
                await self.close()
            self._ping_failures = 0
        except Exception:
            self._ping_failures += 1
            if self._ping_failures >= 3:
                if self._probe.is_process_dead():
                    # Confirmed dead — close.
                    async with self._lock:
                        await self.close()
                else:
                    # Still alive but ping failing — desync, not death; drain.
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
                # P7: ID mismatch = TCP stream desync, not process death.
                raise ProtocolDesyncError(
                    f"Heartbeat ID mismatch: {resp.get('id')} != {ping_id}"
                )
