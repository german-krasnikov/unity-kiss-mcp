"""Pure parsing functions for Unity Editor.log.

No IO side-effects — all functions take strings/paths and return data.
Used by editor_log.py for corroboration logic.
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
