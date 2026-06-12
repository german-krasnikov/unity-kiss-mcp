"""P3 Macro tools — expand to batch internally.

Reduces multi-step setup to a single tool call.
"""
from ._annotations import RW as _RW

_send = None
_args = None


def _parse_kv(parts: list[str]) -> dict:
    """Parse 'key=value' token list into dict."""
    kv = {}
    for p in parts:
        if "=" in p:
            k, v = p.split("=", 1)
            kv[k] = v
    return kv


async def setup_objects(specs: str) -> str:
    """Create+configure multiple objects in one call.
    One per line: name [primitive=X] [parent=Y] [pos=(x,y,z)] [components=A,B]
    Example: NPC1 primitive=Capsule pos=(1,0,0) components=Health"""
    lines = []
    full_paths = []
    for spec in specs.strip().splitlines():
        spec = spec.strip()
        if not spec:
            continue
        parts = spec.split()
        name = parts[0]
        kv = _parse_kv(parts[1:])

        cmd = f"create_object name={name}"
        parent = kv.get("parent", "")
        if "primitive" in kv:
            cmd += f" primitive={kv['primitive']}"
        if parent:
            cmd += f" parent={parent}"
        lines.append(cmd)

        # Compute full path: /Parent/Name when parent is set
        parent_norm = f"/{parent}" if parent and not parent.startswith("/") else parent
        full_path = f"{parent_norm}/{name}" if parent_norm else f"/{name}"
        full_paths.append(full_path)

        if "pos" in kv:
            lines.append(
                f"set_property path={full_path} component=Transform "
                f"prop=m_LocalPosition value={kv['pos']}"
            )
        if "components" in kv:
            for comp in kv["components"].split(","):
                lines.append(f"manage_component path={full_path} type={comp.strip()} action=add")

    if not full_paths:
        return "No valid object specs found"

    lines.append(f"inspect paths={','.join(full_paths)} components=Transform")
    return await _send("batch", {"commands": "\n".join(lines), "on_error": "continue"})


async def set_properties(path: str, props: str) -> str:
    """Set multiple properties on one object.
    Format: component.prop=value per line or semicolon-separated.
    Example: Transform.m_LocalPosition=(1,0,0);Rigidbody.mass=5"""
    lines = []
    components: set[str] = set()

    for pair in props.replace(";", "\n").strip().splitlines():
        pair = pair.strip()
        if not pair or "=" not in pair or "." not in pair.split("=")[0]:
            continue
        left, value = pair.split("=", 1)
        component, prop = left.rsplit(".", 1)
        component = component.strip()
        prop = prop.strip()
        lines.append(
            f"set_property path={path} component={component} prop={prop} value={value.strip()}"
        )
        components.add(component)

    if not lines:
        return "No valid property pairs found"

    for comp in sorted(components):
        lines.append(f"get_component path={path} type={comp}")

    return await _send("batch", {"commands": "\n".join(lines), "on_error": "continue"})


async def configure_objects(config: str) -> str:
    """Configure multiple objects at once.
    Format: /Path component.prop=value [...] per line.
    Example:
    /NPC1 Transform.m_LocalPosition=(1,0,0) Health.maxHp=100
    /NPC2 Transform.m_LocalPosition=(3,0,0)"""
    lines = []
    paths: set[str] = set()

    for line in config.strip().splitlines():
        line = line.strip()
        if not line:
            continue
        first_token = line.split()[0]
        if not first_token.startswith("/") and ":/" not in first_token:
            continue
        parts = line.split()
        obj_path = parts[0]
        paths.add(obj_path)
        for part in parts[1:]:
            if "=" not in part or "." not in part.split("=")[0]:
                continue
            left, value = part.split("=", 1)
            component, prop = left.rsplit(".", 1)
            lines.append(
                f"set_property path={obj_path} component={component} prop={prop} value={value}"
            )

    if not lines:
        return "No valid config lines found"

    lines.append(f"inspect paths={','.join(sorted(paths))}")
    return await _send("batch", {"commands": "\n".join(lines), "on_error": "continue"})


def register(mcp, send, args):
    global _send, _args
    _send = send
    _args = args
    mcp.tool(annotations=_RW)(setup_objects)
    mcp.tool(annotations=_RW)(set_properties)
    mcp.tool(annotations=_RW)(configure_objects)
