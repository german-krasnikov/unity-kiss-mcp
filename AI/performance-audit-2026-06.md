# Аудит производительности unity-kiss-mcp

> 10 агентов-архитекторов × 10 подсистем → 96 находок → 20 уникальных → adversarial verify (12 confirmed, 8 partial, 0 refuted). 2026-06-01.

## Bottom Line

**Главный рычаг скорости** — одна строка `EditorApplication.QueuePlayerLoopUpdate()` в `MCPServer.cs:303` (F02): убирает 10-250ms латентности на _каждую_ команду, давая 500-12500ms/сессию в зависимости от фокуса редактора. 10-минутный фикс, нулевой риск. В сумме quick wins (F02 + F04 + F08 + F13 + F18) дают **~600-2000ms + ~2000-3000 токенов/сессию** за <4 часа. Полная реализация всех подтверждённых фиксов (включая lock-split F01 и middleware decomp F14) даёт потолок **~3000-5000ms + ~5000-7000 токенов/сессию**, но требует ~2 недели и protocol audit на стороне Unity.

---

## 1. Critical Path — Speed

### F02 — QueuePlayerLoopUpdate (CONFIRMED, prio 95)
**`MCPServer.cs:291-303`** — после `_mainThreadQueue.Enqueue(...)` нет `QueuePlayerLoopUpdate()`. Очередь дренируется только через `EditorApplication.update` (стр. 73): 60-100Hz при фокусе (10-17ms), 4Hz без фокуса (до 250ms). 50 команд → 500-850ms (фокус) … до 12.5s (без фокуса).
**Fix:** добавить после стр. 303 `EditorApplication.QueuePlayerLoopUpdate();` | 10 мин | risk: none (API с 2019.4, serial TCP уже гарантирует порядок).

### F01 — Global asyncio.Lock сериализует всё (PARTIAL, prio 97)
**`bridge.py:189-195`** (send держит lock на весь round-trip), **`bridge.py:141-149`** (`_raw_ping` держит lock до 20s). Verify скорректировал: skip-if-locked guard (стр. 119) нейтрализует контеншн heartbeat→send; реальный риск — обратное направление (`_raw_ping` берёт lock первым на 20s). Watchdog делает 2 sequential `send()` (`watchdog.py:39-40`) — блокируют друг друга.
**Quick wins (safe, 2ч):** (1) `_raw_ping` timeout 20→5s `bridge.py:122`; (2) убрать lock вокруг counter++ `bridge.py:173` (asyncio single-threaded); (3) `asyncio.gather` в watchdog `_scan`; (4) heartbeat reconnect → close+mark-dead, пусть `send()` сам реконнектит.
**Full multiplexing (2-3 дня):** msg_id-keyed Future dict + background reader. **Требует аудита:** обрабатывает ли Unity C# запросы строго sequential (`_mainThreadQueue` + serial `ProcessMainThreadQueue` → скорее да). Если да — multiplexing бесполезен (один клиент = один запрос за раз).

### F03 — Speculation broken + PrefetchCache TTL=0.5s (PARTIAL, prio 93)
ДВЕ разные системы. `SpeculativeLayer` (opt-in `UNITY_MCP_SPECULATION=1`) — сломана: `speculation.py:58-63` данные append-ятся inline и выбрасываются, никогда не `.put()`. `PrefetchCache`/GATE_PRIORS (default-on) — **работает** (fire-and-forget `middleware.py:937`, populate `:650`), но `prefetch_cache.py:49` TTL=0.5s → протухает до следующего вызова LLM.
**Fix:** (1) TTL 0.5→12.0s — одна строка, immediate; (2) speculation: `self._prefetch_cache.put(pred.cmd, pred.args, data)`; (3) speculation → `asyncio.create_task`. TTL: 5 мин. Полный: 1-2ч. Полный speculation-выигрыш только при opt-in.

### F05 — Circuit breaker игнорирует state file (PARTIAL, prio 88)
**`middleware.py:55-63`** `allow_request()` чисто time-based, 15s cooldown. Даже если Unity написала `state=ready` через 3s — circuit блокирует. PrefetchCache lookup (`:881`) ниже circuit check (`:811`) → кешированные read-only тоже заблокированы. Открывается только после 3 fail подряд → impact 0-15s conditional, не гарантированный.
**Fix:** (1) пробросить `CompileStateProbe` в `CircuitBreaker`, в `allow_request()`: `state=ready` → OPEN→HALF_OPEN; (2) поднять PrefetchCache lookup выше circuit для read-only. 2-3ч.

