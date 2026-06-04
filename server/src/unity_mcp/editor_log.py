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
import os
import re
import sys
from pathlib import Path

# Matches: Assets/Foo.cs(12,5): error CS0117: message
_RE_ERROR = re.compile(r"^(.*?)\((\d+),(\d+)\):\s+(error CS\d+:.*)")
_STRONG_BROKEN = "All compiler errors have to be fixed"
_OUTPUT_MARKER = "##### Output"
# Unity 6 Bee failure-diagnostic marker — only present in FAILED compiles
_FAILURE_HEADER = "## Script Compilation Error for:"
# Lines starting with these strings end the ##### Output section
_SECTION_BOUNDARIES = ("##### ", "## ", "*** ")

# Module-level cached state for centralized corroboration
_cor_project_path = None
_cor_log_path = None
_cor_source_dirs = None


def get_editor_log_path(env_override: str | None = None) -> "Path | None":
    """Return Editor.log path. env_override or UNITY_MCP_EDITOR_LOG env wins.

    When env override points to a non-existent path, warns to stderr (user explicitly
    asked for corroboration; silent degradation would be confusing).
    """
    raw = env_override or os.environ.get("UNITY_MCP_EDITOR_LOG")
    if raw:
        p = Path(raw)
        if not p.exists():
            print(f"warning: UNITY_MCP_EDITOR_LOG path does not exist: {raw}", file=sys.stderr)
        return p  # return even if not exist; caller decides

    home = Path.home()
    platform = sys.platform
    if platform == "darwin":
        p = home / "Library" / "Logs" / "Unity" / "Editor.log"
    elif platform == "win32":
        local = os.environ.get("LOCALAPPDATA", "")
        p = Path(local) / "Unity" / "Editor" / "Editor.log" if local else None
    else:
        p = home / ".config" / "unity3d" / "Editor.log"

    if p is None:
        return None
    return p if p.exists() else None


def parse_compile_errors_from_log(log_path: Path, max_bytes: int = 256_000) -> "list[str]":
    """Read last max_bytes of log, find LATEST Csc FAILURE block, return error lines.

    Anchors on '## Script Compilation Error for:' (Unity 6 Bee failure marker).
    A successful compile never writes this header, so a parallel assembly that
    succeeds AFTER a failure cannot cause a false-negative.

    Returns [] if no failure header found (clean or header outside tail window).
    Never raises.
    """
    try:
        size = log_path.stat().st_size
        offset = max(0, size - max_bytes)
        with open(log_path, "r", encoding="utf-8", errors="replace") as f:
            if offset:
                f.seek(offset)
            text = f.read()
    except (OSError, PermissionError):
        return []

    # Anchor: last failure header in the tail window.
    # If not found → no failed compile in this window → clean.
    header_pos = text.rfind(_FAILURE_HEADER)
    if header_pos == -1:
        # Fall back to legacy format (no ## Script Compilation Error header):
        # check for ##### ExitCode with nonzero value + ##### Output
        return _parse_legacy_format(text)

    # The ##### Output block with actual errors lives BEFORE the header.
    # Find the last ##### Output that precedes the header.
    output_pos = text.rfind(_OUTPUT_MARKER, 0, header_pos)
    if output_pos == -1:
        return _collect_strong_broken(text[:header_pos])

    # Collect error lines from ##### Output up to the header (or next section boundary)
    output_content = text[output_pos + len(_OUTPUT_MARKER):header_pos]
    return _collect_errors(output_content)


def _parse_legacy_format(text: str) -> "list[str]":
    """Fallback: parse old-format logs without the ## Script Compilation Error header.

    rfind picks the LAST ExitCode block — on legacy single-assembly Csc that's the only
    block, so this is correct. Unity 6 never reaches this path (it always writes the
    '## Script Compilation Error for:' header), so multi-assembly ambiguity is not
    a concern here; single-assembly legacy is an accepted trade-off.
    """
    exit_marker = "##### ExitCode"
    exit_pos = text.rfind(exit_marker)
    if exit_pos == -1:
        return []

    after = text[exit_pos + len(exit_marker):]
    exit_code_str = next((l.strip() for l in after.splitlines() if l.strip()), "")
    try:
        if int(exit_code_str) == 0:
            return []
    except ValueError:
        pass  # treat as failure

    output_start = after.find(_OUTPUT_MARKER)
    if output_start == -1:
        return []
    return _collect_errors(after[output_start + len(_OUTPUT_MARKER):])


