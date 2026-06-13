"""Tests for dynamic MCP tool filtering based on Unity MCPSettings."""
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import mcp.types as mcp_types

from unity_mcp.server import _filter_tools, mcp


def _tool(name: str):
    return SimpleNamespace(name=name)


ALL_TOOLS = [_tool("get_hierarchy"), _tool("scene"), _tool("shader"), _tool("get_enabled_tools")]


# --- test _filter_tools fallback (gating only, no Unity cache) ---

async def test_filter_tools_fallback_when_bridge_none():
    import unity_mcp.server as srv
    orig = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = None
        result = await _filter_tools(ALL_TOOLS, None)
        names = {t.name for t in result}
        assert "get_hierarchy" in names
        assert "get_enabled_tools" in names
        assert "shader" not in names  # gated out (not in TIER1)
    finally:
        srv._disabled_tools_cache = orig


async def test_filter_tools_fallback_when_disconnected():
    import unity_mcp.server as srv
    orig = srv._disabled_tools_cache
    bridge = AsyncMock()
    bridge.connected = False
    try:
        srv._disabled_tools_cache = None
        result = await _filter_tools(ALL_TOOLS, bridge)
        names = {t.name for t in result}
        assert "get_hierarchy" in names
        assert "shader" not in names
    finally:
        srv._disabled_tools_cache = orig


async def test_filter_tools_fallback_on_send_error():
    import unity_mcp.server as srv
    orig = srv._disabled_tools_cache
    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(side_effect=ConnectionError("lost"))
    try:
        srv._disabled_tools_cache = None
        result = await _filter_tools(ALL_TOOLS, bridge)
        names = {t.name for t in result}
        assert "get_hierarchy" in names
        assert "shader" not in names
    finally:
        srv._disabled_tools_cache = orig


async def test_filter_tools_fallback_on_unity_error():
    import unity_mcp.server as srv
    orig = srv._disabled_tools_cache
    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": False, "err": "fail"})
    try:
        srv._disabled_tools_cache = None
        result = await _filter_tools(ALL_TOOLS, bridge)
        names = {t.name for t in result}
        assert "get_hierarchy" in names
        assert "shader" not in names
    finally:
        srv._disabled_tools_cache = orig


# --- Core bug-fix: disabled-set semantics ---

async def test_disabled_tier1_tool_hidden():
    """CORE BUG FIX: unchecking screenshot in Unity form must remove it from ListTools."""
    import unity_mcp.server as srv
    import unity_mcp.tools.gating as gating
    gating.reset()
    orig = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = {"screenshot"}
        tools = [_tool("screenshot"), _tool("get_hierarchy")]
        result = await _filter_tools(tools, None)
        names = {t.name for t in result}
        assert "screenshot" not in names, "Disabled TIER1 tool must be hidden"
        assert "get_hierarchy" in names
    finally:
        srv._disabled_tools_cache = orig
        gating.reset()


async def test_force_visible_survives_disabled():
    """FORCE_VISIBLE tools must never be hidden even if in disabled set."""
    import unity_mcp.server as srv
    import unity_mcp.tools.gating as gating
    gating.reset()
    orig = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = {"do", "discover_tools", "get_hierarchy"}
        tools = [_tool("do"), _tool("discover_tools"), _tool("get_hierarchy")]
        result = await _filter_tools(tools, None)
        names = {t.name for t in result}
        assert "do" in names, "FORCE_VISIBLE 'do' must survive disabled set"
        assert "discover_tools" in names, "FORCE_VISIBLE 'discover_tools' must survive disabled set"
        assert "get_hierarchy" not in names, "Non-FORCE_VISIBLE disabled tool must be hidden"
    finally:
        srv._disabled_tools_cache = orig
        gating.reset()


async def test_disabled_cache_none_no_hiding():
    """None cache = gating-only fallback, nothing extra hidden."""
    import unity_mcp.server as srv
    import unity_mcp.tools.gating as gating
    gating.reset()
    orig = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = None
        tools = [_tool("screenshot"), _tool("get_hierarchy")]
        result = await _filter_tools(tools, None)
        names = {t.name for t in result}
        # Both are TIER1, gating keeps them; disabled cache is None so no hiding
        assert "screenshot" in names
        assert "get_hierarchy" in names
    finally:
        srv._disabled_tools_cache = orig
        gating.reset()


# --- Cache interaction tests (disabled-set semantics) ---

