"""sync_unity tool — unified Unity reload API.

Sequence (ref §13):
  1. bump_version (if bump=True)
  2. C# sync → sync_ack|epoch=N|will_compile=bool
  3. If will_compile=false → fast path (< 5s)
  4. Poll sync_status until epoch=N|state=ready (or failed/timeout)
  5. Reconnect transparently on DomainReloadError
  6. Return errors or "sync clean"
"""
import asyncio
import time
from pathlib import Path

from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.bridge import DomainReloadError
from unity_mcp import editor_log

_send = None

_POLL_INTERVAL = 1.0
_DEFAULT_TIMEOUT = 120.0
_FOCUS_HINT_AFTER = 15.0  # see backgrounded-editor hint in sync_unity

# Path to plugin package.json — resolved relative to this file's repo root.
# None when running as installed package (no sibling unity-plugin dir).
def _package_json_path() -> "Path | None":
    repo_root = Path(__file__).resolve().parents[4]
    pkg = repo_root / "unity-plugin" / "package.json"
    return pkg if pkg.exists() else None


def _parse_ack(ack: str) -> tuple[int, bool]:
    """Parse 'sync_ack|epoch=N|will_compile=bool' → (epoch, will_compile)."""
    parts = {p.split("=", 1)[0]: p.split("=", 1)[1] for p in ack.split("|") if "=" in p}
    epoch = int(parts.get("epoch", "0"))
    will_compile = parts.get("will_compile", "false").lower() == "true"
    return epoch, will_compile


def _parse_status(status: str) -> tuple[int, str, str]:
    """Parse 'epoch=N|state=S[|err=...]' → (epoch, state, err)."""
    parts = {p.split("=", 1)[0]: p.split("=", 1)[1] for p in status.split("|") if "=" in p}
    epoch = int(parts.get("epoch", "0"))
    state = parts.get("state", "idle")
    err   = parts.get("err", "")
    return epoch, state, err


async def sync_unity(
    resolve: bool = False,
    bump: bool = False,
    timeout: float = _DEFAULT_TIMEOUT,
) -> str:
    """Unified Unity reload: trigger Refresh (+ optional Resolve), wait for new code to live.

    resolve=True: call Client.Resolve() first (use after package.json change).
    bump=True: atomically increment plugin patch version BEFORE sync, implies resolve=True.
    Returns: 'sync clean' / compile errors / timeout message.
    """
    if _send is None:
        raise ToolError("sync_unity requires a Unity connection (no bridge)")

    # Step 1: bump version if requested (before sync so C# picks up the new package.json)
    if bump:
        pkg = _package_json_path()
        if pkg is None:
            raise ToolError("bump=True requires unity-plugin/package.json (not found — standalone install?)")
        from unity_mcp.scripts.bump_version import bump_patch
        new_ver = bump_patch(pkg)
        resolve = True  # bump implies resolve

    # Step 2: trigger sync
    try:
        ack = await _send("sync", {"resolve": "true" if resolve else "false"})
    except ConnectionError as e:
        raise ToolError(f"Unity unreachable: {e}") from e

    try:
        epoch, will_compile = _parse_ack(ack)
    except (ValueError, KeyError, IndexError):
        epoch, will_compile = 0, True  # conservative

    # Step 3: fast path — nothing to compile
    if not will_compile:
        errors = await _get_errors()
        return errors if errors else "sync clean (no compile needed)"

    # Step 4-5: poll until epoch match + state=ready/failed
    deadline = time.monotonic() + timeout
    started = time.monotonic()

    while True:
        if time.monotonic() > deadline:
            errors = await _get_errors()
            msg = f"timeout after {timeout:.0f}s — sync still in progress"
            return f"{msg}\n{errors}" if errors else msg

        try:
            status = await _send("sync_status", {})
        except (ConnectionError, DomainReloadError):
            # Domain reload: Unity restarting — wait and retry
            await asyncio.sleep(_POLL_INTERVAL)
            continue

        try:
            s_epoch, state, err = _parse_status(status)
        except (ValueError, KeyError):
            await asyncio.sleep(_POLL_INTERVAL)
            continue

        # macOS/Unity 6 defers compilation while the editor is backgrounded:
        # dur stays 0.0 (compile never started) yet C# can't self-heal because
        # isCompiling is held true. Tell the user instead of burning the timeout.
        if (state == "compiling" and "dur=0.0" in status
                and time.monotonic() - started > _FOCUS_HINT_AFTER):
            return ("Unity appears backgrounded — compilation has not started "
                    f"in {_FOCUS_HINT_AFTER:.0f}s. Click the Unity window to let "
                    "it compile, then re-run sync_unity.")

        # R-3: epoch mismatch → stale status, keep polling
        if s_epoch != epoch:
            await asyncio.sleep(_POLL_INTERVAL)
            continue

        if state == "failed":
            # R-2: compile failed → no reload will happen, report errors
            errors = await _get_errors()
            if errors:
                return errors
            return f"compile failed: {err}" if err else "compile failed"

        if state == "ready":
            errors = await _get_errors()
            return errors if errors else "sync clean"

        # state = compiling/reloading/idle — keep polling
        await asyncio.sleep(_POLL_INTERVAL)


async def _get_errors() -> str:
    """Get compile errors from C# and corroborate with editor_log (both-signals gate, MAJOR-3)."""
    try:
        csharp = await _send("get_compile_errors", {})
    except ConnectionError:
        return ""
    out = editor_log.corroborate(csharp)
    # C#'s "clean" sentinel is not an error — only surface corroborator additions.
    if csharp.strip() == "No compilation errors" and out == csharp:
        return ""
    return out


def register(mcp, send, args):
    global _send
    _send = send
    editor_log.init_corroboration()
    from ._annotations import RW as _RW
    mcp.tool(annotations=_RW)(sync_unity)