### F11 — Batch set_property: snapshot per-op + Physics.SyncTransforms (CONFIRMED, prio 70)
**`CommandRouter.cs:545`** (snapshot на каждый sub-op → append в batch-ответ), **`ObjectManager.cs:84-87`** (Physics.Sync + Physics2D.Sync на каждый Transform write).
**Fix:** static `_batchMode` flag → ExecSetProperty возвращает компактное `"{prop} = {actual}"` без snapshot, ObjectManager скипает Sync; BatchHelper делает single SyncTransforms в конце. 1ч | ~3K-6K токенов/10-op batch + 5-30ms.

### F04 — mark_recompile_issued() dead code (PARTIAL, prio 90)
**`bridge.py:124-127`**. Verify: impact **незначительный** — `send()` уже определяет DomainReloadError через `isinstance` (`:203`), backoff работает. Эффект wiring: heartbeat reconnect wait 2→5s в 30s window + текст ошибки. 15 мин, cosmetic.

### F18 — MultiViewCapture reflection per call (CONFIRMED, prio 42)
**`MultiViewCapture.cs:237-261`** — assembly scan + `GetMethod` на каждый из 4 `RenderCamera()`. Кеша нет.
**Fix:** `static Type _cachedReqType; static MethodInfo _cachedSubmit; static bool _done;`. 15 мин | 2-8ms/multi_view.

### F09 / F17 / F20 (speed, lower prio)
- **F09** (PARTIAL): нет session-start prefetch. Реально экономит 1 local RTT (~10-50ms) для первого `get_hierarchy`. `get_console` не в `_read_cacheable` → кеш не сервится. Фикс через `_send` (не `bridge.send` — иначе bypass middleware). Минор.
- **F17** (PARTIAL): `resolve_path_live` `middleware.py:358` — TCP per unknown path, нет negative-cache (~7ms/miss recurring). SchemaGuard `_fetch_props` `:121` — cold-start only (cache persistent). Fix: negative-cache 10s TTL + background schema fetch. 1ч.
- **F20** (CONFIRMED, но opt-in): `sampling.py:76-84` спавнит CLI-процесс на каждый Haiku-вызов (~300ms). Только при `UNITY_MCP_VISUAL_VERIFY=1`. Fix: `anthropic.AsyncAnthropic` (новая зависимость). Half day.

---

## 2. Token Economy

| # | Finding | Tokens/session | Effort | Risk |
|---|---------|----------------|--------|------|
| F08 | `strip_defaults` unconditionally для reads (сейчас за `UNITY_MCP_DISTILL=1`) | ~1000-2000 | 30 мин | Low (`_no_strip` escape) |
| F12 | Confidence suffix → только при <0.5; AUTO STATE → gate staleness | ~1150 | 30 мин | Low |
| F07 | `fields=` projection на get_component/inspect | ~1440 | 3-4ч | Med (нужны имена полей; есть get_schema) |
| F16 | Error deduplication (key by `(cmd, prefix)`) | ~700-1000 | 2ч | Low |
| F06 | TIER1=38 tools → trim descriptions, invert unknown default | ~500-800 | half day | Med (TDD-тест блокирует move) |
| F13 | Float `"G"` → `"G4"` во всех serializers | ~300-600 | 15 мин | Low |
| F11 | Batch snapshot suppression (token-часть) | ~3K-6K/heavy batch | 1ч | Low |

**Conservative потенциал: ~5000-7000 токенов/сессию.**

Детали:
- **F08** `middleware.py:140`: `_distiller_enabled` за env flag (default off), `strip_defaults` достижим только через distiller pipeline. Fix: звать `strip_defaults` напрямую в `wrapped()` для `{get_component, inspect, get_object_detail}`. Нулевые поля (`m_LocalPosition.x=0`, `m_Mass=1`, `useGravity=true`) = 30-50% текста компонента.
- **F12** `middleware.py:207,246`: confidence suffix + блокирующий `get_hierarchy` каждый 10-й вызов. ⚠️ verify: для gating AUTO STATE нужен **новый** counter `_last_auto_state_call`, а не `_hierarchy_call_id` (тот про HierarchyDiff baseline).
- **F13**: подтверждено в `ComponentSerializer.cs:124,133,136,139,150` + те же `"G"` в `ShaderSerializer`, `AnimationSerializer`, `AnimatorControllerSerializer`, `RuntimeHelper`, `MaterialHelper`, `ScriptableObjectHelper` → реальная экономия выше, чем по одному файлу.
- **F06**: ⚠️ verify скорректировал: `bt_*` tools **не существуют** в репо — никакого «880-token bypass» нет. Move runtime tools из TIER1 блокирован TDD-тестом `test_gating.py:126` и требует Play Mode detection (TCP ping на каждый ListTools). Безопасно: только trim descriptions (`screenshot`=168 токенов→<80) + invert unknown-tool default.

