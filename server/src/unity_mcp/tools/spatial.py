"""Spatial / scene-analysis queries: layout, context, scan, raycast, colliders."""
from ._annotations import RO as _RO

_send = None
_args = None


async def validate_layout(root: str = "/", min_distance: float = 3.0) -> str:
    """Check trigger overlaps. Warns if triggers closer than min_distance meters."""
    return await _send("validate_layout", _args(root=root, min_distance=str(min_distance)))


async def get_spatial_context(path: str, radius: float = 5.0) -> str:
    """Collider info + approach vectors + nearby objects within radius. Raycast in Play Mode only."""
    return await _send("get_spatial_context", _args(path=path, radius=str(radius)))


async def scan_scene(bands: str | None = None) -> str:
    """Scene infrastructure scan: colliders, triggers, audio, lights, rigidbody, canvas, nav. Coverage stats."""
    return await _send("scan_scene", _args(bands=bands))


async def check_colliders(path: str | None = None) -> str:
    """Check collider issues: triggers without Rigidbody, negative scale, micro colliders. Scans whole scene if no path given."""
    return await _send("check_colliders", _args(path=path))


def _validate_polygon(vertices: str | None) -> None:
    """Fast-fail validation for polygon CSV. Raises ToolError on bad input."""
    from mcp.server.fastmcp.exceptions import ToolError
    if not vertices:
        raise ToolError("vertices required for objects_in_polygon")
    pairs = vertices.split(";")
    if len(pairs) < 3:
        raise ToolError(f"polygon needs >=3 vertices, got {len(pairs)}")
    if len(pairs) > 256:
        raise ToolError(f"polygon max 256 vertices, got {len(pairs)}. Simplify contour.")
    for i, pair in enumerate(pairs):
        parts = pair.strip().split(",")
        if len(parts) != 2:
            raise ToolError(f"vertex {i}: expected 'x,z', got '{pair.strip()}'")
        try:
            x, z = float(parts[0]), float(parts[1])
        except ValueError:
            raise ToolError(f"vertex {i}: non-numeric '{pair.strip()}'")
        if abs(x) > 100_000 or abs(z) > 100_000:
            raise ToolError(f"vertex {i}: coordinates out of range (max 100000)")


async def spatial_query(action: str, path: str | None = None, target: str | None = None,
                        distance: float | None = None, radius: float | None = None,
                        component: str | None = None, cell_size: float | None = None,
                        layer_mask: str | None = None,
                        center: str | None = None,
                        vertices: str | None = None,
                        region_id: str | None = None,
                        cap: int | None = None) -> str:
    """Spatial queries. action: nearest|in_front_of|objects_in_radius|bounds_info|raycast|spatial_map|objects_in_polygon.
    nearest: find closest object (optionally filtered by component name).
    in_front_of: position in front of object at distance.
    objects_in_radius: list all objects within radius. path is optional when center='x,y,z' is given.
    bounds_info: detailed bounds/dimensions of object.
    raycast: cast ray from path/pos to target, returns hits sorted by distance.
    spatial_map: ASCII grid map of objects in XZ plane. cell_size in meters.
    objects_in_polygon: objects whose XZ pivot is inside polygon. vertices='x1,z1;x2,z2;...' (>=3 pairs). cap=max results (default 50)."""
    if action == "objects_in_polygon":
        _validate_polygon(vertices)
    return await _send("spatial_query", _args(
        action=action, path=path, target=target,
        distance=str(distance) if distance is not None else None,
        radius=str(radius) if radius is not None else None,
        component=component,
        cell_size=str(cell_size) if cell_size is not None else None,
        layer_mask=layer_mask,
        center=center,
        vertices=vertices,
        region_id=region_id,
        cap=str(cap) if cap is not None else None))


def register(mcp, send, args):
    global _send, _args
    _send = send
    _args = args
    mcp.tool(annotations=_RO)(validate_layout)
    mcp.tool(annotations=_RO)(get_spatial_context)
    mcp.tool(annotations=_RO)(scan_scene)
    mcp.tool(annotations=_RO)(check_colliders)
    mcp.tool(annotations=_RO)(spatial_query)
