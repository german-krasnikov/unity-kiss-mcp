"""Anti-hallucination + speed middleware for Unity MCP.

Enable with env var: UNITY_MCP_MIDDLEWARE=1
Each feature is independent and stateless per Middleware instance.
"""
import asyncio
import atexit
import json
import os
import time
from collections import deque, OrderedDict
from typing import Optional, Callable, Awaitable

from .prefetch_cache import PrefetchCache, GATE_PRIORS
from .compressor import strip_defaults
from .middleware_paths import PathResolverMixin, _levenshtein
from .utils import parse_kv_line

# Commands whose responses benefit from default-value stripping (F08)
_STRIP_CMDS: frozenset = frozenset({"get_component", "inspect", "get_object_detail"})



class CircuitBreaker:
    CLOSED, OPEN, HALF_OPEN = 0, 1, 2

    def __init__(self, threshold: int = 3, cooldown: float = 15.0, is_ready_fn=None):
        self.state = self.CLOSED
        self.failures = 0
        self.threshold = threshold
        self.cooldown = cooldown
        self.opened_at = 0.0
        self._probe_in_flight: bool = False
        self._is_ready_fn = is_ready_fn

    def record_success(self) -> None:
        self.failures = 0
        self.state = self.CLOSED
        self._probe_in_flight = False

    def release_probe(self) -> None:
        self._probe_in_flight = False

    def record_failure(self) -> None:
        self.failures += 1
        if self.failures >= self.threshold:
            self.state = self.OPEN
            self.opened_at = time.monotonic()

    def allow_request(self) -> bool:
        if self.state == self.CLOSED:
            return True
        if self.state == self.OPEN:
            # Check external readiness signal (e.g. compile state) before time-based cooldown
            if self._is_ready_fn is not None:
                try:
                    if self._is_ready_fn():
                        self.state = self.HALF_OPEN
                        self._probe_in_flight = True
                        return True
                except Exception:
                    pass
            if time.monotonic() - self.opened_at > self.cooldown:
                self.state = self.HALF_OPEN
                self._probe_in_flight = True
                return True
            return False
        # HALF_OPEN: allow only the first probe request
        if self._probe_in_flight:
            return False
        self._probe_in_flight = True
        return True

    def get_status(self) -> str:
        return ["CLOSED", "OPEN", "HALF_OPEN"][self.state]

    def remaining(self) -> float:
        return max(0.0, self.cooldown - (time.monotonic() - self.opened_at))

BLAST_RADIUS = {
    "get_hierarchy": 0, "get_component": 0, "inspect": 0, "screenshot": 0,
    "query_state": 0, "get_object_detail": 0, "find_objects": 0,
    "set_property": 1, "set_active": 1, "set_material": 1, "set_runtime_property": 1,
    "create_object": 2, "manage_component": 2, "wire_event": 2,
    "delete_object": 3, "scene": 3, "batch": 3,
}

WRITE_CMDS = {
    "set_property", "set_property_delta", "create_object", "delete_object", "manage_component",
    "wire_event", "set_active", "set_material", "set_runtime_property", "set_rect", "move_to",
    "batch", "animation", "timeline", "animator", "particle", "shader",
    "material", "prefab", "scriptable_object", "asset", "scene",
    "create_ui", "execute_code", "menu", "project_settings", "set_parent", "unwire_event",
}

READ_CMDS = {
    "get_hierarchy", "get_component", "inspect", "get_object_detail",
    "get_components_list", "find_objects", "search_scene", "compress_hierarchy",
    "query_state", "get_spatial_context", "scan_scene",
    "get_console", "get_compile_errors", "validate_references", "screenshot",
}

# Reads safe to serve from PrefetchCache (both above-circuit and pre-TCP paths).
_READ_CACHEABLE = frozenset({
    "get_component", "get_hierarchy", "get_components_list", "inspect", "get_compile_errors",
})


