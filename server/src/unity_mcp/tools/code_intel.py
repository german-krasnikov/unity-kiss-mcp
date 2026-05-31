"""Code intelligence tools backed by Unity-side Roslyn workspace.

These tools are TIER1 — replace many existing read+grep+recompile patterns:
- find_references: replaces grep + multiple file reads (saves ~25 tool calls per rename)
- compile_preflight: catches typos in 100-300ms vs 30s Unity recompile cycle
- semantic_at: replaces read+reasoning for "what's at this position"

Phase A: Python tool wrappers only.
Phase B: Unity-side C# Roslyn impl (deferred — requires Unity Editor for testing).

NOTE: Until Phase B C# ships, calling these tools raises ToolError
("Command not registered: ..."). The [ROSLYN UNAVAILABLE: ...] response
documented in roslyn_responses.txt is the GRACEFUL FALLBACK Phase B C#
must emit when Roslyn DLLs fail to load AFTER the handler is registered.
Pre-Phase-B agents see hard errors — by design (fail-safe > silent wrong).
"""
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


def register(mcp, send, args):
    global _send
    _send = send
    from ._annotations import RO as _RO
    mcp.tool(annotations=_RO)(find_references)
    mcp.tool(annotations=_RO)(compile_preflight)
    mcp.tool(annotations=_RO)(semantic_at)
