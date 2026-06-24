# Runtime & PlayTest Tools

Execute methods, modify values at runtime, run automated test scenarios. These tools are available in Play Mode and for real-time control.

## run_playtest

Execute a Play Mode test scenario using the Playtest DSL. Deterministic step-by-step assertions.

**Parameters:**
- `script` (string, required) — DSL script (21 step types supported)
- `timeout` (int, default=300) — Max seconds for entire test
- `stop_on_fail` (bool, default=true) — Stop at first failure
- `verbose` (bool, default=false) — Show detailed logs

**Output:** Test results with PASS/FAIL/ERR for each step, optional graphs for monitored values.

**DSL Quick Reference:**

| Step | Purpose | Example |
|------|---------|---------|
| **ALIAS** | Define substitution | `ALIAS player_start (100,50,0)` |
| **ASSERT** | Test single condition | `ASSERT Player/Health == 100` |
| **ASSERT_BATCH...END** | Multiple assertions | `ASSERT_BATCH\n  Player/Health == 100\n  Enemy/Health > 0\nEND` |
| **ASSERT_NEAR** | Check distance | `ASSERT_NEAR Player Enemy 5.0` |
| **ASSERT_CTA** | Verify UI button interactable | `ASSERT_CTA StartButton` |
| **ASSERT_CONSOLE_CLEAN** | No errors in console | `ASSERT_CONSOLE_CLEAN ignore="warning"` |
| **CAPTURE** | Snapshot value | `CAPTURE initial_pos = Player/Transform/position` |
| **ASSERT_CAPTURED** | Verify captured value | `ASSERT_CAPTURED initial_pos != (100,100,0)` |
| **SET** | Modify runtime property | `SET Player/Health 50` |
| **MOVE** | Pathfind and walk to position | `MOVE Player TO 100,50,0` |
| **TELEPORT** | Instantly move | `TELEPORT Player 0,0,0` |
| **WAIT** | Pause execution | `WAIT 2.0` |
| **WAIT_UNTIL** | Poll condition with timeout | `WAIT_UNTIL Player/Health == 100 timeout=10` |
| **SIMULATE** | Advance physics/time | `SIMULATE duration=1.0 physics=true` |
| **SCREENSHOT** | Capture view | `SCREENSHOT width=1280 height=720` |
| **LOG** | Print message | `LOG Test step completed` |
| **MONITOR** | Watch expression during step | `MONITOR Player/Health` |
| **COMMENT** | Documentation (no-op) | `# This is a comment` |
| **INVARIANT** | Assert continuously | `INVARIANT Player/Health > 0` |
| **INVOKE** | Call method | `INVOKE Enemy Attack` |
| **TRACE_FLOW** | Log method entry/exit | `TRACE_FLOW Player.OnTakeDamage` |
| **TIMESCALE** | Change time speed | `TIMESCALE 0.5` |

**Full DSL Reference:** [Playtest DSL Docs](../features/playtest.md)

**Example: Combat Test**

```python
script = """
# Setup
LOG Starting combat test
TELEPORT Player 0,0,0
TELEPORT Enemy 5,0,0

# Verify initial state
CAPTURE initial_enemy_health = Enemy/Health/hp
ASSERT Enemy/Health/hp == 100

# Deal damage
LOG Enemy takes 10 damage
INVOKE Enemy TakeDamage 10

# Verify damage applied
WAIT 0.5
ASSERT Enemy/Health/hp == 90
ASSERT_CAPTURED initial_enemy_health != 90

# Verify enemy still alive
ASSERT Enemy/Health/hp > 0

# Visual checkpoint
SCREENSHOT width=1280 height=720

# Console clean
ASSERT_CONSOLE_CLEAN ignore="warning"

LOG Test completed successfully
"""

result = await run_playtest(script=script, timeout=60)
print(result)
```

**Example: Patrol Route Test**