class Middleware(PathResolverMixin):
    """Anti-hallucination + speed + logging features."""

    def __init__(self):
        self._retry_cache: OrderedDict = OrderedDict()  # h -> (timestamp, None)
        self._RETRY_TTL = float(os.environ.get("UNITY_MCP_RETRY_TTL", "5.0"))
        self._RETRY_MAX = 32
        self.confidence: float = 1.0
        self.sampling: Optional["SamplingService"] = None  # type: ignore[name-defined]
        self._mutation_log = None
        log_dir = os.environ.get("UNITY_MCP_LOG_DIR")
        if log_dir:
            os.makedirs(log_dir, exist_ok=True)
            self._mutation_log = open(os.path.join(log_dir, "mutations.jsonl"), "a")
            atexit.register(lambda: self._mutation_log.close() if self._mutation_log else None)
        self._clean_paths: OrderedDict = OrderedDict()
        self._MAX_PATHS = 256
        self.call_count: int = 0
        self._last_hierarchy_call: int = 0  # call_count index when hierarchy was last seen (organic or auto)
        self.known_paths: set = set()
        self.is_playing: bool = False
        self._last_writes: OrderedDict = OrderedDict()
        self._MAX_WRITES = 128
        self._circuit_ready_fn = None
        self.circuit: CircuitBreaker = CircuitBreaker(is_ready_fn=lambda: self._circuit_ready_fn and self._circuit_ready_fn())
        self._error_dedup: OrderedDict = OrderedDict()
        self._negative_path_cache: dict = {}
        self._NEGATIVE_PATH_TTL: float = 10.0
        self._response_hashes: deque = deque(maxlen=5)
        self._mutation_count: int = 0
        self._last_success: float = time.time()
        self._consecutive_writes: int = 0
        self.scene_brief: Optional["SceneBrief"] = None  # type: ignore[name-defined]
        self._component_cache: OrderedDict = OrderedDict()  # path -> {component_names}
        self._MAX_COMPONENTS = 256
        # Tier C features
        self.speculation = None
        self.lessons = None
        self.recorder = None
        self.watchdog = None
        self.session = None
        self.inferrer = None
        self.hinter = None
        # Distiller (Cycle 5b / 5d)
        self._recent_focus: deque = deque(maxlen=8)
        self._distiller_enabled: bool = os.environ.get("UNITY_MCP_DISTILL", "0") == "1"
        self._distiller = None  # lazy init
        self._distill_cache: OrderedDict = OrderedDict()  # cache_key -> haiku-distilled text
        self._MAX_DISTILL_CACHE = 64
        self._haiku_in_flight: set = set()  # cache_keys currently being computed
        # Disambiguator (Cycle 5d Item 1)
        self._disambig_enabled: bool = os.environ.get("UNITY_MCP_DISAMBIG", "1") != "0"
        self._disambig = None  # lazy
        # PrefetchCache (Item 1)
        self._prefetch_cache: Optional[PrefetchCache] = (
            PrefetchCache() if os.environ.get("UNITY_MCP_PREFETCH_CACHE", "1") != "0" else None
        )
        # HierarchyDiff (Item 2)
        self._last_hierarchy_full: Optional[str] = None
        self._hierarchy_call_id: int = 0
        # SchemaGuard
        self.schema_cache = None
        self.schema_guard = None
        if os.environ.get("UNITY_MCP_VALIDATE", "1") != "0":
            from .schema_cache import SchemaCache
            from .schema_guard import SchemaGuard
            self.schema_cache = SchemaCache()
            self.schema_guard = SchemaGuard(self, self.schema_cache)

    # ── Feature 1: Retry Watchdog ─────────────────────────────────────────

    def check_retry(self, cmd: str, args: dict) -> Optional[str]:
        if cmd in READ_CMDS:
            return None
        h = hash((cmd, json.dumps(args, sort_keys=True)))
        now = time.monotonic()
        entry = self._retry_cache.get(h)
        if entry is not None and now - entry[0] < self._RETRY_TTL:
            return (f"⚠ RETRY (within {self._RETRY_TTL:.1f}s): identical {cmd}. "
                    "Re-read state before retrying.")
        self._retry_cache[h] = (now, None)
        self._retry_cache.move_to_end(h)
        while len(self._retry_cache) > self._RETRY_MAX:
            self._retry_cache.popitem(last=False)
        return None

    def reset_session(self) -> None:
        """Drop volatile in-flight state on reconnect."""
        self._retry_cache.clear()
        self._error_dedup.clear()
        self._negative_path_cache.clear()
        self._response_hashes.clear()
        self._last_writes.clear()
        self.is_playing = False
        self.circuit = CircuitBreaker(is_ready_fn=lambda: self._circuit_ready_fn and self._circuit_ready_fn())
        if self.schema_cache is not None:
            self.schema_cache.invalidate_all()
        self._component_cache.clear()
        self.known_paths.clear()
        if self._prefetch_cache is not None:
            self._prefetch_cache.clear()
        self._last_hierarchy_full = None
        self._hierarchy_call_id = 0
        self._last_hierarchy_call = 0

    # ── Feature 2: Confidence Decay ───────────────────────────────────────

    def update_confidence(self, cmd: str, result: str) -> str:
        if cmd in WRITE_CMDS:
            self.confidence = max(0.0, self.confidence - 0.08)
        elif cmd in READ_CMDS:
            self.confidence = min(1.0, self.confidence + 0.15)
        if self.confidence >= 0.5:
            return result
        suffix = f"\n[confidence: {self.confidence:.2f}]"
        if self.confidence < 0.3:
            suffix += " [LOW CONFIDENCE: re-read state before writing]"
        return result + suffix

    # ── Feature 3: Taint Tracking ─────────────────────────────────────────

    def record_read(self, cmd: str, args: dict, result: str) -> None:
        if cmd in ("get_component", "inspect", "get_object_detail"):
            path = args.get("path", "")
            if path:
                if path in self._clean_paths:
                    self._clean_paths.move_to_end(path)
                else:
                    self._clean_paths[path] = None
                    if len(self._clean_paths) > self._MAX_PATHS:
                        self._clean_paths.popitem(last=False)

    def check_taint(self, cmd: str, args: dict) -> Optional[str]:
        if cmd != "set_property":
            return None
        prop = args.get("prop", "")
        if not prop.endswith("Reference"):
            return None
        value = args.get("value", "")
        if not value or value == "null" or value.startswith("#"):
            return None
        if value not in self._clean_paths:
            return f"⚠ TAINT WARNING: '{value}' was never read. Consider get_hierarchy first."
        return None

    # ── Feature 4: Periodic State Injection ───────────────────────────────

    async def maybe_inject_state(
        self,
        send_fn: Callable[..., Awaitable[str]],
        result: str,
    ) -> str:
        self.call_count += 1
        if self.call_count % 10 == 0 and (self.call_count - self._last_hierarchy_call) > 5:
            try:
                hierarchy = await send_fn("get_hierarchy", {"summary": "true"})
                self._last_hierarchy_call = self.call_count
                return result + f"\n--- AUTO STATE (call #{self.call_count}) ---\n{hierarchy}"
            except Exception:
                pass
        return result

    # ── Item 3: Component Cache ───────────────────────────────────────────────

    def _lru_add_component(self, path: str, component: str) -> None:
        if path in self._component_cache:
            self._component_cache.move_to_end(path)
        else:
            self._component_cache[path] = set()
            if len(self._component_cache) > self._MAX_COMPONENTS:
                self._component_cache.popitem(last=False)
        self._component_cache[path].add(component)

    def cache_components(self, cmd: str, args: dict, result: str) -> None:
        """Update component cache from read results and mutations."""
        if cmd == "manage_component" and "path" in args and "type" in args:
            path = args["path"]
            comp = args["type"]
            action = args.get("action", "")
            if action == "add" and not result.startswith("err"):
                self._lru_add_component(path, comp)
            elif action in ("remove", "delete") and path in self._component_cache:
                self._component_cache[path].discard(comp)
            return
        if cmd == "delete_object" and "path" in args:
            self._component_cache.pop(args["path"], None)
            return
        if cmd == "get_component" and "path" in args and "type" in args:
            self._lru_add_component(args["path"], args["type"])
        elif cmd == "inspect" and "[" in result:
            current_path: Optional[str] = None
            for line in result.split("\n"):
                stripped = line.strip()
                if stripped.startswith("---") and stripped.endswith("---"):
                    inner = stripped.strip("-").strip()
                    current_path = inner.split()[0] if inner else None
                    if current_path and current_path not in self._component_cache:
                        self._component_cache[current_path] = set()
                        if len(self._component_cache) > self._MAX_COMPONENTS:
                            self._component_cache.popitem(last=False)
                elif stripped.startswith("[") and stripped.endswith("]") and current_path:
                    if current_path in self._component_cache:
                        self._component_cache[current_path].add(stripped[1:-1])

    def check_component_exists(self, path: str, component: str) -> Optional[str]:
        """Return warning if component definitely not on object (from cache). None = ok/unknown."""
        if path not in self._component_cache:
            return None
        known = self._component_cache[path]
        if not known:
            return None
        if component in known:
            return None
        lower_known = {c.lower(): c for c in known}
        if component.lower() in lower_known:
            return None  # case diff — InputNormalizer will fix
        return f"Component '{component}' not found on '{path}'. Known: {', '.join(sorted(known))}"

    # ── Item 5: Console Error Categorization ─────────────────────────────────

    def categorize_console_errors(self, result: str) -> str:
        """Parse console errors in result, append categorized hints."""
        if "NullReferenceException" in result:
            return result + "\n[HINT: NullRef — likely broken reference. Use validate_references to check.]"
        if "MissingComponentException" in result:
            return result + "\n[HINT: Missing component — was it removed? Use get_components_list to check.]"
        if "FormatException" in result or "Input string was not in a correct format" in result:
            return result + "\n[HINT: Format error — check value format. Use get_schema for field types.]"
        return result

    # ── Feature 6: Dead Write Elimination ────────────────────────────────

    def check_dead_write(self, cmd: str, args: dict) -> Optional[str]:
        if cmd != "set_property":
            return None
        key = (args.get("path"), args.get("component"), args.get("prop"))
        prev = self._last_writes.get(key)
        if key in self._last_writes:
            self._last_writes.move_to_end(key)
            self._last_writes[key] = args.get("value")
        else:
            self._last_writes[key] = args.get("value")
            if len(self._last_writes) > self._MAX_WRITES:
                self._last_writes.popitem(last=False)
        if prev is not None:
            return f"⚠ OVERWRITE: {key[2]} was set to '{prev}' without reading. New value: '{args.get('value')}'"
        return None

    def clear_write_on_read(self, cmd: str, args: dict) -> None:
        if cmd in READ_CMDS and args.get("path"):
            path = args["path"]
            for k in [k for k in self._last_writes if k[0] == path]:
                del self._last_writes[k]

    # ── Feature N: Starvation Monitor ────────────────────────────────────────

    def check_starvation(self, result: str) -> str:
        h = hash(result[:200])
        self._response_hashes.append(h)
        if len(self._response_hashes) == 5 and len(set(self._response_hashes)) == 1:
            result += "\n⚠ STARVATION: last 5 calls returned same result. Try different approach or re-read state."
        return result

    # ── Feature N: Blast Radius Tags ─────────────────────────────────────────

    def check_blast_radius(self, cmd: str) -> Optional[str]:
        radius = BLAST_RADIUS.get(cmd, 1)
        if radius >= 3:
            return f"⚠ HIGH BLAST RADIUS ({radius}): '{cmd}' affects multiple objects. Consider checkpoint first."
        return None

    # ── Feature N: Incremental Verification ──────────────────────────────────

    def check_verification_needed(self, cmd: str) -> Optional[str]:
        if cmd in WRITE_CMDS:
            self._mutation_count += 1
            if self._mutation_count % 5 == 0:
                return f"⚡ VERIFICATION CHECKPOINT ({self._mutation_count} mutations): verify state is consistent with goal before continuing."
        return None

    # ── Feature N: Alive Check ────────────────────────────────────────────────

    def dedup_error(self, cmd: str, result: str) -> str:
        """Collapse a repeated identical error to '(repeated Nx) ...' form.

        Pure mechanism — the caller gates this on genuine protocol errors. Keyed on
        the FULL message (no 80-char prefix collisions) and bounded to avoid growth.
        """
        if not result:
            return result
        key = (cmd, result)
        count = self._error_dedup.get(key, 0)
        self._error_dedup[key] = count + 1
        if len(self._error_dedup) > 256:
            self._error_dedup.popitem(last=False)
        if count == 0:
            return result
        return f"(repeated {count + 1}x) {result}"

    def check_alive(self) -> bool:
        return (time.time() - self._last_success) < 30.0

    # ── Feature 12: Workflow Phase FSM ───────────────────────────────────────

    def transition(self, cmd: str) -> Optional[str]:
        if cmd in READ_CMDS:
            self._consecutive_writes = 0
            return None
        if cmd in WRITE_CMDS:
            self._consecutive_writes += 1
            if self._consecutive_writes >= 3:
                return f"⚡ {self._consecutive_writes} consecutive writes without reading. Consider verifying state."
        return None

    # ── Feature: Visual Verification via MCP Sampling ────────────────────────────

    async def maybe_verify_visual(self, cmd: str, args: dict, result: str) -> str:
        if self.sampling is None or not self.sampling.enabled:
            return result
        if cmd not in WRITE_CMDS:
            return result
        if self.confidence >= 0.5:
            return result
        prompt = f"Verify that '{cmd}' succeeded. Args: {args}. Result: {result[:200]}"
        verdict = await self.sampling.verify_visual(prompt)
        if verdict:
            result = result + f"\n[VERIFY: {verdict}]"
        return result

    # ── Feature: Play Mode Auto-Routing ──────────────────────────────────────

    def track_editor_state(self, cmd: str, result: str) -> None:
        """Update is_playing from editor(action=state) response."""
        if cmd in ("recompile", "scene") and self.schema_cache is not None:
            self.schema_cache.invalidate_all()
        # TODO v2: also invalidate on asset cmd modifying .cs files (currently relies on subsequent recompile)
        if cmd != "editor":
            return
        lower = result.lower()
        if "state: playing" in lower or "state: paused" in lower:
            self.is_playing = True
        elif "state: stopped" in lower or "state: edit" in lower:
            self.is_playing = False

    def reroute_cmd(self, cmd: str, args: dict) -> tuple[str, dict]:
        """Rewrite set_property↔set_runtime_property based on play mode."""
        if cmd == "set_property" and self.is_playing and "prop" in args:
            new_args = {**args, "field": args["prop"]}
            del new_args["prop"]
            return "set_runtime_property", new_args
        return cmd, args

    # ── Feature: Batch Conflict Scan ─────────────────────────────────────────

    def scan_batch_conflicts(self, commands: str) -> Optional[str]:
        """Detect conflicts in batch command text. Returns warning or None."""
        if not commands:
            return None
        lines = [l.strip() for l in commands.splitlines() if l.strip()]
        warnings: list[str] = []
        write_keys: dict[tuple, int] = {}  # (path, component, prop) → line index
        deleted_paths: set[str] = set()
        created_names: set[str] = set()

        for i, line in enumerate(lines):
            cmd, kv = parse_kv_line(line)

            if cmd == "set_property":
                key = (kv.get("path"), kv.get("component"), kv.get("prop"))
                if key in write_keys:
                    warnings.append(f"⚠ BATCH: duplicate write to prop '{key[2]}' on {key[0]}")
                else:
                    write_keys[key] = i
                path = kv.get("path", "")
                if path in deleted_paths:
                    warnings.append(f"⚠ BATCH: referencing deleted object '{path}'")

            elif cmd == "create_object":
                created_names.add(kv.get("name", ""))

            elif cmd == "delete_object":
                path = kv.get("path", "")
                # Check create+delete no-op (name matches last segment of path)
                name = path.split("/")[-1] if path else ""
                if name in created_names:
                    warnings.append(f"⚠ BATCH: create+delete '{name}' is a no-op")
                deleted_paths.add(path)

        return "\n".join(warnings) if warnings else None

    # ── Feature: Post-mutation Snapshot Verification ─────────────────────────

    def verify_snapshot(self, result: str, prop: str, value: str) -> str:
        """Parse snapshot in set_property response and verify prop=value was written."""
        # Look for a component snapshot (a line starting with '[')
        has_snapshot = any(l.strip().startswith("[") and l.strip().endswith("]") for l in result.splitlines())
        if not has_snapshot:
            return result
        prop_lower = prop.lower()
        for line in result.splitlines():
            if ": " not in line:
                continue
            key, actual = line.split(": ", 1)
            if key.strip().lower() == prop_lower:
                actual = actual.strip()
                if actual == value:
                    return result + f"\n[VERIFIED: {prop}={value}]"
                else:
                    return result + f"\n[VERIFY FAIL: expected {value}, got {actual}]"
        return result

    def log_mutation(self, cmd: str, args: dict, result: str) -> None:
        if self._mutation_log and cmd in WRITE_CMDS:
            self._mutation_log.write(json.dumps({
                "t": round(time.time(), 2), "cmd": cmd,
                "args": {k: v for k, v in args.items() if v is not None},
                "result": result[:200],
            }) + "\n")
            self._mutation_log.flush()

    # ── Item 1: PrefetchCache ──────────────────────────────────────────────────

    async def _background_prefetch(self, cmd: str, args: dict, send_fn) -> None:
        """Fire a predicted read in background, populate cache on success."""
        try:
            result = await send_fn(cmd, args)
            text = result.get("data", "") if isinstance(result, dict) else str(result)
            if text and self._prefetch_cache is not None:
                self._prefetch_cache.put(cmd, args, text)
        except Exception:
            from .metrics import METRICS
            METRICS.inc("prefetch.error")

    # ── Cycle 5b: Distiller helpers ───────────────────────────────────────────

    def _track_focus(self, cmd: str, args: dict, result: str) -> None:
        """Update _recent_focus deque from args paths."""
        if cmd in {"find_objects", "get_component", "inspect", "get_object_detail",
                   "set_property", "set_active"}:
            path = args.get("path") or args.get("target")
            if path:
                if path in self._recent_focus:
                    self._recent_focus.remove(path)
                self._recent_focus.append(path)

    def _seed_preimage(self, cmd: str, args: dict, result: str) -> None:
        """After WRITE returns with reflect snapshot, seed PrefetchCache."""
        if self._prefetch_cache is None or cmd != "set_property":
            return
        if not args.get("path") or not args.get("component"):
            return
        try:
            from .reflect import _parse_snapshot
        except ImportError:
            return
        snap = _parse_snapshot(result)
        if not snap:
            return
        body = f"[{args['component']}]\n" + "\n".join(f"{k}: {v}" for k, v in snap.items())
        self._prefetch_cache.put_synthetic(
            "get_component",
            {"path": args["path"], "type": args["component"]},
            body,
            source="reflect-snapshot",
        )

    async def _maybe_distill(self, cmd: str, args: dict, result: str, no_distill: bool = False) -> str:
        """Apply heuristic distillation + Haiku background cache (Cycle 5d)."""
        if not self._distiller_enabled or no_distill:
            return result

        if self._distiller is None:
            from .distiller import ResponseDistiller
            sampling = None
            if os.environ.get("UNITY_MCP_DISTILL_HAIKU", "0") == "1":
                try:
                    from .sampling import SamplingService
                    svc = SamplingService()
                    if svc.enabled:
                        sampling = svc
                except Exception:
                    sampling = None
            self._distiller = ResponseDistiller(sampling=sampling)

        focus = tuple(self._recent_focus)

        # Check Haiku cache first (cheap key)
        if args.get("path"):
            path_key = args["path"]
        else:
            sig_args = {k: v for k, v in sorted(args.items()) if not k.startswith("_") and k != "path"}
            path_key = json.dumps(sig_args, sort_keys=True)
        cache_key = (cmd, path_key, focus)
        cached = self._distill_cache.get(cache_key)
        if cached is not None:
            self._distill_cache.move_to_end(cache_key)
            return f"{cached}\n[DISTILLED haiku-cached; full: re-call with _no_distill=true]"

        res = self._distiller.distill_heuristic(cmd, result, focus)

        # Schedule background Haiku for next call if heuristic was weak
        if (
            self._distiller._sampling is not None
            and res.method in ("passthrough", "skip")
            and len(result) > 1500
            and bool(focus)
            and cmd in self._distiller._haiku_cmds
            and cache_key not in self._haiku_in_flight
        ):
            self._haiku_in_flight.add(cache_key)
            asyncio.create_task(self._haiku_to_cache(cmd, result, focus, cache_key))

        if res.method in ("skip", "passthrough"):
            return result
        return (
            f"{res.text}\n"
            f"[DISTILLED {res.method} {res.original_size}→{res.distilled_size} chars; "
            f"full: re-call with _no_distill=true]"
        )

    async def _haiku_to_cache(self, cmd: str, text: str, focus: tuple, cache_key: tuple) -> None:
        """Background Haiku distillation. Fire-and-forget. Populates _distill_cache."""
        try:
            result = await self._distiller.distill_haiku(cmd, text, focus)
            if result is not None:
                self._distill_cache[cache_key] = result.text
                if len(self._distill_cache) > self._MAX_DISTILL_CACHE:
                    self._distill_cache.popitem(last=False)
        except Exception:
            from .metrics import METRICS
            METRICS.inc("distill.haiku_error")
        finally:
            self._haiku_in_flight.discard(cache_key)

    # ── Item 2: HierarchyDiff ──────────────────────────────────────────────────

    def _maybe_diff_hierarchy(self, full: str) -> str:
        """Return unified diff if economical (<50% of full), else full text."""
        if self._last_hierarchy_full is None:
            self._last_hierarchy_full = full
            self._hierarchy_call_id = 1
            return full

        import difflib
        prev_lines = self._last_hierarchy_full.splitlines()
        full_lines = full.splitlines()
        diff_lines = list(difflib.unified_diff(prev_lines, full_lines, n=0))

        # Skip header lines ('---' and '+++')
        body_lines = diff_lines[2:] if len(diff_lines) > 2 else []
        diff_body = "".join(body_lines)

        if not diff_body:
            return f"[DIFF since #{self._hierarchy_call_id}: NO_CHANGE]"

        # Use diff only if changed lines (+ and -) < 50% of full line count
        changed_lines = [l for l in body_lines if l.startswith(("+", "-"))]
        if len(changed_lines) > len(full_lines) * 0.5:
            self._last_hierarchy_full = full
            self._hierarchy_call_id += 1
            return full

        # Diff economical — update state and return diff
        prev_id = self._hierarchy_call_id
        self._last_hierarchy_full = full
        self._hierarchy_call_id += 1
        return f"[DIFF since #{prev_id}]\n{diff_body}"


