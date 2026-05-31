import pytest
import types
from unittest.mock import MagicMock, AsyncMock, patch
from unity_mcp.plugins import load_plugins


def test_load_plugins_calls_register():
    """load_plugins calls register on discovered plugins."""
    called = []

    fake_mod = types.ModuleType("test_plugin")
    fake_mod.register = lambda mcp, s, a: called.append("test")

    with patch("unity_mcp.plugins.pkgutil.iter_modules", return_value=[("finder", "test_plugin", False)]):
        with patch("unity_mcp.plugins.import_module", return_value=fake_mod):
            load_plugins(MagicMock(), MagicMock(), MagicMock())

    assert len(called) > 0


def test_load_plugins_bad_module_skipped():
    """Plugin with import error is skipped, not crash."""
    with patch("unity_mcp.plugins.import_module") as mock_import:
        mock_import.side_effect = ImportError("no module")
        try:
            load_plugins(MagicMock(), MagicMock(), MagicMock())
        except ImportError:
            pytest.fail("ImportError should be swallowed by load_plugins")


def test_plugin_without_register_skipped():
    """Module without register() is silently skipped."""
    with patch("unity_mcp.plugins.import_module") as mock_import:
        no_reg = types.ModuleType("no_reg")
        mock_import.return_value = no_reg
        try:
            load_plugins(MagicMock(), MagicMock(), MagicMock())
        except AttributeError:
            pytest.fail("Missing register() should be silently skipped")


def test_skip_plugins_env(monkeypatch):
    """UNITY_MCP_SKIP_PLUGINS=test_ skips matching plugins."""
    import importlib
    import unity_mcp.plugins as plugins_mod

    monkeypatch.setenv("UNITY_MCP_SKIP_PLUGINS", "test_")
    importlib.reload(plugins_mod)

    registered_names = []

    class FakeMcp:
        def tool(self, **kwargs):
            def decorator(fn):
                registered_names.append(fn.__name__)
                return fn
            return decorator

    plugins_mod.load_plugins(FakeMcp(), MagicMock(), MagicMock())

    test_tools = [n for n in registered_names if n.startswith("test_")]
    assert test_tools == [], f"test_ tools registered despite skip: {test_tools}"

    monkeypatch.delenv("UNITY_MCP_SKIP_PLUGINS", raising=False)
    importlib.reload(plugins_mod)


def test_skip_plugins_empty_prefix_no_blanket_skip(monkeypatch):
    """UNITY_MCP_SKIP_PLUGINS='' or ',' does NOT skip all plugins."""
    import importlib
    import unity_mcp.plugins as plugins_mod

    monkeypatch.setenv("UNITY_MCP_SKIP_PLUGINS", ",")
    importlib.reload(plugins_mod)

    registered_names = []

    class FakeMcp:
        def tool(self, **kwargs):
            def decorator(fn):
                registered_names.append(fn.__name__)
                return fn
            return decorator

    plugins_mod.load_plugins(FakeMcp(), MagicMock(), MagicMock())

    # Empty skip list must not blanket-skip all plugins
    assert not plugins_mod._should_skip("some_plugin")
    monkeypatch.delenv("UNITY_MCP_SKIP_PLUGINS", raising=False)
    importlib.reload(plugins_mod)


# --- plugin_api facade tests ---

def test_plugin_api_register_dsl_tools():
    from unity_mcp.plugin_api import register_dsl_tools
    from unity_mcp.tools.batch import _dsl_tools
    _dsl_tools.discard("test_dsl_cmd")
    register_dsl_tools("test_dsl_cmd")
    assert "test_dsl_cmd" in _dsl_tools
    _dsl_tools.discard("test_dsl_cmd")


def test_plugin_api_register_read_cmds():
    from unity_mcp.plugin_api import register_read_cmds
    from unity_mcp.middleware import READ_CMDS
    register_read_cmds("test_read_cmd")
    assert "test_read_cmd" in READ_CMDS
    READ_CMDS.discard("test_read_cmd")


def test_plugin_api_register_write_cmds():
    from unity_mcp.plugin_api import register_write_cmds
    from unity_mcp.middleware import WRITE_CMDS
    register_write_cmds("test_write_cmd")
    assert "test_write_cmd" in WRITE_CMDS
    WRITE_CMDS.discard("test_write_cmd")


def test_plugin_api_register_tools():
    from unity_mcp.plugin_api import register_tools
    from unity_mcp.tools.gating import CATEGORIES, TIER1, _ALL_KNOWN
    register_tools("test_cat", {"tool_a", "tool_b"}, tier1={"tool_a"})
    assert "tool_a" in CATEGORIES["test_cat"]
    assert "tool_b" in CATEGORIES["test_cat"]
    assert "tool_a" in TIER1
    del CATEGORIES["test_cat"]
    TIER1.discard("tool_a")
    _ALL_KNOWN.discard("tool_a")
    _ALL_KNOWN.discard("tool_b")


def test_plugin_api_register_features():
    from unity_mcp.plugin_api import register_features
    from unity_mcp.budget.registry import FEATURES
    register_features({"test_feat": {"priority": "low", "difficulty": 0.1, "est_in": 100, "est_out": 50, "image": False}})
    assert "test_feat" in FEATURES
    assert FEATURES["test_feat"].priority == "low"
    del FEATURES["test_feat"]


def test_plugin_api_exports():
    from unity_mcp import plugin_api
    for name in plugin_api.__all__:
        assert hasattr(plugin_api, name), f"plugin_api.__all__ lists '{name}' but it's not defined"