```python
script = """
ALIAS patrol_1 (10,0,0)
ALIAS patrol_2 (20,0,0)
ALIAS patrol_3 (10,0,0)

LOG Testing patrol route
CAPTURE patrol_count = Enemy/Patrol/position_count

MOVE Enemy TO ${patrol_1}
WAIT_UNTIL distance(Enemy, ${patrol_1}) < 0.5 timeout=10
ASSERT_NEAR Enemy ${patrol_1} 0.5

MOVE Enemy TO ${patrol_2}
WAIT_UNTIL distance(Enemy, ${patrol_2}) < 0.5 timeout=10

MOVE Enemy TO ${patrol_3}
ASSERT_NEAR Enemy ${patrol_3} 0.5

ASSERT_CONSOLE_CLEAN
LOG Patrol test passed
"""

result = await run_playtest(script=script)
```

---

## fuzz_playtest

Generate and run a random playtest DSL script. Finds hidden bugs via property-based testing.

**Parameters:**
- `steps` (int, default=10) — Number of random actions to generate
- `seed` (int, optional) — Random seed for reproducibility

**Output:** Playtest report with pass/fail per step.

**Example:**

```python
# Run 20 random steps
result = await fuzz_playtest(steps=20)

# Reproducible run
result = await fuzz_playtest(steps=10, seed=42)
```

---

## invoke_method

Call a public method on a component at runtime (Play Mode).

**Parameters:**
- `path` (string) — GameObject path
- `component` (string) — Component name (required; uses reflection)
- `method` (string) — Method name
- `args` (string, optional) — Comma-separated arguments

**Example:**

```python
# Call method with no arguments
await invoke_method(path="Enemy", component="EnemyAI", method="Attack")

# Call with arguments
await invoke_method(path="Player", component="Health", method="TakeDamage", args="10")

# Call with multiple arguments
await invoke_method(path="Weapon", component="WeaponController", method="Fire", args="10.0,5.0")

# Batch multiple invocations
await batch("""
invoke_method path=Enemy1 component=EnemyAI method=Attack
invoke_method path=Enemy2 component=EnemyAI method=Attack
""")
```

---

## set_runtime_property

Modify a component field in Play Mode via reflection (runtime-only).

**Parameters:**
- `path` (string) — GameObject path
- `component` (string) — Component name
- `field` (string) — Field name (public field or property)
- `value` (string) — New value (inferred type)

**Note:** Only works in Play Mode. Use `set_property` in Edit Mode.

**Example:**

```python
# Modify health at runtime
await set_runtime_property("Player", "Health", "hp", "50")

# Modify velocity
await set_runtime_property("Player", "Rigidbody", "velocity", "0,10,0")

# Batch modifications
await batch("""
set_runtime_property path=Player component=Health field=hp value=100
set_runtime_property path=Enemy component=Health field=hp value=50
""")
```

---

## wait_until

Poll a condition with timeout. Block until true or timeout. Play Mode only.

**Parameters:**
- `path` (string) — GameObject path (e.g., "Player")
- `component` (string) — Component type (e.g., "Health")
- `field` (string) — Field name (e.g., "hp")
- `value` (string) — Expected value
- `timeout` (float, default=5.0) — Max seconds to wait
- `negate` (bool, default=False) — If True, wait for value to NOT match

**Example:**

```python
# Wait for health to reach 100
await wait_until(path="Player", component="Health", field="hp", value="100", timeout=10)

# Wait for animation to finish
await wait_until(path="Player", component="Animator", field="IsPlaying", value="false", timeout=3)

# Wait for health to NOT be 100
await wait_until(path="Player", component="Health", field="hp", value="100", negate=True, timeout=5)
```

---

## move_to

Pathfind and walk character to world position (Play Mode).

**Parameters:**
- `path` (string) — GameObject to move
- `position` (string) — Position as "x,y,z"
- `timeout` (float, optional) — Max seconds (default: 15.0)

**Returns:** "arrived" when within 0.1m of target, "blocked" if timeout.

**Example:**

```python
# Walk to position
result = await move_to(path="Player", position="100,50,0")

# With custom timeout
result = await move_to(path="Player", position="0,0,0", timeout=20)
```

---

## query_state

Snapshot multiple game values in one call (Play Mode only).

**Parameters:**
- `queries` (string) — Comma-separated triplets: `path|component|field` (e.g., "/GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX")