def _collect_errors(output_content: str) -> "list[str]":
    """Extract CS error lines from an Output block body."""
    errors: list[str] = []
    for line in output_content.splitlines():
        if any(line.startswith(b) for b in _SECTION_BOUNDARIES):
            break
        stripped = line.strip()
        m = _RE_ERROR.match(stripped)
        if m:
            filepath, lineno, col, rest = m.groups()
            errors.append(f"{filepath}:{lineno}:{col}: {rest}")
            continue
        if _STRONG_BROKEN in stripped:
            errors.append("compile broken: All compiler errors have to be fixed")
    return errors


def _collect_strong_broken(text_before_header: str) -> "list[str]":
    """Check for STRONG_BROKEN signal before the failure header."""
    for line in text_before_header.splitlines():
        if _STRONG_BROKEN in line:
            return ["compile broken: All compiler errors have to be fixed"]
    return []


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
) -> "bool | None":
    """Compare UnityMCP.Editor.dll mtime vs plugin .cs files under source_dirs.

    Returns True (fresh), False (stale), None (undeterminable).
    source_dirs=None/[] → None — do NOT fall back to project rglob (wrong + slow).
    """
    dll = project_path / "Library" / "ScriptAssemblies" / "UnityMCP.Editor.dll"
    if not dll.exists():
        return None

    if not source_dirs:
        return None

    cs_files = [f for d in source_dirs for f in d.rglob("*.cs")]
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
) -> str:
    """Corroborate a "clean" C# response against Editor.log.

    Override C#'s "clean" ONLY when BOTH signals present:
      - log has error lines (stale failure block)
      - dll is definitively stale (fresh is False)

    A fresh or undeterminable dll means the log error is stale → trust C#.
    This prevents false positives from lingering Bee/Csc failure blocks after a fix.
    """
    if "error CS" in csharp_response:
        return csharp_response

    # No log path → graceful pass-through (CI without Unity, tests with mocked _send).
    if log_path is None:
        return csharp_response

    # Resolve source_dirs: caller may pass explicit dirs or we auto-detect from repo.
    effective_source_dirs = source_dirs if source_dirs is not None else find_plugin_source_dir()
    fresh = (
        check_dll_freshness(project_path, source_dirs=effective_source_dirs)
        if project_path is not None
        else None
    )

    log_errors = parse_compile_errors_from_log(log_path)

    if log_errors and fresh is False:
        # Both signals confirmed → genuinely stale dll with matching log errors.
        return "[editor.log - dll stale]\n" + "\n".join(log_errors)

    if fresh is False:
        # Stale dll but no log errors → soft warn only.
        return csharp_response + "\n[warn: UnityMCP.Editor.dll may be stale - consider recompiling]"

    return csharp_response


def init_corroboration() -> None:
    """Autodetect + cache project/log paths and plugin source dirs once at startup.

    Idempotent — safe to call from multiple register() functions.
    """
    global _cor_project_path, _cor_log_path, _cor_source_dirs
    from .compile_state import CompileStateProbe  # lazy import — avoid circular at module load
    _cor_project_path = CompileStateProbe.autodetect_project_path()
    _cor_log_path = get_editor_log_path()
    _cor_source_dirs = find_plugin_source_dir()  # cached — fixes per-call double-rglob


def corroborate(csharp_response: str) -> str:
    """Corroborate using cached project/log/source_dirs set by init_corroboration()."""
    return corroborate_compile_status(
        csharp_response,
        _cor_project_path,
        _cor_log_path,
        source_dirs=_cor_source_dirs,
    )
