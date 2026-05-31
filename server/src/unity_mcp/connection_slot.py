"""Single-slot Unity connection holder. One bridge, optional port switch."""
import asyncio
from typing import Optional
from .bridge import UnityBridge


class ConnectionSlot:
    def __init__(self):
        self._bridge: Optional[UnityBridge] = None
        self._port: int = 9500
        self._host: str = "127.0.0.1"
        self._reconnect_callbacks: list = []

    @property
    def bridge(self) -> Optional[UnityBridge]:
        return self._bridge

    @property
    def connected(self) -> bool:
        return self._bridge is not None and self._bridge.connected

    @property
    def port(self) -> int:
        return self._port

    def add_reconnect_callback(self, cb) -> None:
        """Register a callback to be wired on every new bridge (survives reconnect)."""
        self._reconnect_callbacks.append(cb)

    async def connect(self, port: int, host: str = "127.0.0.1") -> str:
        if self._bridge is not None:
            self._bridge.stop_heartbeat()
            await self._bridge.close()
        self._bridge = UnityBridge(host, port)
        self._port = port
        self._host = host
        for cb in self._reconnect_callbacks:
            self._bridge.add_reconnect_callback(cb)
        try:
            await self._bridge.connect()
            self._bridge.start_heartbeat()
            return f"Connected to Unity on port {port}"
        except (OSError, asyncio.TimeoutError):
            return f"Registered Unity on port {port} (not yet available)"

    async def close(self):
        if self._bridge:
            self._bridge.stop_heartbeat()
            await self._bridge.close()
            self._bridge = None