**Example:**

```python
# Query multiple fields
result = await query_state(queries="/Player|Health|hp,/Enemy|Health|hp")

# Single query (comma-separated format)
result = await query_state(queries="/Player|Health|hp")
```

---

## test_step

Move character, snapshot state before/after, check console. Play Mode only.

**Parameters:**
- `path` (string) — GameObject to move
- `position` (string) — Target position as "x,y,z"
- `checks_before` (string, default="") — Comma-separated `path|component|field` triplets to snapshot before move
- `checks_after` (string, default="") — Comma-separated `path|component|field` triplets to snapshot after move
- `wait_after` (float, default=0.5) — Seconds to wait after arriving
- `timeout` (float, default=15.0) — Max seconds to wait for arrival

**Returns:** Structured BEFORE/MOVE/AFTER/CONSOLE report.

**Example:**

```python
# Move player and verify health unchanged
result = await test_step(
    path="Player",
    position="10,0,0",
    checks_before="Player|Health|hp",
    checks_after="Player|Health|hp"
)
```

---

## animator_intent

Natural language control for animator state machines (Category: Intent).

**Parameters:**
- `instruction` (string) — Natural language command (e.g., "make player jump")

**Example:**

```python
# Natural language animator control
await animator_intent("make player jump")
await animator_intent("transition to idle animation")
await animator_intent("set walk speed to 2.0")
```

---

## vfx_intent

Natural language VFX and particle control (Category: Intent).

**Parameters:**
- `instruction` (string) — Natural language command

**Example:**

```python
# Natural language VFX control
await vfx_intent("create explosion effect at player position")
await vfx_intent("spawn rain particles over scene")
await vfx_intent("fade out all particle systems")
```

---

## ui_intent

Natural language UI manipulation (Category: Intent).

**Parameters:**
- `instruction` (string) — Natural language command

**Example:**

```python
await ui_intent("show health bar above player")
await ui_intent("hide pause menu")
await ui_intent("flash screen red")
```

---

## Common Patterns

| Task | Tool | Example |
|------|------|---------|
| Verify game logic | run_playtest + ASSERT | `script = "ASSERT Player/Health == 100"; await run_playtest(script=script)` |
| Test combat flow | run_playtest + INVOKE + WAIT_UNTIL | `script = "INVOKE Enemy Attack\nWAIT_UNTIL Player/Health < 100"; await run_playtest(script=script)` |
| Test movement | run_playtest + MOVE + ASSERT_NEAR | `script = "MOVE Player TO 10,0,0\nASSERT_NEAR Player (10,0,0) 0.5"; await run_playtest(script=script)` |
| Stress test | fuzz_playtest | `await fuzz_playtest(steps=50)` |
| Method invocation | invoke_method | `await invoke_method("Enemy", "HealthComponent", "TakeDamage", args="10")` |
| Runtime modification | set_runtime_property + batch | `await batch("set_runtime_property path=Player component=Health field=hp value=50")` |

## PlayTest DSL Full Syntax

See [Playtest DSL Reference](../features/playtest.md) for complete documentation including:
- All 21 step types with parameters
- Parsing rules and edge cases
- Result format and error handling
- Common assertions for game logic
- Performance monitoring examples

## Workflow Example: Full Test Cycle

```python
# 1. Enter Play Mode
await editor("play")
await asyncio.sleep(1)  # Wait for initialization

# 2. Run test scenario
test_script = """
LOG Verifying game state
ASSERT Player/Health/hp == 100
ASSERT Enemy/Health/hp == 100

LOG Dealing damage
INVOKE Player Attack
WAIT 1.0

LOG Verifying damage
ASSERT Player/Health/hp < 100

LOG Test completed
ASSERT_CONSOLE_CLEAN
"""

result = await run_playtest(script=test_script, timeout=30)
print(result)

# 3. Exit Play Mode
await editor("stop")
```

---

**See also:** [Scene Tools](scene.md) for Play Mode control (editor play/stop), [Objects](objects.md) for component access, [Diagnostics](diagnostics.md) for compile gates.
