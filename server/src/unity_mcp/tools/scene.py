import re
from ._annotations import RO as _RO, RW as _RW, RW_IDEM as _RW_IDEM, DEL as _DEL

_RE_SLOT = re.compile(r'slot_\d+\s+\[\]\s+#')
_RE_POINT = re.compile(r'point_\d+\s+\[\]\s+#')
_RE_MESH = re.compile(r'\[MeshFilter,MeshRenderer\]\s+#')

_send = None
_args = None


def _count_group(lines: list[str], i: int, regex, extra_check=None) -> tuple[str, int]:
    """Count consecutive lines matching regex (with optional extra_check). Returns (indent, count)."""
    indent = lines[i][:len(lines[i]) - len(lines[i].lstrip())]
    count = 1
    while i + count < len(lines) and regex.search(lines[i + count]):
        if extra_check and not extra_check(lines[i + count]):
            break
        count += 1
    return indent, count


def compress_hierarchy(text: str) -> str:
    """Compress hierarchy output: group identical siblings, collapse visual-only subtrees."""
    lines = text.split('\n')
    result = []
    i = 0
    while i < len(lines):
        line = lines[i]
        if _RE_SLOT.search(line):
            indent, count = _count_group(lines, i, _RE_SLOT)
            result.append(f"{indent}[{count}x slot]")
            i += count
            continue
        if _RE_POINT.search(line):
            indent, count = _count_group(lines, i, _RE_POINT)
            result.append(f"{indent}[{count}x point]")
            i += count
            continue
        if _RE_MESH.search(line) and '...' not in line:
            indent, count = _count_group(lines, i, _RE_MESH, lambda l: '...' not in l)
            if count >= 3:
                result.append(f"{indent}[{count}x visual mesh]")
                i += count
                continue
        result.append(line)
        i += 1
    return '\n'.join(result)


async def get_hierarchy(depth: int = 2, root: str | None = None, filter: str | None = None,
                        components: bool = False, compress: bool = False,
                        summary: bool = False, incremental: bool = False) -> str:
    """Scene hierarchy as text tree. Max 3000 nodes. Use filter/depth to narrow. Set components=true to see component types. Set compress=true to group repeated slots/points/meshes. Set summary=true for compact root-only counts (60-100 tokens). Set incremental=true to get NO_CHANGE if scene unchanged since last call."""
    if summary:
        return await _send("get_hierarchy", _args(root=root, summary="true"))
    result = await _send("get_hierarchy", _args(
        depth=depth, root=root, filter=filter,
        components="true" if components else None,
        incremental="true" if incremental else None))
    if compress:
        result = compress_hierarchy(result)
    return result


async def get_console(count: int = 10, level: str | None = None, first: int = 0) -> str:
    """Recent console logs. first>0: return first N from init buffer + last (count-first) from ring."""
    return await _send("get_console", _args(count=count, level=level, first=first if first > 0 else None))


async def get_compile_errors() -> str:
    """Compilation errors with file:line:column. Not lost on Console.Clear(). Structured, typed."""
    from .. import editor_log
    return editor_log.corroborate(await _send("get_compile_errors", {}))


def _get_describer_safe():
    """Return ScreenshotDescriber or None on import/init error."""
    try:
        from ..screenshot_describe.describer import get_describer
        return get_describer()
    except Exception:
        return None


async def screenshot(width: int = 640, height: int = 480, camera: str | None = None,
                     path: str | None = None, describe: str | None = None,
                     raw: bool = False, zoom: float | None = None,
                     angles: str | None = None, supersample: int | None = None,
                     offset: str | None = None, fixed_size: float | None = None,
                     highlight: str | None = None,
                     show_colliders: bool | None = None,
                     angle: str | None = None) -> str:
    """Capture screenshot (file path); describe= -> Haiku text (15-100x fewer tokens), raw=True forces path.
    camera: scene_view|scene_view_frame|multi_view|single_view|overview|overview_game. angle (single_view): front|left|top|iso|ex,ey,ez.
    zoom: higher=closer. angles: per-view Euler "ex,ey,ez|..." (_=skip). supersample 1-4. offset/fixed_size: framing.
    highlight: paths[:#RRGGBB] for bbox. show_colliders: wireframes."""
    result = await _send("screenshot", _args(width=width, height=height, camera=camera,
                                             path=path, zoom=zoom, angles=angles,
                                             supersample=supersample,
                                             offset=offset, fixed_size=fixed_size,
                                             highlight=highlight,
                                             show_colliders="true" if show_colliders else None,
                                             angle=angle))
    if raw or describe is None or "Data saved to:" not in result:
        return result
    png_path = result.split("Data saved to: ")[-1].strip()
    key = "multi_view" if camera == "multi_view" else describe
    describer = _get_describer_safe()
    if describer is None:
        return result
    try:
        try:
            fp = await _send("fingerprint", _args(path=path, depth=2))
        except Exception:
            fp = None
        desc = await describer.describe(png_path, key, fp)
        if desc is None:
            return result
        return f"{desc}\n[img:{png_path}]"
    except Exception:
        return result