# ── Wrap _send with full middleware pipeline ──────────────────────────────────

def wrap_send(send_fn, mw: Optional[Middleware] = None):
    """Return a wrapped _send that runs all middleware checks."""
    if mw is None:
        mw = Middleware()

    async def wrapped(cmd: str, args: dict, timeout: float = 30.0) -> str:
        # ToolHinter: adoption check at call start
        if mw.hinter is not None:
            mw.hinter.note_adoption(cmd)

        # Strip internal flags BEFORE sending to bridge — must not leak to Unity
        _no_reflect = bool(args.get("_no_reflect", False))
        _no_distill = bool(args.get("_no_distill", False))
        _explicit_path = bool(args.get("_explicit_path", False))
        _no_validate = bool(args.get("_no_validate", False))
        _no_strip = bool(args.get("_no_strip", False))
        args = {k: v for k, v in args.items() if k not in (
            "_no_reflect", "_no_distill", "_explicit_path", "_no_validate", "_no_strip"
        )}

        # F05: Cache-above-circuit — serve cacheable reads from PrefetchCache even when OPEN
        if mw._prefetch_cache is not None and cmd in _READ_CACHEABLE:
            _pre_cached = mw._prefetch_cache.get(cmd, args)
            if _pre_cached is not None:
                if _pre_cached.startswith("[CACHED:"):
                    return _pre_cached
                return f"[CACHED]\n{_pre_cached}"

        # Circuit breaker check
        if not mw.circuit.allow_request():
            secs = int(mw.circuit.remaining()) + 1
            return f"⚡ Circuit OPEN: Unity unavailable. Auto-retry in {secs}s"

        _probe_active = mw.circuit._probe_in_flight

        def _early_return(val):
            if _probe_active:
                mw.circuit.release_probe()
            return val

        # Play mode auto-routing
        cmd, args = mw.reroute_cmd(cmd, args)

        # Tier C: speculation hit tracking
        if mw.speculation is not None:
            mw.speculation.record_actual_next(cmd)

        # Tier C: lessons hint (prepend to result later)
        lessons_hint = mw.lessons.hint_for(cmd, args) if mw.lessons else None

        # Tier C: argument inference
        inferred_tags: list = []
        if mw.inferrer is not None and mw.session is not None:
            args, inferred_tags = mw.inferrer.infer(cmd, args, mw.session)

        # Tier C: watchdog pending alert
        watchdog_alert = mw.watchdog.consume_alert() if mw.watchdog else None

        # Pre-call checks
        retry_warn = mw.check_retry(cmd, args)
        if retry_warn:
            return _early_return(retry_warn)
        taint_warn = mw.check_taint(cmd, args)
        dead_warn = mw.check_dead_write(cmd, args)
        blast_warn = mw.check_blast_radius(cmd)
        verif_warn = mw.check_verification_needed(cmd)
        batch_warn = mw.scan_batch_conflicts(args.get("commands", "")) if cmd == "batch" else None

        # find_objects cache bypass
        if cmd == "find_objects" and not args.get("tag") and not args.get("layer") and not args.get("component"):
            cached = mw.find_from_cache(args.get("name"))
            if cached is not None:
                return _early_return(cached)

        # P1: Pre-flight path resolution via live search
        resolve_marker = ""
        if "path" in args and args["path"] and not _explicit_path:
            resolved, resolve_marker = await mw.resolve_path_live(args["path"], send_fn)
            if resolved.startswith("__DISAMBIG_BLOCK__"):
                return _early_return(resolved.split("\n", 1)[1])
            if resolved != args["path"]:
                args = {**args, "path": resolved}

        # SchemaGuard pre-flight validation
        if mw.schema_guard is not None:
            if not _no_validate:
                block = await mw.schema_guard.validate(cmd, args, send_fn)
                if block is not None:
                    from .metrics import METRICS
                    METRICS.inc("validate.blocked")
                    return _early_return(block)

        # P1: Component existence pre-check (blocks when cache confirms absence)
        if cmd == "set_property" and "component" in args:
            comp_warn = mw.check_component_exists(args.get("path", ""), args["component"])
            if comp_warn:
                return _early_return(comp_warn)

        # PrefetchCache: serve cached reads before TCP round-trip
        if mw._prefetch_cache is not None and cmd in _READ_CACHEABLE:
            cached = mw._prefetch_cache.get(cmd, args)
            if cached is not None:
                # Synthetic entries already carry [CACHED:<source>] tag — don't double-wrap
                if cached.startswith("[CACHED:"):
                    return _early_return(cached)
                return _early_return(f"[CACHED]\n{cached}")

        # Alive check: quick ping if last success was >30s ago
        if not mw.check_alive():
            try:
                await send_fn("ping", {}, timeout=3.0)
            except Exception:
                mw.circuit.record_failure()
                if _probe_active:
                    mw.circuit.release_probe()
                raise

        # Execute
        from .metrics import METRICS
        METRICS.inc(f"cmd.{cmd}.calls")
        try:
            with METRICS.timer(f"cmd.{cmd}.ms"):
                result = await send_fn(cmd, args, timeout=timeout)
        except Exception:
            METRICS.inc(f"cmd.{cmd}.fail")
            mw.circuit.record_failure()
            if _probe_active:
                mw.circuit.release_probe()
            raise
        mw.circuit.record_success()
        mw._last_success = time.time()

        # Extract string from dict response (when send_fn is raw bridge.send)
        protocol_err = False
        if isinstance(result, dict):
            if not result.get("ok"):
                protocol_err = True
                result = result.get("err", "Unknown error")
            elif "file" in result:
                result = f"Data saved to: {result['file']}"
            else:
                result = result.get("data", "")

        # F08: strip defaults unconditionally for component reads
        if cmd in _STRIP_CMDS and not _no_strip:
            result = strip_defaults(result)

        # F16: dedup only GENUINE protocol errors — never success payloads that merely
        # contain "Error" as data (e.g. get_console / an object named "ErrorHandler").
        if protocol_err:
            result = mw.dedup_error(cmd, result)

        # PrefetchCache: on write, invalidate path + fire background prefetch
        if cmd in WRITE_CMDS and mw._prefetch_cache is not None:
            path = args.get("path", "")
            if path:
                mw._prefetch_cache.invalidate_path(path)
            prior_fn = GATE_PRIORS.get(cmd)
            if prior_fn:
                predicted = prior_fn(args)
                if predicted:
                    p_cmd, p_args = predicted
                    asyncio.create_task(mw._background_prefetch(p_cmd, p_args, send_fn))

        # HierarchyDiff: reset on writes, apply diff on get_hierarchy reads
        if cmd in WRITE_CMDS:
            mw._last_hierarchy_full = None
            # F17: a create/rename may make a previously-absent path resolvable
            if mw._negative_path_cache:
                mw._negative_path_cache.clear()

        # Post-call updates
        mw.log_mutation(cmd, args, result)
        mw.cache_components(cmd, args, result)  # P1: populate component cache
        result = mw.categorize_console_errors(result)  # P1: append error hints
        mw.record_read(cmd, args, result)
        mw.clear_write_on_read(cmd, args)
        mw.update_path_cache(cmd, result)
        # Track focus for distiller
        mw._track_focus(cmd, args, result)
        # HierarchyDiff: compress repeated get_hierarchy calls
        if cmd == "get_hierarchy" and not _no_distill:
            result = mw._maybe_diff_hierarchy(result)
        # Seed preimage cache from reflect snapshots (after diff)
        mw._seed_preimage(cmd, args, result)
        mw.track_editor_state(cmd, result)
        if cmd == "set_property" and args.get("prop") and args.get("value") \
                and os.environ.get("UNITY_MCP_REFLECT", "1") == "0":
            result = mw.verify_snapshot(result, prop=args["prop"], value=args["value"])
        result = await mw.maybe_inject_state(send_fn, result)
        # F12: track organic hierarchy reads so the staleness gate is meaningful
        if cmd == "get_hierarchy":
            mw._last_hierarchy_call = mw.call_count
        # P2: Scene Brief — ensure() first, then inject if ready
        if mw.scene_brief is not None and not mw.scene_brief._injected:
            await mw.scene_brief.ensure(send_fn)
            if mw.scene_brief.should_inject(cmd):
                result = f"--- SCENE CONTEXT ---\n{mw.scene_brief.brief}\n---\n{result}"
                mw.scene_brief.mark_injected()
        result = mw.check_starvation(result)
        result = mw.update_confidence(cmd, result)
        result = await mw.maybe_verify_visual(cmd, args, result)

        # Tier C post-call
        if mw.session is not None:
            mw.session.record(cmd, args, result)
        if inferred_tags:
            result += f"\n[INFERRED: {', '.join(inferred_tags)}]"
        if mw.watchdog is not None:
            mw.watchdog.maybe_trigger(cmd)
        if mw.recorder is not None:
            # F16: classify on the protocol ok-flag, not a substring scan — a success
            # payload containing "Error" (e.g. get_console logs) must NOT count as a fail.
            mw.recorder.record(cmd, args, result, not protocol_err)
        if mw.speculation is not None:
            result = await mw.speculation.maybe_prefetch(cmd, args, result)

        # Asymmetric Reflection: compare args vs snapshot
        _reflect_on = os.environ.get("UNITY_MCP_REFLECT", "1") != "0"
        if result.startswith("[DEGRADED:"):
            _reflect_on = False
        if cmd in WRITE_CMDS and _reflect_on and not _no_reflect:
            from .reflect import reflect
            mismatch = await reflect(cmd, args, result, send_fn)
            if mismatch is not None:
                safe_msg = mismatch.msg.replace("]", ")")
                result += f"\n[REFLECT: {safe_msg}]"

        # ToolHinter: append hint (after all other markers, skip on DEGRADED)
        if mw.hinter is not None and not result.startswith("[DEGRADED:"):
            try:
                hint = mw.hinter.observe(cmd, args)
                if hint:
                    result += "\n" + hint
            except Exception:
                METRICS.inc("hinter.error")

        # Distill large reads (before prepend so warnings aren't distilled away)
        result = await mw._maybe_distill(cmd, args, result, no_distill=_no_distill)

        # Prepend resolve marker if path was auto-disambiguated
        if resolve_marker:
            result = resolve_marker + "\n" + result

        # Prepend warnings
        fsm_warn = mw.transition(cmd)
        warnings = [w for w in (taint_warn, dead_warn, blast_warn, verif_warn, fsm_warn, batch_warn) if w]
        prepend = [w for w in (watchdog_alert, lessons_hint) if w] + warnings
        if prepend:
            result = "\n".join(prepend) + "\n" + result
        return result

    return wrapped