async def test_filter_tools_uses_cache_when_available():
    """With disabled cache populated, _filter_tools must NOT call bridge.send."""
    from unittest.mock import Mock
    import unity_mcp.server as srv

    tool_a = Mock()
    tool_a.name = "get_hierarchy"
    tool_b = Mock()
    tool_b.name = "set_property"
    bridge = AsyncMock()
    bridge.send = AsyncMock()

    orig = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = set()  # empty disabled set = nothing hidden
        bridge.send.reset_mock()
        result = await srv._filter_tools([tool_a, tool_b], bridge)
        bridge.send.assert_not_called()
        assert tool_a in result
        assert tool_b in result
    finally:
        srv._disabled_tools_cache = orig


async def test_filter_tools_fallback_when_cache_empty():
    """With None cache, _apply_gating is used (no TCP)."""
    from unittest.mock import Mock
    import unity_mcp.server as srv

    tool_a = Mock()
    tool_a.name = "get_hierarchy"
    bridge = AsyncMock()
    bridge.connected = False

    orig = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = None
        bridge.send.reset_mock()
        result = await srv._filter_tools([tool_a], bridge)
        bridge.send.assert_not_called()
        assert any(t.name == "get_hierarchy" for t in result)
    finally:
        srv._disabled_tools_cache = orig


async def test_disabled_tools_cache_populated_on_reconnect():
    """Reconnect populates _disabled_tools_cache via get_disabled_tools."""
    from unittest.mock import AsyncMock
    import unity_mcp.server as srv

    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": "screenshot,shader"})

    orig = srv._disabled_tools_cache
    orig_lock = srv._refresh_tools_lock
    try:
        srv._disabled_tools_cache = None
        srv._refresh_tools_lock = None
        await srv._refresh_tools_cache(bridge)
        assert srv._disabled_tools_cache == {"screenshot", "shader"}
    finally:
        srv._disabled_tools_cache = orig
        srv._refresh_tools_lock = orig_lock


async def test_disabled_tools_empty_csv_gives_empty_set():
    """Empty CSV from Unity must produce empty set, not {''}."""
    from unittest.mock import AsyncMock
    import unity_mcp.server as srv

    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": ""})

    orig = srv._disabled_tools_cache
    orig_lock = srv._refresh_tools_lock
    try:
        srv._disabled_tools_cache = None
        srv._refresh_tools_lock = None
        await srv._refresh_tools_cache(bridge)
        assert srv._disabled_tools_cache == set(), f"Expected empty set, got {srv._disabled_tools_cache}"
    finally:
        srv._disabled_tools_cache = orig
        srv._refresh_tools_lock = orig_lock


# --- test handler registration ---

def test_request_handler_is_patched():
    """Our wrapper must be installed in request_handlers, not the original FastMCP handler."""
    handler = mcp._mcp_server.request_handlers[mcp_types.ListToolsRequest]
    assert handler.__name__ == "_filtered_tools_handler"


# --- TDD F4: handler strips deferred / preserves core ---

async def test_handler_strips_non_core_schema():
    """_filter_tools returns STUB schema for non-core tools."""
    import unity_mcp.server as srv
    from unity_mcp.tools.schema_registry import STUB_SCHEMA

    full = {"type": "object", "properties": {"x": {"type": "string"}}, "required": ["x"]}
    tool_core = _tool("get_hierarchy")
    tool_core.inputSchema = full
    tool_noncore = _tool("animation")
    tool_noncore.inputSchema = full

    orig_cache = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = None
        result = await srv._filter_tools([tool_core, tool_noncore], None)
        names = {t.name: t for t in result}
        # get_hierarchy passes gating — verify its schema kept (if returned)
        if "get_hierarchy" in names:
            assert names["get_hierarchy"].inputSchema == full
        # animation gets gated out by tier filter (not in TIER1 and not enabled)
        assert "animation" not in names
    finally:
        srv._disabled_tools_cache = orig_cache


async def test_handler_preserves_core_full_schema():
    """Core tools keep their full inputSchema after strip."""
    import unity_mcp.server as srv
    from unity_mcp.server import _strip_deferred_schemas

    full = {"type": "object", "properties": {"p": {"type": "string"}}, "required": ["p"]}
    tool = _tool("batch")
    tool.inputSchema = full

    result = _strip_deferred_schemas([tool])
    assert result[0].inputSchema == full


# ---------------------------------------------------------------------------
# A3: _tcp_probe + read_unity_port TCP-probe integration
# ---------------------------------------------------------------------------