async def recompile() -> str:
    """Trigger Unity to reimport C# scripts. Returns immediately; use await_compile to block until done."""
    return await _send("recompile", {}, timeout=60.0)


_POLL_INTERVAL = 2.0
_POLL_ATTEMPTS = 30


async def run_tests(mode: str = "EditMode") -> str:
    """Run Unity tests. mode: EditMode or PlayMode."""
    from mcp.server.fastmcp.exceptions import ToolError as _TE
    try:
        return await _send("run_tests", {"mode": mode}, timeout=120.0)
    except _TE:
        if mode != "PlayMode":
            raise
        import asyncio
        for _ in range(_POLL_ATTEMPTS):
            await asyncio.sleep(_POLL_INTERVAL)
            try:
                result = await _send("get_test_results", {})
                if result and result != "pending":
                    return result
            except _TE:
                pass
        return "Error: PlayMode test results not received (timeout)"


async def get_test_results() -> str:
    """Poll for test results after PlayMode run. Returns results, 'pending', or 'none'."""
    return await _send("get_test_results", {})


async def scene(action: str, path: str | None = None) -> str:
    """Scene management. action: new|open|save|discard. path: for open/save."""
    return await _send("scene", _args(action=action, path=path))


async def search_scene(query: str, root: str | None = None, limit: int = 50) -> str:
    """Search scene objects. Syntax: name text, t:Component, tag=Tag, layer=N, active=bool. Combine with spaces.
    root: scope search to subtree (path or None for whole scene).
    limit: max results (default 50; 0=unlimited). Default not sent over wire."""
    return await _send("search_scene", _args(
        query=query, root=root,
        limit=str(limit) if limit != 50 else None))


async def editor(action: str = "state", path: str | None = None) -> str:
    """Editor state/control. action: state|play|pause|stop|select|project_path. select needs path."""
    t = 15.0 if action in ("play", "stop", "pause") else 30.0
    return await _send("editor", _args(action=action, path=path), timeout=t)


async def checkpoint(label: str = "checkpoint") -> str:
    """Create a named Undo checkpoint. Use before major scene changes. Allows rollback via Ctrl+Z in Unity."""
    return await _send("checkpoint", _args(label=label))


async def fingerprint(path: str | None = None, depth: int = 3) -> str:
    """Scene state hash. Returns fp:XXXXXXXX. If unchanged, skip re-reading. ~5 tokens."""
    return await _send("fingerprint", _args(path=path, depth=depth))


async def scene_diff() -> str:
    """Compare scene with last snapshot. First call saves snapshot. Returns diff: added/removed lines."""
    return await _send("scene_diff", {})


# Re-exports from scene_session for backward compatibility
from .scene_session import (  # noqa: E402
    save_session, load_session, screenshot_baseline, screenshot_compare,
    get_changes, _extract_saved_path,
)


def register(mcp, send, args):
    global _send, _args
    _send = send
    _args = args
    from .. import editor_log
    editor_log.init_corroboration()
    mcp.tool(annotations=_RO)(get_hierarchy)
    mcp.tool(annotations=_RO)(get_console)
    mcp.tool(annotations=_RO)(get_compile_errors)
    mcp.tool(annotations=_RO)(screenshot)
    mcp.tool(annotations=_RW_IDEM)(recompile)
    mcp.tool(annotations=_RO)(run_tests)
    mcp.tool(annotations=_RO)(get_test_results)
    mcp.tool(annotations=_DEL)(scene)
    mcp.tool(annotations=_RO)(search_scene)
    mcp.tool(annotations=_RW)(editor)
    mcp.tool(annotations=_RW)(checkpoint)
    mcp.tool(annotations=_RO)(fingerprint)
    mcp.tool(annotations=_RO)(scene_diff)
    from . import scene_session
    scene_session.register(mcp, send, args)
