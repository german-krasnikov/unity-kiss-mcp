"""AskExecutor — runs a ToolPlan's steps via _send."""
from typing import Callable, Awaitable
from .plans import ToolPlan


class AskExecutor:
    def __init__(self, send: Callable):
        self._send = send

    async def run(self, plan: ToolPlan) -> list[str]:
        """Execute all steps in plan, return list of raw results."""
        results = []
        for tool, args in plan.steps:
            try:
                result = await self._send(tool, args)
                results.append(str(result))
            except Exception as e:
                results.append(f"ERROR: {e}")
        return results