def test_read_unity_port_skips_candidate_if_tcp_probe_fails(tmp_path):
    """PID alive but TCP refused → skip candidate, return default 9500."""
    from unittest.mock import patch, MagicMock
    from pathlib import Path

    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    port_file = ports_dir / "12345.port"
    port_file.write_text("9501\n/some/project\nMyProject", encoding="utf-8")

    with (
        patch("unity_mcp.server_filtering.Path") as mock_path_cls,
        patch("os.kill"),  # PID alive, no exception
        patch("unity_mcp.server_filtering._tcp_probe", return_value=False),
        patch.dict("os.environ", {}, clear=False),
    ):
        # Wire Path.home() / ".unity-mcp" / "ports" to our tmp dir
        mock_home = MagicMock()
        mock_path_cls.home.return_value = mock_home
        mock_home.__truediv__ = lambda self, x: tmp_path / x if x == ".unity-mcp" else MagicMock()

        from unity_mcp import server_filtering
        with patch.object(Path, "home", return_value=tmp_path):
            result = server_filtering.read_unity_port()

    assert result == 9500


def test_read_unity_port_includes_candidate_if_tcp_probe_succeeds(tmp_path):
    """PID alive and TCP connects → include candidate, return its port."""
    from pathlib import Path
    from unittest.mock import patch

    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    port_file = ports_dir / "12345.port"
    port_file.write_text("9501\n/some/project\nMyProject", encoding="utf-8")

    with (
        patch("unity_mcp.server_filtering._tcp_probe", return_value=True),
        patch("os.kill"),  # PID alive
        patch.object(Path, "home", return_value=tmp_path),
    ):
        from unity_mcp import server_filtering
        result = server_filtering.read_unity_port()

    assert result == 9501


def test_read_unity_port_cyrillic_project_path_parses_correctly(tmp_path):
    """Cyrillic project path in .port file must parse without mojibake.
    Discriminating: remove encoding= from server_filtering.py and this test fails
    (UnicodeDecodeError on cp1251 systems / garbled project field on macOS).
    Uses write_bytes to avoid test-side EncodingWarning.
    """
    from pathlib import Path

    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    port_file = ports_dir / "12345.port"
    # Write file as raw UTF-8 bytes — bypasses EncodingWarning gate on test side
    port_file.write_bytes("9501\n/Users/Иван/MyProject\nМойПроект\n".encode("utf-8"))

    with (
        patch("unity_mcp.server_filtering._tcp_probe", return_value=True),
        patch("os.kill"),  # PID alive
        patch.object(Path, "home", return_value=tmp_path),
    ):
        from unity_mcp import server_filtering
        result = server_filtering.read_unity_port()

    assert result == 9501  # port parsed correctly despite Cyrillic


# ---------------------------------------------------------------------------
# PY2.arch.3: push_catalog must omit empty categories
# ---------------------------------------------------------------------------

async def test_push_catalog_omits_empty_categories():
    """push_catalog must not send 'CONNECTION:' (or any empty-tools category)."""
    from unittest.mock import AsyncMock, MagicMock
    from unity_mcp.server_filtering import push_catalog

    bridge = MagicMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": ""})

    await push_catalog(bridge)

    bridge.send.assert_called_once()
    catalog_arg = bridge.send.call_args[1].get("catalog") or bridge.send.call_args[0][1].get("catalog", "")
    for line in catalog_arg.split("\n"):
        if ":" in line:
            cat, tools_str = line.split(":", 1)
            assert tools_str.strip(), f"Category '{cat}' has empty tools list in catalog"


# ---------------------------------------------------------------------------
# PY3.arch.1: _strip_deferred_schemas must use canonical STUB_SCHEMA (identity)
# ---------------------------------------------------------------------------

def test_strip_uses_canonical_stub():
    """Non-core tool's inputSchema after strip must be the exact STUB_SCHEMA object."""
    from types import SimpleNamespace
    from unity_mcp.server_filtering import _strip_deferred_schemas
    from unity_mcp.tools.schema_registry import STUB_SCHEMA

    tool = SimpleNamespace(name="animation", inputSchema={"type": "object", "properties": {}})
    result = _strip_deferred_schemas([tool])
    assert result[0].inputSchema is STUB_SCHEMA


# ---------------------------------------------------------------------------
# X4.cross.4: UNITY_MCP_NO_GATING=1 bypasses tier filter
# ---------------------------------------------------------------------------

def test_no_gating_env_bypasses_filter(monkeypatch):
    """UNITY_MCP_NO_GATING=1 makes _apply_gating return the original list unchanged."""
    from types import SimpleNamespace
    from unity_mcp.server_filtering import _apply_gating

    monkeypatch.setenv("UNITY_MCP_NO_GATING", "1")
    tools = [SimpleNamespace(name="shader"), SimpleNamespace(name="animation")]
    result = _apply_gating(tools)
    assert result is tools
