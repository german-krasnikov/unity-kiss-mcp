"""Strip default values from component read responses to save tokens."""

_FIELD_ALIASES = {
    "position": "m_localposition", "localposition": "m_localposition",
    "rotation": "m_localrotation", "localrotation": "m_localrotation",
    "scale": "m_localscale", "localscale": "m_localscale",
    "mass": "m_mass", "enabled": "m_enabled", "active": "m_isactive",
    "name": "m_name", "tag": "m_tagstring", "layer": "m_layer",
}

_DEFAULTS = frozenset({
    "0", "0.0", "false", "null", "None", '""',
    "(0, 0, 0)", "(0.0, 0.0, 0.0)",
    "(0, 0, 0, 1)", "(0.0, 0.0, 0.0, 1.0)",
    "(1, 1, 1)", "(1.0, 1.0, 1.0)",
    "[]", "#00000000",
    # F08: additional common Unity defaults. "Default"/"Untagged" are context-dependent
    # (layer vs tag field); use _no_strip=True escape hatch if a real value collides.
    "1", "1.0", "Untagged", "Default",
    "(0, 0)", "(0, 0, 0, 0)", "#FFFFFFFF",
})


def project_fields(text: str, fields: str) -> str:
    """F07: keep only lines whose key matches a requested field (exact or dotted-prefix,
    case-insensitive). Always keep headers ([...]), separators (---), error/blank lines.
    Requesting 'm_LocalPosition' keeps 'm_LocalPosition.x/y/z'; 'pos' does NOT match 'position'."""
    wanted = []
    for f in (fields or "").split(","):
        raw = f.strip().lower()
        if not raw:
            continue
        wanted.append(raw)
        alias = _FIELD_ALIASES.get(raw)
        if alias and alias != raw:
            wanted.append(alias)
    if not wanted:
        return text
    out = []
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("[") or stripped == "---" or stripped.startswith("err:"):
            out.append(line)
            continue
        key = (stripped.split(": ", 1)[0] if ": " in stripped else stripped).strip().lower()
        if any(key == w or key.startswith(w + ".") for w in wanted):
            out.append(line)
    return "\n".join(out)


def strip_defaults(text: str) -> str:
    """Remove lines whose value is a known default. Keep headers, separators, errors."""
    if not text:
        return text
    out = []
    for line in text.splitlines():
        stripped = line.strip()
        # Always keep: section headers, separators, error lines, blank lines
        if not stripped or stripped.startswith("[") or stripped == "---" or stripped.startswith("err:"):
            out.append(line)
            continue
        # Check value after colon
        if ": " in stripped:
            value = stripped.split(": ", 1)[1].strip()
            if value in _DEFAULTS:
                continue
        out.append(line)
    return "\n".join(out)
