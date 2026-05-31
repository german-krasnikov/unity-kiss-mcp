"""ToolHinter — post-call discoverability hints for suboptimal tool-use patterns.

Appends a single [HINT: ... — saves ~Ntok] line when a known anti-pattern is detected.
Self-suppressing after 2 consecutive ignores. Cooldown: 8 calls between emits per pattern.
"""
import os
from collections import deque
from dataclasses import dataclass
from typing import Callable, Optional

# keep in sync with middleware.WRITE_CMDS
_WRITE_CMDS: frozenset[str] = frozenset({
    "set_property", "set_property_delta", "create_object", "delete_object",
    "manage_component", "wire_event", "set_active", "set_material",
    "set_runtime_property", "set_rect", "move_to", "batch",
    "animation", "timeline", "animator", "particle", "shader",
    "material", "prefab", "scriptable_object", "asset", "scene",
})


@dataclass(frozen=True)
class Call:
    cmd: str
    key: tuple


@dataclass(frozen=True)
class Pattern:
    id: str
    predicate: Callable[[deque, "Call"], bool]
    suggested_cmd: str
    hint: str
    trigger_cmd: str


def _key(cmd: str, args: dict) -> tuple:
    if cmd in ("get_component", "inspect", "get_object_detail"):
        return ("read", args.get("path", ""), args.get("type") or args.get("component", ""))
    if cmd in ("set_property", "set_property_delta"):
        return ("write", args.get("path", ""), args.get("component", ""),
                args.get("prop") or args.get("field", ""))
    if cmd == "set_active":
        return ("active", args.get("path", ""))
    if cmd == "delete_object":
        return ("delete", args.get("path", ""))
    if cmd == "screenshot":
        return ("snap",)
    if cmd in ("get_console", "recompile", "get_compile_errors"):
        return (cmd,)
    return (cmd,)


def _write_between(recent: deque) -> bool:
    """True if any write cmd appears in recent."""
    return any(c.cmd in _WRITE_CMDS for c in recent)


def _count_cmd_in_window(recent: deque, cmd: str, n: int) -> int:
    return sum(1 for c in list(recent)[-n:] if c.cmd == cmd)


def _count_key_in_window(recent: deque, key_prefix: tuple, n: int) -> int:
    return sum(1 for c in list(recent)[-n:] if c.key[:len(key_prefix)] == key_prefix)


def _cmd_in_window(recent: deque, cmd: str, n: int) -> bool:
    return any(c.cmd == cmd for c in list(recent)[-n:])


# ── Pattern predicates ────────────────────────────────────────────────────────

def _pred_inspect_loop(recent: deque, call: Call) -> bool:
    if call.cmd != "get_component":
        return False
    return _count_cmd_in_window(recent, "get_component", 8) >= 2


def _pred_batch_writes(recent: deque, call: Call) -> bool:
    if call.cmd != "set_property":
        return False
    path = call.key[1]
    component = call.key[2]
    same_target = ("write", path, component)
    return _count_key_in_window(recent, same_target, 6) >= 2


def _pred_find_then_read(recent: deque, call: Call) -> bool:
    if call.cmd != "get_component":
        return False
    return _cmd_in_window(recent, "find_objects", 4)


def _pred_screenshot_spam(recent: deque, call: Call) -> bool:
    if call.cmd != "screenshot":
        return False
    window = list(recent)[-8:]
    if sum(1 for c in window if c.cmd == "screenshot") < 2:
        return False
    # No write between screenshots
    return not _write_between(deque(window))


def _pred_console_poll(recent: deque, call: Call) -> bool:
    if call.cmd != "get_console":
        return False
    window = list(recent)[-6:]
    has_recompile = any(c.cmd == "recompile" for c in window)
    console_count = sum(1 for c in window if c.cmd == "get_console")
    return has_recompile and console_count >= 2


def _pred_redundant_verify(recent: deque, call: Call) -> bool:
    if call.cmd != "get_component":
        return False
    if os.environ.get("UNITY_MCP_REFLECT", "1") == "0":
        return False
    path = call.key[1]
    component = call.key[2] if len(call.key) > 2 else ""
    # Look for set_property on same (path, component) in last 3
    return any(
        c.cmd == "set_property" and c.key[1] == path and c.key[2] == component
        for c in list(recent)[-3:]
    )


