"""Whitelist of allowed commands and their signatures for do() tool."""

ALLOWED: dict[str, dict] = {
    "create_object":    {"required": {"name"}, "optional": {"primitive", "parent"}},
    "set_property":     {"required": {"path", "component", "prop", "value"}, "optional": {"dry_run"}},
    "manage_component": {"required": {"path", "type", "action"}, "optional": set()},
    "set_active":       {"required": {"path", "active"}, "optional": set()},
    "set_material":     {"required": {"path"}, "optional": {"color", "shader"}},
    "wire_event":       {"required": {"path", "component", "event", "target", "method"}, "optional": {"arg_type", "arg_value"}},
}

FORBIDDEN: set[str] = {
    "delete_object", "execute_code", "recompile",
}


def build_glossary() -> str:
    """Return human-readable command list for Haiku prompt."""
    lines = []
    for cmd, sig in ALLOWED.items():
        req = " ".join(f"{k}=..." for k in sorted(sig["required"]))
        opt = " ".join(f"[{k}=...]" for k in sorted(sig["optional"]))
        lines.append(f"  {cmd} {req} {opt}".rstrip())
    return "\n".join(lines)
