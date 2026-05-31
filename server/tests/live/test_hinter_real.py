"""Verify ToolHinter fires [HINT: after repeated get_component calls against real Unity."""
import pytest

from unity_mcp.middleware import Middleware, wrap_send
from unity_mcp.hinter import ToolHinter

pytestmark = pytest.mark.live


@pytest.mark.asyncio
async def test_three_get_component_emits_hint(wrapped_bridge, sandbox, hinter_enabled):
    """Three consecutive get_component calls via middleware must append [HINT:...inspect..."""
    mw = Middleware()
    mw.hinter = ToolHinter(enabled=True)
    # Wrap the raw UnityBridge.send to avoid double-firing middleware side-effects.
    wrapped = wrap_send(wrapped_bridge._raw.send, mw)

    # Use different component types to avoid retry-watchdog short-circuit,
    # while still triggering the inspect-loop hinter pattern (3× get_component).
    r1 = await wrapped("get_component", {"path": sandbox, "type": "Transform"})
    r2 = await wrapped("get_component", {"path": sandbox, "type": "MeshFilter"})
    r3 = await wrapped("get_component", {"path": sandbox, "type": "Rigidbody"})

    # After 3rd call the hinter pattern should fire
    assert "[HINT:" in r3, f"Expected [HINT: after 3× get_component; got:\n{r3[:400]}"
    assert "inspect" in r3.lower(), (
        f"Hint should mention 'inspect' tool; got:\n{r3[:400]}"
    )
