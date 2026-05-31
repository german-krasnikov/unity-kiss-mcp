from mcp.server.fastmcp.exceptions import ToolError
from ._annotations import RO as _RO, RW_IDEM as _RW_IDEM

_get_slot = None


async def list_connections() -> str:
    """List Unity connection status."""
    s = _get_slot() if _get_slot else None
    if s is None:
        return "No slot initialized"
    status = "connected" if s.connected else "disconnected"
    return f"port {s.port} ({status})"


async def reconnect_unity(port: int = 9500) -> str:
    """Reconnect to Unity on the given port (default 9500)."""
    s = _get_slot() if _get_slot else None
    if s is None:
        raise ToolError("Server not initialized")
    return await s.connect(port)


def register(mcp, send, args, *, get_slot, **_kw):
    global _get_slot
    _get_slot = get_slot
    mcp.tool(annotations=_RO)(list_connections)
    mcp.tool(annotations=_RW_IDEM)(reconnect_unity)