---

## 3. Modularity & Maintainability

- **F14** (CONFIRMED): `middleware.py` = 1019 строк, класс `Middleware` 99-792 (694 стр, 37 методов, 21 shared-state var), `wrap_send` 793-1019 (227 стр). Shared mutable coupling: `_clean_paths` читают `check_taint`/`record_read`/`_get_disambig`. ⚠️ verify: фичи **уже** юнит-тестируемы по отдельности (тесты зовут `mw.check_taint(...)` на голом инстансе) — testability-аргумент завышен; реальная боль — 220-строчный waterfall в `wrapped`.
  **Decomp:** `middleware/{circuit, path_resolver, write_guard, cache_layer, confidence, pipeline}.py`. Старт с PathResolver (2-3ч). Полная: 1-2 дня. 1480 тестов ловят регрессии.
- **F15** (CONFIRMED): `CommandRouter.cs` = 1054 строки, 36 handler-методов. 12 consolidated handlers (829-1030, ~202 стр) — чистый boilerplate, дублирует уже работающий `RegisterAction` паттерн. Mechanical move в Helper-классы → −280 строк. 3-4ч, risk none. ⚠️ целевые хелперы уже >200 строк (AnimationHelper=344, AnimatorControllerHelper=391).
- **F19** (CONFIRMED): `advanced.py` 351 стр / 22 тула; `PlaytestRunner.cs` 559 стр / `ExecuteStep` 300-строчный switch с 21 case + 8 ref-параметрами. Split: `tools/{batch,skills,templates,spatial}.py` + Dictionary dispatch + `PlaytestContext` struct. ⚠️ 5 тест-файлов импортят из `advanced` по символу → обновить import paths. Py 2ч / C# 3ч.

---

## 4. Prioritized Roadmap

### Wave 0 — Quick Wins (≈4ч, всё safe)
| ID | Fix | Dim | Impact | Effort |
|----|-----|-----|--------|--------|
| F02 | `QueuePlayerLoopUpdate()` после enqueue | Speed | 500-12500ms/sess | 10м |
| F13 | `"G"`→`"G4"` во всех serializers | Tokens | ~300-600 tok | 15м |
| F18 | Cache reflection в MultiViewCapture | Speed | 2-8ms/call | 15м |
| F04 | Wire `mark_recompile_issued()` | Speed | cosmetic | 15м |
| F08 | `strip_defaults` unconditionally | Tokens | ~1000-2000 tok | 30м |
| F12 | Confidence gate <0.5 + AUTO STATE staleness | Tokens | ~1150 tok | 30м |
| F03-ttl | PrefetchCache TTL 0.5→12s | Speed | 10-150ms | 5м |

### Wave 1 — Foundation (1-2 дня)
F01-qw (ping timeout, gather, heartbeat simplify, counter lock) · F11 (batch `_batchMode`) · F05 (circuit consult state + cache above circuit) · F16 (error dedup) · F17 (negative path-cache + bg schema fetch).

### Wave 2 — Architecture (DONE, 2026-06-02)
✓ F06 (trim descriptions) · ✓ F07 (`fields=` projection) · ✓ F14 (middleware decomp PathResolver) · ✓ F15 (CommandRouter→partial classes) · ✓ F19 (split advanced.py + PlaytestRunner). All 6 commits merged, 1565 Python tests pass, 754 C# EditMode pass.

### Wave 3 — Deep (1-2 недели, requires audit)
F01-full (multiplexing — **сначала** аудит Unity FIFO) · F03-full (speculation populate cache) · F20 (Anthropic SDK).

---

## 5. НЕ делать (verify отсёк / micro-opt)
- **F01 full multiplexing без аудита Unity** — если C# отвечает строго sequential (`_mainThreadQueue` → почти наверняка), выигрыша ноль.
- **F06 move runtime tools → dynamic** — блокировано TDD-тестом + нет Play Mode autodetect. `bt_*` bypass не существует.
- **F09 session-start prefetch console/editor-state** — не в `_read_cacheable`, кеш не сервится; экономит максимум 1 RTT для hierarchy.
- **F10 wrap_send closure caching** — sub-microsecond, чистый code-quality, не скорость.
- **F20** — только при `UNITY_MCP_VISUAL_VERIFY=1` (default off), добавляет hard dependency.
- **Из исходного research (skip):** FlatBuffers, LZ4, perfect-hash routing, compiled-regex cache, scene-partitioned locks, Burst parallel serialize — все <10ms/сессию или невозможны (Unity Editor API single-threaded).
