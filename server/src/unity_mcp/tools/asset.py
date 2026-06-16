from ._annotations import RO as _RO, RW as _RW, RW_IDEM as _RW_IDEM

_send = None
_args = None


async def asset(action: str, path: str | None = None, type: str | None = None,
                name: str | None = None, folder: str | None = None,
                source: str | None = None, dest: str | None = None,
                prop: str | None = None, value: str | None = None,
                recursive: bool = False, labels: str | None = None,
                output: str | None = None) -> str:
    """Asset database. action: find|get_info|create|move|validate_move|duplicate|delete|get_dependencies|import_settings|export_package|import_package. find: type+name+folder+labels. create: type=Folder|Material|PhysicMaterial. move/validate_move: source+dest (Assets/ paths). Moves .meta correctly. export_package: path+output. import_package: path (filesystem)."""
    return await _send("asset", _args(
        action=action, path=path, type=type, name=name, folder=folder,
        source=source, dest=dest, prop=prop, value=value,
        recursive="true" if recursive else None, labels=labels,
        output=output))


async def project_settings(action: str, target: str, prop: str | None = None,
                           value: str | None = None, index: int | None = None) -> str:
    """Project settings. action: get|set. target: tags|layers|sorting_layers|quality|physics|time|player."""
    return await _send("project_settings", _args(
        action=action, target=target, prop=prop, value=value, index=index))


async def material(action: str, path: str | None = None, object_path: str | None = None,
                   shader: str | None = None, prop: str | None = None, value: str | None = None,
                   source: str | None = None, targets: str | None = None) -> str:
    """Material. action: create|get|set|copy|list_properties. create: path+shader. get/set: path (asset) or object_path (scene). copy: source+targets (comma-sep scene paths)."""
    return await _send("material", _args(
        action=action, path=path, object_path=object_path, shader=shader,
        prop=prop, value=value, source=source, targets=targets))


async def prefab(action: str, path: str | None = None, asset_path: str | None = None,
                 base_path: str | None = None, variant_path: str | None = None,
                 recursive: bool = False) -> str:
    """Prefab. action: save|create_variant|apply|revert|get_overrides|unpack. save: path (scene) + asset_path. create_variant: base_path + variant_path."""
    return await _send("prefab", _args(
        action=action, path=path, asset_path=asset_path,
        base_path=base_path, variant_path=variant_path,
        recursive="true" if recursive else None))


async def scriptable_object(action: str, path: str | None = None, type: str | None = None,
                            prop: str | None = None, value: str | None = None,
                            filter: str | None = None) -> str:
    """ScriptableObject. action: create|get|set|list_types|find. create: type+path. get/set: path. find: type. list_types: filter."""
    return await _send("scriptable_object", _args(
        action=action, path=path, type=type, prop=prop, value=value, filter=filter))


async def get_enabled_tools() -> str:
    """List enabled tool names, comma-separated."""
    return await _send("get_enabled_tools", {})


def register(mcp, send, args):
    global _send, _args
    _send = send
    _args = args
    mcp.tool(annotations=_RW)(asset)
    mcp.tool(annotations=_RW_IDEM)(project_settings)
    mcp.tool(annotations=_RW)(material)
    mcp.tool(annotations=_RW)(prefab)
    mcp.tool(annotations=_RW)(scriptable_object)
    mcp.tool(annotations=_RO)(get_enabled_tools)
