"""Executor for do() tool — runs batch + 1 retry on partial failure."""
import re
from typing import Optional, Callable, Set
from ..sampling_postproc import normalize
from .validator import validate_plan

# Matches '[N] err: ...' (actual Unity bridge batch format)
_ERR_LINE_RE = re.compile(r"^\[\d+\]\s+err:", re.IGNORECASE)


def _count_failures(result: str) -> list[str]:
    """Extract failed lines from batch result — matches '[N] err:' format."""
    failed = []
    for line in result.splitlines():
        if _ERR_LINE_RE.match(line):
            failed.append(line)
    return failed


def _is_partial(result: str) -> bool:
    """True if result contains any err lines or legacy PARTIAL: marker."""
    if "PARTIAL:" in result:
        return True
    return any(_ERR_LINE_RE.match(l) for l in result.splitlines())


class Executor:
    def __init__(self, send: Callable, sampling=None):
        self._send = send
        self._svc = sampling

    async def execute(
        self,
        plan: str,
        original_intent: str = "",
        scene_paths: Optional[Set[str]] = None,
    ) -> str:
        result = await self._send("batch", {"commands": plan, "on_error": "continue"})
        result_str = str(result)

        # Retry once on partial failure (≤5 failed lines)
        if _is_partial(result_str) and self._svc is not None:
            failed_lines = _count_failures(result_str)
            if 0 < len(failed_lines) <= 5:
                fix_prompt = (
                    f"Fix these failed Unity MCP commands (output only fixed commands, one per line):\n"
                    f"Original intent: {original_intent}\n"
                    f"Failed:\n" + "\n".join(failed_lines)
                )
                fixed = await self._svc.generate(fix_prompt, max_tokens=400)
                fixed, _ = normalize(fixed, "dsl")
                if fixed:
                    # Fix 11: validate retry plan before executing
                    err = validate_plan(fixed, scene_paths or set())
                    if err:
                        return result_str  # validation failed — return original result
                    retry = await self._send("batch", {"commands": fixed, "on_error": "continue"})
                    return str(retry)

        return result_str
