"""Telemetry/metrics registry. In-memory counters + timers + cost tracking.

Thread-unsafe by design — relies on CPython GIL for tiny dict[str]+=1 ops.
Optional jsonl event log when UNITY_MCP_METRICS=1 + UNITY_MCP_LOG_DIR set.
"""
import atexit
import inspect
import json
import os
import statistics
import time
from collections import defaultdict, deque
from contextlib import contextmanager
from typing import Iterator, Optional

# Haiku pricing (per 1M tokens)
HAIKU_IN_PER_MTOK = 0.80
HAIKU_OUT_PER_MTOK = 4.00


class MetricsRegistry:
    """Process-wide metrics. inc() / observe() / timer() / cost() / event()."""

    def __init__(self):
        self._counters: defaultdict[str, int] = defaultdict(int)
        self._observations: defaultdict[str, deque] = defaultdict(lambda: deque(maxlen=256))
        self._costs_usd: defaultdict[str, float] = defaultdict(float)
        self._jsonl_f = None
        self._start_ts = time.time()
        self._setup_sink()

    def _setup_sink(self) -> None:
        if os.environ.get("UNITY_MCP_METRICS") != "1":
            return
        log_dir = os.environ.get("UNITY_MCP_LOG_DIR")
        if not log_dir:
            return
        try:
            os.makedirs(log_dir, exist_ok=True)
            path = os.path.join(log_dir, "metrics.jsonl")
            self._jsonl_f = open(path, "a")
            atexit.register(self._close)
        except Exception:
            self._jsonl_f = None

    def _close(self) -> None:
        if self._jsonl_f:
            try:
                self._jsonl_f.close()
            except Exception:
                pass
            self._jsonl_f = None

    def inc(self, key: str, n: int = 1) -> None:
        # Hard cap on unique keys to prevent unbounded growth on hostile/dynamic cmds
        if key not in self._counters and len(self._counters) >= 512:
            return
        self._counters[key] += n

    def observe(self, key: str, value: float) -> None:
        if key not in self._observations and len(self._observations) >= 512:
            return
        self._observations[key].append(value)

    @contextmanager
    def timer(self, key: str) -> Iterator[None]:
        t0 = time.perf_counter()
        try:
            yield
        finally:
            self.observe(key, (time.perf_counter() - t0) * 1000.0)

    def cost(self, feature: str, model: str, in_tok: int, out_tok: int) -> None:
        usd = in_tok * HAIKU_IN_PER_MTOK / 1e6 + out_tok * HAIKU_OUT_PER_MTOK / 1e6
        self._costs_usd[feature] += usd
        self._costs_usd["__total__"] += usd

    def event(self, kind: str, **fields) -> None:
        if not self._jsonl_f:
            return
        try:
            line = json.dumps({"t": time.time(), "kind": kind, **fields})
            self._jsonl_f.write(line + "\n")
            self._jsonl_f.flush()
        except Exception:
            pass

    def snapshot(self) -> dict:
        return {
            "uptime_s": time.time() - self._start_ts,
            "counters": dict(self._counters),
            "observations": {
                k: {
                    "n": len(v),
                    "p50": statistics.median(v) if v else 0,
                    "p95": _p95(v) if len(v) >= 20 else (max(v) if v else 0),
                    "avg": statistics.mean(v) if v else 0,
                }
                for k, v in self._observations.items()
            },
            "costs_usd": dict(self._costs_usd),
        }

    def reset(self) -> None:
        self._counters.clear()
        self._observations.clear()
        self._costs_usd.clear()
        self._start_ts = time.time()

    def snapshot_and_reset(self) -> dict:
        """Atomic snapshot + reset to avoid losing concurrent writes."""
        snap = self.snapshot()
        self.reset()
        return snap

    def format_report(self) -> str:
        s = self.snapshot()
        c = s["counters"]
        lines = [f"=== Unity MCP Metrics (uptime {s['uptime_s']:.0f}s) ==="]

        # Caches
        cache_keys = [k for k in c if k.startswith("fpcache") or k.startswith("diffcache")]
        if cache_keys:
            lines.append("\n[Caches]")
            for prefix in ("fpcache", "diffcache"):
                hit = c.get(f"{prefix}.hit", 0)
                miss = c.get(f"{prefix}.miss", 0)
                if hit + miss == 0:
                    continue
                rate = 100 * hit / (hit + miss)
                lines.append(f"  {prefix}: hit={hit} miss={miss} rate={rate:.0f}%")

        # Speculation
        if any(k.startswith("speculation") for k in c):
            hit = c.get("speculation.hit", 0)
            miss = c.get("speculation.miss", 0)
            total = hit + miss
            rate = (100 * hit / total) if total else 0
            lines.append(
                f"\n[Speculation] predict={c.get('speculation.predict', 0)} "
                f"hit={hit} miss={miss} hit_rate={rate:.0f}%"
            )

        # Sampling
        if any(k.startswith("sampling") for k in c):
            obs = s["observations"]
            lat = obs.get("sampling.latency_ms", {})
            lines.append(
                f"\n[Sampling/Haiku] calls={c.get('sampling.calls', 0)} "
                f"success={c.get('sampling.success', 0)} "
                f"timeout={c.get('sampling.timeout', 0)} fail={c.get('sampling.fail', 0)}"
            )
            if lat:
                lines.append(f"  latency p50={lat.get('p50', 0):.0f}ms p95={lat.get('p95', 0):.0f}ms")
            total_cost = s["costs_usd"].get("__total__", 0)
            if total_cost > 0:
                lines.append(f"  cost_usd ~= ${total_cost:.4f}")

        # Lessons
        if any(k.startswith("lessons") for k in c):
            lines.append(
                f"\n[Lessons] recorded={c.get('lessons.recorded', 0)} "
                f"hint_emitted={c.get('lessons.hint_emitted', 0)}"
            )

        # Hinter
        if any(k.startswith("hint.") or k == "hinter.error" for k in c):
            emitted = sum(v for k, v in c.items() if k.startswith("hint.emitted."))
            adopted = sum(v for k, v in c.items() if k.startswith("hint.adopted."))
            ignored = sum(v for k, v in c.items() if k.startswith("hint.ignored."))
            suppressed = sum(v for k, v in c.items() if k.startswith("hint.suppressed."))
            lines.append(
                f"\n[Hinter] emitted={emitted} adopted={adopted} ignored={ignored} "
                f"suppressed={suppressed} error={c.get('hinter.error', 0)}"
            )

        # Top commands by latency
        cmd_timers = {
            k.replace("cmd.", "").replace(".ms", ""): v
            for k, v in s["observations"].items()
            if k.startswith("cmd.")
        }
        if cmd_timers:
            lines.append("\n[Top commands by latency]")
            sorted_cmds = sorted(cmd_timers.items(), key=lambda kv: kv[1].get("p95", 0), reverse=True)[:5]
            for name, stats in sorted_cmds:
                lines.append(f"  {name}: n={stats['n']} p50={stats['p50']:.0f}ms p95={stats['p95']:.0f}ms")

        return "\n".join(lines)


def _p95(seq) -> float:
    s = sorted(seq)
    idx = int(0.95 * len(s))
    return s[min(idx, len(s) - 1)]


# Module singleton
METRICS = MetricsRegistry()


def timed(key: str):
    """Decorator: records ms latency for sync or async function."""
    def decorator(fn):
        if inspect.iscoroutinefunction(fn):
            async def awrapper(*args, **kwargs):
                with METRICS.timer(key):
                    return await fn(*args, **kwargs)
            return awrapper
        else:
            def wrapper(*args, **kwargs):
                with METRICS.timer(key):
                    return fn(*args, **kwargs)
            return wrapper
    return decorator


def counted(key: str):
    """Decorator: increments counter on each call."""
    def decorator(fn):
        if inspect.iscoroutinefunction(fn):
            async def awrapper(*args, **kwargs):
                METRICS.inc(key)
                return await fn(*args, **kwargs)
            return awrapper
        else:
            def wrapper(*args, **kwargs):
                METRICS.inc(key)
                return fn(*args, **kwargs)
            return wrapper
    return decorator
