"""Out-of-band compile verifier: reads Unity's Editor.log from disk.

Does NOT depend on the C# plugin being compilable — pure disk IO.
Used by get_compile_errors / await_compile to corroborate "clean" responses
when the plugin dll may be stale.

Unity 6 (Bee/Csc) log format — FAILED compile writes this header line:
    ## Script Compilation Error for: Csc Library/Bee/.../UnityMCP.Editor.dll (+2 others)

A successful compile writes NO such header. We rfind this header as the failure anchor
instead of rfind(ExitCode) to avoid being fooled by a parallel assembly that later
succeeds (ExitCode=0 would be the last ExitCode, giving a false-negative).

The errors live in the ##### Output block BEFORE the header (lines 79707-79733 in real log).
"""
import re
from dataclasses import dataclass, field
from pathlib import Path

from .editor_log_parser import (  # noqa: F401 — re-export for back-compat
    get_editor_log_path,
    get_editor_prev_log_path,
    parse_compile_errors_from_log,
    parse_build_failure,
    classify_failure_currency,
    BuildFailure,
)

# Module-level cached state for centralized corroboration
_cor_project_path = None
_cor_log_path = None
_cor_source_dirs = None   # legacy — kept for back-compat with tests that patch it
_cor_source_files = None  # RC-4: scoped list (excludes Chat/Tests asmdefs)


def find_plugin_source_files(plugin_dir: "Path | None" = None) -> "list[Path]":
    """Return .cs files under plugin_dir that belong ONLY to UnityMCP.Editor assembly.

    Excludes .cs files under any directory that contains a .asmdef whose stem is
    NOT 'UnityMCP.Editor' (e.g. Chat, Tests, Chat.Tests asmdefs).
    plugin_dir=None → auto-resolve from repo root (same as find_plugin_source_dir).
    """
    if plugin_dir is None:
        repo_root = Path(__file__).resolve().parents[3]
        plugin_dir = repo_root / "unity-plugin"
    if not plugin_dir.exists():
        return []

    # Find all dirs that have a foreign asmdef (stem != 'UnityMCP.Editor')
    excluded_dirs: set[Path] = set()
    for asmdef in plugin_dir.rglob("*.asmdef"):
        if asmdef.stem != "UnityMCP.Editor":
            excluded_dirs.add(asmdef.parent.resolve())

    result = []
    for cs in plugin_dir.rglob("*.cs"):
        cs_resolved = cs.resolve()
        if not any(
            cs_resolved == excl or excl in cs_resolved.parents
            for excl in excluded_dirs
        ):
            result.append(cs)
    return result


def find_plugin_source_dir() -> "list[Path] | None":
    """Return [repo/unity-plugin] if it exists and contains .cs files; else None.

    Walks up from this file: server/src/unity_mcp/editor_log.py → parents[3] = repo root.
    In an installed/standalone server there is no sibling unity-plugin → None (correct:
    end users don't edit the plugin so freshness is undeterminable → trust C#).
    """
    repo_root = Path(__file__).resolve().parents[3]
    plugin_dir = repo_root / "unity-plugin"
    if plugin_dir.exists() and next(plugin_dir.rglob("*.cs"), None) is not None:
        return [plugin_dir]
    return None


def check_dll_freshness(
    project_path: Path,
    source_dirs: "list[Path] | None" = None,
    grace_s: float = 10.0,
    source_files: "list[Path] | None" = None,
) -> "bool | None":
    """Compare UnityMCP.Editor.dll mtime vs plugin .cs files.

    source_files (preferred): explicit list of .cs files — no rglob of foreign asmdefs.
    source_dirs (legacy): directories to rglob for *.cs (may include foreign asmdefs).
    source_dirs=None/[] and source_files=None → None (undeterminable).
    Returns True (fresh), False (stale), None (undeterminable).
    """
    dll = project_path / "Library" / "ScriptAssemblies" / "UnityMCP.Editor.dll"
    if not dll.exists():
        return None

    if source_files is not None:
        cs_files = source_files
    elif source_dirs:
        cs_files = [f for d in source_dirs for f in d.rglob("*.cs")]
    else:
        return None

    if not cs_files:
        return None

    dll_mtime = dll.stat().st_mtime
    max_cs_mtime = max(f.stat().st_mtime for f in cs_files)
    return (dll_mtime + grace_s) >= max_cs_mtime


