"""Capability gating — tier-based tool visibility with session enable."""

TIER1: set[str] = {
    "get_hierarchy", "get_component", "inspect", "set_property",
    "create_object", "delete_object", "manage_component", "batch",
    "get_console", "get_compile_errors", "screenshot", "scene", "editor",
    "search_scene", "run_tests", "discover_tools", "get_enabled_tools",
    "setup_objects", "set_properties", "configure_objects",
    "do", "ask",
    "animator_intent", "vfx_intent", "ui_intent",
    "get_metrics",
    "find_references", "compile_preflight", "semantic_at",
    "set_parent",
    # runtime tools — always available in Play Mode
    "invoke_method", "set_runtime_property", "wait_until", "move_to",
    "query_state", "test_step", "run_playtest", "fuzz_playtest",
}

CATEGORIES: dict[str, set[str]] = {
    "object": {"find_objects", "get_object_detail", "get_components_list", "set_active", "set_material", "wire_event", "unwire_event", "set_property_delta"},
    "animation": {"animation", "timeline", "animator", "particle"},
    "asset": {"asset", "material", "prefab", "scriptable_object", "project_settings"},
    "advanced": {
        "shader", "references", "validate_references", "menu", "checkpoint", "recompile", "execute_code",
        "check_colliders", "get_schema", "scan_scene", "spatial_query", "auto_fix", "smart_build",
        "apply_template", "save_template", "list_templates",
    },
    "ui": {"create_ui", "set_rect", "validate_layout", "get_spatial_context"},
    "runtime": {"invoke_method", "set_runtime_property", "wait_until", "move_to", "query_state", "test_step", "run_playtest", "fuzz_playtest"},
    "connection": {"list_connections", "reconnect_unity"},
    "session": {
        "fingerprint", "scene_diff", "get_changes", "save_session", "load_session",
        "screenshot_baseline", "screenshot_compare", "save_skill", "use_skill", "list_skills",
    },
}

# All known tool names across all tiers
_ALL_KNOWN: set[str] = TIER1 | {n for s in CATEGORIES.values() for n in s}

_session_enabled: set[str] = set()


def register_tools(category: str, tools: set, tier1: set | None = None) -> None:
    """Plugin self-registration: add tools to a category and optionally to TIER1."""
    CATEGORIES.setdefault(category, set()).update(tools)
    _ALL_KNOWN.update(tools)
    if tier1:
        TIER1.update(tier1)
        _ALL_KNOWN.update(tier1)


def reset() -> None:
    _session_enabled.clear()


def get_categories() -> dict[str, set[str]]:
    return CATEGORIES


def is_visible(name: str) -> bool:
    if name in TIER1:
        return True
    return name in _session_enabled


def enable_category(category: str) -> list[str]:
    if category not in CATEGORIES:
        raise ValueError(f"Unknown category: '{category}'. Valid: {sorted(CATEGORIES)}")
    names = CATEGORIES[category]
    _session_enabled.update(names)
    return sorted(names)


def filter_by_tier(tools: list) -> list:
    """Keep TIER1 + session-enabled + unknown (plugin) tools."""
    return [t for t in tools if t.name not in _ALL_KNOWN or is_visible(t.name)]


async def discover_tools(category: str | None = None, enable: bool = True) -> str:
    """Find and enable tools by category.
    Categories: object, animation, asset, advanced, ui, runtime, connection, session.
    Pass enable=False to browse without enabling."""
    if category is None:
        lines = [f"{k}: {', '.join(sorted(v))}" for k, v in CATEGORIES.items()]
        return "\n".join(lines)
    if category not in CATEGORIES:
        raise ValueError(f"Unknown category: '{category}'. Valid: {sorted(CATEGORIES)}")
    names = sorted(CATEGORIES[category])
    if enable:
        _session_enabled.update(CATEGORIES[category])
    return f"Category '{category}': {', '.join(names)}"


discover_tools.__test__ = False  # prevent pytest collection