_PATTERNS: list[Pattern] = [
    Pattern(
        id="inspect-loop",
        predicate=_pred_inspect_loop,
        suggested_cmd="inspect",
        hint="[HINT: 3+ get_component calls — use inspect(paths=...) for one trip — saves ~80tok]",
        trigger_cmd="get_component",
    ),
    Pattern(
        id="batch-writes",
        predicate=_pred_batch_writes,
        suggested_cmd="batch",
        hint="[HINT: 3+ writes to same component — use batch or set_property_delta — saves ~120tok]",
        trigger_cmd="set_property",
    ),
    Pattern(
        id="find-then-read",
        predicate=_pred_find_then_read,
        suggested_cmd="inspect",
        hint="[HINT: find_objects → get_component pattern — use inspect to fold both — saves ~60tok]",
        trigger_cmd="get_component",
    ),
    Pattern(
        id="screenshot-spam",
        predicate=_pred_screenshot_spam,
        suggested_cmd="fingerprint",
        hint="[HINT: repeated screenshot without writes — try fingerprint for cheap state diff — saves ~500tok]",
        trigger_cmd="screenshot",
    ),
    Pattern(
        id="console-poll",
        predicate=_pred_console_poll,
        suggested_cmd="wait_until",
        hint="[HINT: post-recompile console polling — use wait_until or get_compile_errors — saves ~300tok]",
        trigger_cmd="get_console",
    ),
    Pattern(
        id="redundant-verify-read",
        predicate=_pred_redundant_verify,
        suggested_cmd="",
        hint="[HINT: set_property already returns snapshot via reflect — skip the verify read — saves ~50tok]",
        trigger_cmd="get_component",
    ),
]


class ToolHinter:
    _COOLDOWN = 8

    def __init__(self, enabled: bool = True):
        self.enabled = enabled
        self._recent: deque[Call] = deque(maxlen=16)
        self._emitted_at: dict[str, int] = {}
        self._ignored: dict[str, int] = {}
        self._suppressed: set[str] = set()
        self._call_idx: int = 0
        self._last_hint_pattern: Optional[str] = None
        self._patterns: list[Pattern] = list(_PATTERNS)

    def _check_adoption(self, cmd: str) -> None:
        """Check if previous hint was adopted or ignored."""
        if not self._last_hint_pattern:
            return
        pid = self._last_hint_pattern
        pat = next((p for p in self._patterns if p.id == pid), None)
        if pat is None:
            return

        from .metrics import METRICS
        # Adoption: called the suggested cmd
        if pat.suggested_cmd and cmd == pat.suggested_cmd:
            METRICS.inc(f"hint.adopted.{pid}")
            self._ignored[pid] = 0
            self._last_hint_pattern = None
            return

        # Ignore: repeated the suboptimal pattern (same triggering cmd)
        if cmd == pat.trigger_cmd:
            METRICS.inc(f"hint.ignored.{pid}")
            self._ignored[pid] = self._ignored.get(pid, 0) + 1
            if self._ignored[pid] >= 2:
                self._suppressed.add(pid)
                from .metrics import METRICS as M
                M.inc(f"hint.suppressed.{pid}")
            self._last_hint_pattern = None

    def observe(self, cmd: str, args: dict) -> Optional[str]:
        if not self.enabled:
            return None

        from .metrics import METRICS
        self._check_adoption(cmd)

        call = Call(cmd=cmd, key=_key(cmd, args))

        hint_result: Optional[str] = None
        for pat in self._patterns:
            if pat.id in self._suppressed:
                continue
            last = self._emitted_at.get(pat.id, -999)
            if self._call_idx - last < self._COOLDOWN:
                continue
            try:
                if pat.predicate(self._recent, call):
                    hint_result = pat.hint
                    self._emitted_at[pat.id] = self._call_idx
                    self._last_hint_pattern = pat.id
                    METRICS.inc(f"hint.emitted.{pat.id}")
                    break  # one hint per call
            except Exception:
                METRICS.inc("hinter.error")

        self._recent.append(call)
        self._call_idx += 1
        return hint_result

    def note_adoption(self, cmd: str) -> None:
        """Called at START of wrap_send to track adoption before state changes."""
        self._check_adoption(cmd)