def corroborate_compile_status(
    csharp_response: str,
    project_path: "Path | None" = None,
    log_path: "Path | None" = None,
    source_dirs: "list[Path] | None" = None,
    source_files: "list[Path] | None" = None,
    compile_status: str = "",
) -> str:
    """Corroborate a "clean" C# response against Editor.log.

    Override C#'s "clean" ONLY when BOTH signals present:
      - log has error lines (stale failure block)
      - dll is definitively stale (fresh is False)

    3rd-signal gate (P2): stale log CS errors are only resurrected when
    compile_status == "idle-failed" — prevents false positives from old Bee
    blocks after a fix has been applied but the log block lingers.

    A fresh or undeterminable dll means the log error is stale → trust C#.

    source_files (preferred): scoped list from find_plugin_source_files() — excludes Chat/Tests.
    source_dirs (legacy): rglobs dirs (may pick up Chat/Tests asmdefs).
    """
    if "error CS" in csharp_response:
        return csharp_response

    # No log path → graceful pass-through (CI without Unity, tests with mocked _send).
    if log_path is None:
        return csharp_response

    # Prefer source_files (scoped); fall back to source_dirs; then auto-detect (legacy).
    if source_files is not None:
        fresh = (
            check_dll_freshness(project_path, source_files=source_files)
            if project_path is not None
            else None
        )
    else:
        effective_source_dirs = source_dirs if source_dirs is not None else find_plugin_source_dir()
        fresh = (
            check_dll_freshness(project_path, source_dirs=effective_source_dirs)
            if project_path is not None
            else None
        )

    log_errors = parse_compile_errors_from_log(log_path)

    if log_errors and fresh is False:
        # 3rd-signal gate: only resurrect stale log block when C# confirms idle-failed.
        # Without this, a lingering Bee failure block from a previous run causes FP.
        if compile_status and compile_status != "idle-failed":
            # compile_status says we're not in a failed state → log block is stale, trust C#
            pass
        else:
            # Both signals confirmed (+ optional idle-failed agreement) → genuine stale dll.
            return "[editor.log - dll stale]\n" + "\n".join(log_errors)

    if fresh is False:
        # Stale dll but no log errors → soft warn only.
        return csharp_response + "\n[warn: UnityMCP.Editor.dll may be stale - consider recompiling]"

    return csharp_response


def init_corroboration(port: "int | None" = None) -> None:
    """Autodetect + cache project/log paths and scoped plugin source files once at startup.

    UNITY_MCP_PROJECT_PATH override wins over port-file autodetect.
    Idempotent — safe to call from multiple register() functions.
    port: Unity TCP port used to look up project path from port file (RC-3).
    """
    global _cor_project_path, _cor_log_path, _cor_source_files
    from .compile_state import CompileStateProbe  # lazy import — avoid circular at module load
    _cor_project_path = CompileStateProbe.autodetect_project_path(port=port)
    _cor_log_path = get_editor_log_path()
    # RC-4: use scoped file list (excludes Chat/Tests asmdefs) — not the wide rglob dir list
    _cor_source_files = find_plugin_source_files()


def corroborate(csharp_response: str) -> str:
    """Corroborate using cached project/log/source_files set by init_corroboration()."""
    return corroborate_compile_status(
        csharp_response,
        _cor_project_path,
        _cor_log_path,
        source_files=_cor_source_files,
    )


@dataclass
class WedgeReport:
    """Result of detect_wedge — describes the current wedge type and its evidence."""
    kind: str                         # 'build-failed-wedge' | 'stale-cache'
    cs_errors: list[str] = field(default_factory=list)
    failed_dlls: list[str] = field(default_factory=list)
    log_path: "Path | None" = None


def detect_wedge(
    log_path: "Path | None" = None,
    project_path: "Path | None" = None,
) -> "WedgeReport | None":
    """Pure disk authority: detect a reload wedge without needing TCP.

    M4: consults BOTH Editor.log AND Editor-prev.log; takes the most-recent
    reload-terminal across both (incident often rolls to -prev.log).

    Returns WedgeReport when a wedge is detected, None when clean.
    Refines to 'stale-cache' when EVERY cs_error crosschecks as stale-on-disk.
    """
    # Resolve log paths
    primary = log_path or get_editor_log_path()
    prev = None
    if primary is not None:
        prev_candidate = primary.parent / "Editor-prev.log"
        if prev_candidate.exists():
            prev = prev_candidate
    else:
        prev = get_editor_prev_log_path()

    best_text: str | None = None
    best_path: Path | None = None

    def _read(p: "Path | None") -> "tuple[str, Path] | None":
        if p is None or not p.exists():
            return None
        try:
            return p.read_text(encoding="utf-8", errors="replace"), p
        except OSError:
            return None

    primary_data = _read(primary)
    prev_data = _read(prev)

    if primary_data is None and prev_data is None:
        return None

    # Pick the log that contains a CURRENT failure (content-based selection).
    # Unity rotates logs by writing Editor-prev.log FIRST then opening a fresh
    # Editor.log, so Editor.log is always newer by mtime — mtime cannot be used.
    # Content wins; mtime is only a tiebreaker when both or neither are "current".
    if primary_data and prev_data:
        primary_bf = parse_build_failure(primary_data[0])
        prev_bf = parse_build_failure(prev_data[0])
        primary_currency = classify_failure_currency(primary_data[0], primary_bf)
        prev_currency = classify_failure_currency(prev_data[0], prev_bf)
        if primary_currency == "current":
            best_text, best_path = primary_data
        elif prev_currency == "current":
            best_text, best_path = prev_data
        else:
            # Neither is current — fall back to mtime (prefer newer)
            primary_mtime = primary.stat().st_mtime if primary and primary.exists() else 0
            prev_mtime = prev.stat().st_mtime if prev and prev.exists() else 0
            if prev_mtime > primary_mtime:
                best_text, best_path = prev_data
            else:
                best_text, best_path = primary_data
    elif primary_data:
        best_text, best_path = primary_data
    else:
        best_text, best_path = prev_data  # type: ignore[assignment]

    bf = parse_build_failure(best_text)
    currency = classify_failure_currency(best_text, bf)

    if currency != "current":
        return None

    # We have a current failure — build the report
    report = WedgeReport(
        kind="build-failed-wedge",
        cs_errors=bf.cs_errors,
        failed_dlls=bf.failed_dlls,
        log_path=best_path,
    )

    # Refine to stale-cache if EVERY cs_error crosschecks as stale-on-disk
    if bf.cs_errors and _all_errors_stale_on_disk(bf.cs_errors):
        report.kind = "stale-cache"

    return report


