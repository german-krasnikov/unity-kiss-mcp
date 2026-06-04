"""Code intelligence tools backed by Unity-side Roslyn workspace.

These tools are TIER1 — replace many existing read+grep+recompile patterns:
- find_references: replaces grep + multiple file reads (saves ~25 tool calls per rename)
- compile_preflight: catches typos in 100-300ms vs 30s Unity recompile cycle
- semantic_at: replaces read+reasoning for "what's at this position"
- await_compile: block until compile finishes, then return errors (if any)

Phase A: Python tool wrappers only.
Phase B: Unity-side C# Roslyn impl (deferred — requires Unity Editor for testing).

NOTE: Until Phase B C# ships, calling these tools raises ToolError
("Command not registered: ..."). The [ROSLYN UNAVAILABLE: ...] response
documented in roslyn_responses.txt is the GRACEFUL FALLBACK Phase B C#
must emit when Roslyn DLLs fail to load AFTER the handler is registered.
Pre-Phase-B agents see hard errors — by design (fail-safe > silent wrong).
"""
import asyncio
import time
from typing import Any

_send = None


async def find_references(symbol: str, kind: str = "", scope: str = "") -> str:
    """Find all C# references to a symbol (Roslyn). Replaces grep+reads for renames.
    kind: disambiguator — class|field|method|property|param|local|namespace. scope: asm name (empty=all).
    Returns SYMBOL header + file:line:col refs; AMBIGUOUS (with kind options) / NOT FOUND (with candidates) / [ROSLYN UNAVAILABLE]."""
    args: dict[str, Any] = {"symbol": symbol}
    if kind:
        args["kind"] = kind
    if scope:
        args["scope"] = scope
    return await _send("find_references", args, timeout=10.0)


async def compile_preflight(file_path: str, new_content: str) -> str:
    """Validate C# WITHOUT writing/recompiling (Roslyn). Use before writing .cs — catches typos in ~200ms vs 30s recompile.
    file_path: Assets-relative. new_content: full file. Returns OK preflight (ms) / ERR preflight + diagnostics / [ROSLYN UNAVAILABLE]."""
    args: dict[str, Any] = {"file_path": file_path, "new_content": new_content}
    return await _send("compile_preflight", args, timeout=15.0)


async def semantic_at(file_path: str, line: int, col: int) -> str:
    """Symbol/type info at a file position (Roslyn). Replaces read±20 lines + type reasoning.
    file_path: Assets-relative. line/col: 1-based. Returns kind + decl location + namespace + signature/members / NO SYMBOL / [ROSLYN UNAVAILABLE]."""
    args: dict[str, Any] = {"file_path": file_path, "line": int(line), "col": int(col)}
    return await _send("semantic_at", args, timeout=10.0)  # 10s — assumes warm Roslyn workspace; cold start may need more


def _parse_status(status: str) -> tuple[str, float]:
    """Parse 'state|number' → (state, number). Unknown format → ('idle', 0.0)."""
    try:
        state, val = status.split("|", 1)
        return state.strip(), float(val)
    except (ValueError, AttributeError):
        return "idle", 0.0


async def await_compile(timeout: float = 60.0) -> str:
    """Block until Unity finishes compiling + reloading, then return compile errors.
    Use after writing .cs files instead of sleep. Returns errors or 'compile clean (Xs)'.
    Handles domain reload disconnects transparently. timeout=0 → immediate check, no loop."""
    async def _get_errors() -> str:
        try:
            csharp = await _send("get_compile_errors", {})
        except ConnectionError:
            return ""
        from .. import editor_log
        return editor_log.corroborate(csharp)

    # timeout=0: single check, no loop
    if timeout == 0:
        try:
            status = await _send("compile_status", {})
            state, _ = _parse_status(status)
            if state != "idle":
                return "still compiling"
        except ConnectionError:
            pass
        return await _get_errors()

    deadline = time.monotonic() + timeout

    while True:
        elapsed_total = time.monotonic() - (deadline - timeout)
        if time.monotonic() > deadline:
            errors = await _get_errors()
            msg = f"timeout after {elapsed_total:.1f}s — compile still in progress"
            return f"{msg}\n{errors}" if errors else msg

        try:
            status = await _send("compile_status", {})
        except ConnectionError:
            # Domain reload (DomainReloadError is-a ConnectionError): Unity is restarting — wait and retry
            await asyncio.sleep(1)
            continue

        state, duration = _parse_status(status)

        if state == "idle":
            errors = await _get_errors()
            return errors if errors else f"compile clean ({duration}s)"

        # still compiling — poll
        await asyncio.sleep(1)


def register(mcp, send, args):
    global _send
    _send = send
    from .. import editor_log
    editor_log.init_corroboration()
    from ._annotations import RO as _RO
    mcp.tool(annotations=_RO)(find_references)
    mcp.tool(annotations=_RO)(compile_preflight)
    mcp.tool(annotations=_RO)(semantic_at)
    mcp.tool(annotations=_RO)(await_compile)
