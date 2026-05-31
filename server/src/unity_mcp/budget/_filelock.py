"""Cross-process file lock via fcntl. Linux + macOS only.

Used by CostTracker to serialize multi-process writes to budget.json.
Blocking exclusive lock — held briefly per write.
"""
from __future__ import annotations

import fcntl
from contextlib import contextmanager
from pathlib import Path


@contextmanager
def locked(path: Path):
    """Acquire fcntl exclusive lock on a sentinel `.lock` file next to path.

    Auto-releases on context exit. Sentinel file allows safe cross-process
    serialization without race on the data file itself.
    """
    sentinel = path.with_suffix(path.suffix + ".lock")
    sentinel.parent.mkdir(parents=True, exist_ok=True)
    f = open(sentinel, "w")
    try:
        fcntl.flock(f.fileno(), fcntl.LOCK_EX)
        yield
    finally:
        try:
            fcntl.flock(f.fileno(), fcntl.LOCK_UN)
        except Exception:
            pass
        f.close()