def _all_errors_stale_on_disk(cs_error_lines: list[str]) -> bool:
    """Return True iff every CS error line crosschecks as stale-on-disk."""
    # Parse each error line: "path(line,col): error CS####: msg"
    _RE_ERR_LINE = re.compile(r"^(.*?)\((\d+),\d+\):\s+error CS\d+:.*'(\w+)'[^']*'[^.]+\.(\w+)\(\)'")
    for line in cs_error_lines:
        m = _RE_ERR_LINE.match(line.strip())
        if not m:
            return False  # can't parse → ambiguous → real error wins
        file_path, lineno, type_name, member = m.groups()
        result = crosscheck_error_on_disk({
            "file": file_path,
            "line": int(lineno),
            "member": member,
            "type_name": type_name,
        })
        if result != "stale-on-disk":
            return False
    return True


def crosscheck_error_on_disk(cs_error: dict) -> str:
    """Check if a CS error's missing member still exists on disk.

    C2 fix: the named member token must appear inside the BRACE-SCOPE of the NAMED type,
    not anywhere in the file (StartTickPump appears 16×).

    Returns:
        'stale-on-disk'  — member found inside the named type's brace-scope → error is fixed
        'matches'        — member NOT found in scope → real error (or any ambiguity → real wins)

    On ANY parse ambiguity / missing-file → 'matches' (real error wins, never hide live errors).
    """
    file_path = cs_error.get("file", "")
    member = cs_error.get("member", "")
    type_name = cs_error.get("type_name", "")

    if not (file_path and member and type_name):
        return "matches"

    try:
        text = Path(file_path).read_text(encoding="utf-8", errors="replace")
    except (OSError, PermissionError):
        return "matches"

    # Find the brace-scope of the NAMED type.
    # Deliberate: matches only `class` — CS0535 on a `struct`/`record` falls through
    # to "matches" (real error wins), which is safe per C2.
    type_match = re.search(
        r"\bclass\s+" + re.escape(type_name) + r"\b[^{]*\{",
        text,
    )
    if not type_match:
        # Type not found → ambiguous → real error wins
        return "matches"

    # Walk forward from the type's opening brace to find matching closing brace
    start = type_match.end() - 1  # position of the '{'
    depth = 0
    end = len(text)
    for i in range(start, len(text)):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                end = i + 1
                break

    scope_text = text[start:end]
    # Check each line in scope: member must appear on a non-comment code line
    # to count as "implemented" (a comment mentioning the name is NOT an implementation).
    for line in scope_text.splitlines():
        stripped = line.strip()
        if stripped.startswith("//") or stripped.startswith("*"):
            continue
        if re.search(r"\b" + re.escape(member) + r"\b", stripped):
            return "stale-on-disk"
    return "matches"


async def get_corroborated_errors(send) -> str:
    """Shared helper: get compile errors from C#, corroborate, strip clean sentinel.

    Sentinel-strip lives in exactly one place (P3). Both sync.py and code_intel.py
    import this. Returns "" when C# says "No compilation errors" and log agrees.
    """
    try:
        csharp = await send("get_compile_errors", {})
    except ConnectionError:
        return ""
    out = corroborate(csharp)
    # Strip the clean sentinel — it's not an error payload.
    if csharp.strip() == "No compilation errors" and out == csharp:
        return ""
    return out
