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
from pathlib import Path

from .editor_log_parser import (  # noqa: F401 — re-export for back-compat
    get_editor_log_path,
    parse_compile_errors_from_log,
)

# Module-level cached state for centralized corroboration
_cor_project_path = None
_cor_log_path = None
_cor_source_dirs = None


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
