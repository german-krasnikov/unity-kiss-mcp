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


# no-assert: crash guard
def test_load_plugins_bad_module_skipped():
    """Plugin with import error is skipped, not crash."""
    with patch("unity_mcp.plugins.import_module") as mock_import:
        mock_import.side_effect = ImportError("no module")
        try:
            load_plugins(MagicMock(), MagicMock(), MagicMock())
        except ImportError:
            pytest.fail("ImportError should be swallowed by load_plugins")


# no-assert: crash guard
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


# --- Zone #28 gap tests ---

# no-assert: crash guard
def test_load_entry_points_bad_ep_load_skipped():
    """Entry point whose .load() raises is skipped, not crash."""
    from unity_mcp.plugins import _load_entry_points

    bad_ep = MagicMock()
    bad_ep.name = "bad_plugin"
    bad_ep.load.side_effect = Exception("failed to load")

    with patch("importlib.metadata.entry_points", return_value=[bad_ep]):
        # Should not raise
        _load_entry_points(MagicMock(), MagicMock(), MagicMock())


# no-assert: crash guard
def test_load_plugin_dirs_nonexistent_dir_skipped(monkeypatch):
    """UNITY_MCP_PLUGIN_DIRS pointing to non-existent dir is silently skipped."""
    from unity_mcp.plugins import _load_plugin_dirs

    monkeypatch.setenv("UNITY_MCP_PLUGIN_DIRS", "/nonexistent/path/that/does/not/exist")
    # Should not raise, just skip silently
    _load_plugin_dirs(MagicMock(), MagicMock(), MagicMock())


def test_check_api_version_too_high_returns_false():
    """Module requiring API v999 is rejected when server has API_VERSION=1."""
    import types
    from unity_mcp.plugins import _check_api_version

    mod = types.ModuleType("future_plugin")
    mod.REQUIRED_API_VERSION = 999
    result = _check_api_version(mod, "future_plugin")
    assert result is False


def test_check_api_version_compatible_returns_true():
    """Module with REQUIRED_API_VERSION <= API_VERSION is accepted."""
    import types
    from unity_mcp.plugins import _check_api_version

    mod = types.ModuleType("compat_plugin")
    mod.REQUIRED_API_VERSION = 1
    assert _check_api_version(mod, "compat_plugin") is True


def test_check_api_version_no_version_attr_returns_true():
    """Module with no REQUIRED_API_VERSION is always accepted."""
    import types
    from unity_mcp.plugins import _check_api_version

    mod = types.ModuleType("plain_plugin")
    assert _check_api_version(mod, "plain_plugin") is True


async def test_save_skill_invalid_name_slash():
    """save_skill with '/' in name raises ValueError."""
    from unity_mcp.tools.skills import save_skill
    with pytest.raises(ValueError, match="Invalid name"):
        await save_skill("bad/name", "desc", "code")


async def test_save_skill_invalid_name_dotdot():
    """save_skill with '..' in name raises ValueError."""
    from unity_mcp.tools.skills import save_skill
    with pytest.raises(ValueError, match="Invalid name"):
        await save_skill("../etc", "desc", "code")


def test_plugin_register_called_twice_no_crash():
    """Calling register twice on same module does not crash."""
    import types

    call_count = [0]
    mod = types.ModuleType("dup_plugin")
    mod.register = lambda mcp, s, a: call_count.__setitem__(0, call_count[0] + 1)

    mcp = MagicMock()
    send = MagicMock()
    args = MagicMock()

    with patch("unity_mcp.plugins.pkgutil.iter_modules", return_value=[("finder", "dup_plugin", False)]):
        with patch("unity_mcp.plugins.import_module", return_value=mod):
            from unity_mcp.plugins import load_plugins
            load_plugins(mcp, send, args)
            load_plugins(mcp, send, args)

    assert call_count[0] >= 2  # registered twice, no exception


# ── Fix 28: plugins PLUGIN_DIRS API version check ─────────────────────────────

def test_load_plugin_dirs_calls_check_api_version(tmp_path, monkeypatch):
    """Fix 28: _load_plugin_dirs must call _check_api_version for each loaded plugin."""
    import sys
    plugin_file = tmp_path / "fake_plugin.py"
    plugin_file.write_text(
        "REQUIRED_API_VERSION = 999\n"
        "def register(mcp, send, args): pass\n",
        encoding="utf-8",
    )
    monkeypatch.setenv("UNITY_MCP_PLUGIN_DIRS", str(tmp_path))
    monkeypatch.setenv("UNITY_MCP_SKIP_PLUGINS", "")

    for k in list(sys.modules.keys()):
        if "fake_plugin" in k:
            del sys.modules[k]

    checked = []
    import unity_mcp.plugins as plug

    original_check = plug._check_api_version

    def spy_check(module, name):
        checked.append(name)
        return original_check(module, name)

    monkeypatch.setattr(plug, "_check_api_version", spy_check)

    mcp = MagicMock()
    plug._load_plugin_dirs(mcp, MagicMock(), MagicMock())

    assert "fake_plugin" in checked, f"_check_api_version not called. checked={checked}"
