"""Server-side LLM verification via claude CLI.

Enable: UNITY_MCP_VISUAL_VERIFY=1
Uses `claude -p` for cheap/fast verification. Zero API keys needed.
"""
import asyncio
import os
from typing import Optional

CLAUDE_CMD = os.environ.get("UNITY_MCP_CLAUDE_CMD", "claude")

_budget_tracker = None
_budget_router = None


def init_budget(tracker, router) -> None:
    """Called from server.py lifespan to wire budget tracking."""
    global _budget_tracker, _budget_router
    _budget_tracker = tracker
    _budget_router = router


def _gate(feature: str, difficulty: float) -> bool:
    """Returns True if call should proceed. No-op when budget not initialized."""
    if _budget_router is None:
        return True
    decision = _budget_router.should_run(feature, difficulty)
    return decision.run


def _record(feature: str, has_image: bool = None) -> None:
    """Sync record — kept for legacy/CLI paths without event loop."""
    if _budget_tracker is None:
        return
    from .budget.registry import get_feature
    meta = get_feature(feature)
    image = meta.image if has_image is None else has_image
    _budget_tracker.record(feature, meta.est_in, meta.est_out, image)


async def _record_async(feature: str, has_image: bool = None) -> None:
    """Async-safe budget record. Use in production async paths."""
    if _budget_tracker is None:
        return
    from .budget.registry import get_feature
    meta = get_feature(feature)
    image = meta.image if has_image is None else has_image
    await _budget_tracker.record_async(feature, meta.est_in, meta.est_out, image)


class SamplingService:
    """Claude CLI calls for server-side verification."""

    _semaphore: Optional[asyncio.Semaphore] = None

    @classmethod
    def _get_semaphore(cls) -> asyncio.Semaphore:
        if cls._semaphore is None:
            limit = int(os.environ.get("UNITY_MCP_VISUAL_CONCURRENCY", "4"))
            cls._semaphore = asyncio.Semaphore(limit)
        return cls._semaphore

    @property
    def enabled(self) -> bool:
        return os.environ.get("UNITY_MCP_VISUAL_VERIFY") == "1"

    async def _run(self, args: list, timeout: float) -> Optional[str]:
        """Spawn subprocess; kill it on timeout/error to prevent zombies."""
        async with self._get_semaphore():
            return await self._run_inner(args, timeout)

    async def _run_inner(self, args: list, timeout: float) -> Optional[str]:
        from .metrics import METRICS
        METRICS.inc("sampling.calls")
        try:
            proc = await asyncio.create_subprocess_exec(
                *args, stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE,
                stdin=asyncio.subprocess.DEVNULL)
        except Exception:
            METRICS.inc("sampling.fail")
            return None
        try:
            with METRICS.timer("sampling.latency_ms"):
                stdout, _ = await asyncio.wait_for(proc.communicate(), timeout=timeout)
            result = stdout.decode().strip() or None
            if result:
                METRICS.inc("sampling.success")
                est_in = sum(len(a) for a in args if isinstance(a, str)) // 4
                est_out = len(result) // 4
                METRICS.cost("sampling", "haiku", est_in, est_out)
            else:
                METRICS.inc("sampling.fail")
            return result
        except asyncio.TimeoutError:
            METRICS.inc("sampling.timeout")
            proc.kill()
            try:
                await asyncio.wait_for(proc.wait(), 2.0)
            except (asyncio.TimeoutError, Exception):
                pass
            return None
        except BaseException as e:
            if proc.returncode is None:
                try:
                    proc.kill()
                    await asyncio.wait_for(proc.wait(), 2.0)
                except BaseException:
                    pass
            if isinstance(e, asyncio.CancelledError):
                raise
            METRICS.inc("sampling.fail")
            return None

    async def verify_visual(
        self,
        prompt: str,
        screenshot_path: Optional[str] = None,
        *,
        feature: str = "visual_verify",
    ) -> Optional[str]:
        if not self.enabled:
            return None
        if not _gate(feature, 0.7):
            return None
        system = "You verify Unity scene changes. Answer: PASS or FAIL + 1 sentence. Nothing else."
        full_prompt = f"{system}\n\n{prompt}"
        if screenshot_path and os.path.isfile(screenshot_path):
            full_prompt += f"\n\nRead this screenshot and verify: {screenshot_path}"
            args = [CLAUDE_CMD, "-p", full_prompt, "--model", "haiku",
                    "--max-turns", "2", "--tools", "Read"]
        else:
            args = [CLAUDE_CMD, "-p", full_prompt, "--model", "haiku", "--max-turns", "1"]
        result = await self._run(args, 15.0)
        if result:
            await _record_async(feature, has_image=bool(screenshot_path and os.path.isfile(screenshot_path)))
        return result

    async def generate(
        self,
        prompt: str,
        *,
        feature: str = "do_intent",
    ) -> Optional[str]:
        """Raw text generation via claude CLI. No system prompt added."""
        if not self.enabled:
            return None
        if not _gate(feature, 0.5):
            return None
        result = await self._run(
            [CLAUDE_CMD, "-p", prompt, "--model", "haiku", "--max-turns", "1"], 15.0)
        if result:
            await _record_async(feature)
        return result

    async def describe_image(
        self,
        prompt: str,
        image_path: str,
        *,
        feature: str = "screenshot_describe",
    ) -> Optional[str]:
        """Image -> text via Haiku. Pure description, no PASS/FAIL framing."""
        if not self.enabled or not os.path.isfile(image_path):
            return None
        if not _gate(feature, 0.9):
            return None
        full_prompt = f"Read this image and analyze it.\nImage: {image_path}\n\n{prompt}"
        args = [CLAUDE_CMD, "-p", full_prompt, "--model", "haiku",
                "--max-turns", "2", "--tools", "Read"]
        result = await self._run(args, 20.0)
        if result:
            await _record_async(feature)
        return result

    async def verify_visual_diff(
        self,
        before: str,
        after: str,
        prompt: str,
        *,
        feature: str = "visual_diff",
    ) -> Optional[str]:
        """Send TWO images + prompt to Haiku for semantic comparison."""
        if not self.enabled:
            return None
        if not (os.path.isfile(before) and os.path.isfile(after)):
            return None
        if not _gate(feature, 0.9):
            return None
        full_prompt = f"Read these two images and compare them.\nImage 1 (before): {before}\nImage 2 (after): {after}\n\n{prompt}"
        args = [CLAUDE_CMD, "-p", full_prompt, "--model", "haiku",
                "--max-turns", "2", "--tools", "Read"]
        result = await self._run(args, 25.0)
        if result:
            await _record_async(feature)
        return result

    async def summarize(
        self,
        data: str,
        instruction: str = "Summarize concisely",
        *,
        feature: str = "summarize",
    ) -> Optional[str]:
        if not self.enabled:
            return None
        if not _gate(feature, 0.2):
            return None
        full_prompt = f"{instruction}:\n{data[:3000]}"
        result = await self._run(
            [CLAUDE_CMD, "-p", full_prompt, "--model", "haiku", "--max-turns", "1"], 15.0)
        if result:
            await _record_async(feature)
        return result
