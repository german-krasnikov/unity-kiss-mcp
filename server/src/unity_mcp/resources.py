"""MCP Resources — live Unity context exposed as resource URIs."""
from .console_levels import PROBLEM_LEVELS

_send = None


async def _safe_send(cmd: str, args: dict) -> str:
    try:
        return await _send(cmd, args)
    except Exception as e:
        return f"[disconnected: {e}]"


async def scene_hierarchy() -> str:
    """Current scene hierarchy summary."""
    return await _safe_send("get_hierarchy", {"summary": "true"})


async def console_errors() -> str:
    """Recent console errors."""
    return await _safe_send("get_console", {"count": 20, "level": PROBLEM_LEVELS})


async def editor_state() -> str:
    """Editor state: play mode, scene, selection."""
    return await _safe_send("editor", {"action": "state"})


async def tool_categories() -> str:
    """Available tool categories."""
    from .tools.gating import get_categories
    return "\n".join(f"{k}: {', '.join(sorted(v))}" for k, v in get_categories().items())


def register(mcp, send, args) -> None:
    global _send
    _send = send
    mcp.resource("unity://scene/hierarchy")(scene_hierarchy)
    mcp.resource("unity://console/errors")(console_errors)
    mcp.resource("unity://editor/state")(editor_state)
    mcp.resource("unity://tools/categories")(tool_categories)
