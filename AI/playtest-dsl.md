# Playtest DSL Reference (21 Steps + ALIAS Macro)

Run Play Mode scenarios with deterministic step-by-step assertions. Parser applies `ALIAS` substitutions, then executes steps sequentially.

## Step Types (Alphabetical)

### ALIAS

Define substitution macro for paths/values.

```
ALIAS player_start "(100,50,0)"
MOVE player TO ${player_start}  → resolves to: MOVE player TO (100,50,0)
```

**Syntax:** `ALIAS name value`

---

### ASSERT

Test single property value with comparison operator.

```
ASSERT Player/Health == 100
ASSERT Player/Score > 50
ASSERT Enemy/Status != active
```

**Syntax:** `ASSERT path/component/field op value`  
**Operators:** `==`, `!=`, `>`, `<`, `>=`, `<=`  
**Float equality:** `==` uses 0.001f tolerance — no `~=` operator exists  
**Timeout:** default 5s

---

### ASSERT_BATCH...END

Multiple assertions in one block, stops at first failure.

```
ASSERT_BATCH
  ASSERT Player/Health == 100
  ASSERT Enemy/Health > 0
  ASSERT Score != -1
END
```

**Syntax:**
```
ASSERT_BATCH
  ASSERT query op value
  ASSERT query op value
  ...
END
```

---

### ASSERT_NEAR

Check distance between two GameObjects ≤ threshold (meters).

```
ASSERT_NEAR Player Enemy 5.0
```

**Syntax:** `ASSERT_NEAR path1 path2 distance_threshold`  
**Output:** `(dist=X.XX)` actual distance in result

---

### ASSERT_CTA

Verify Call-To-Action state (VISIBLE or CLICKABLE).

```
ASSERT_CTA VISIBLE
ASSERT_CTA CLICKABLE
```

**Syntax:** `ASSERT_CTA VISIBLE | ASSERT_CTA CLICKABLE`  
**Default:** `VISIBLE` if mode omitted

---

### ASSERT_CONSERVED

Check that a sum of quantities remains constant over a time window (conservation law check).

```
ASSERT_CONSERVED SUM /Player|Health + /Enemy|Health == CONSTANT OVER 5
ASSERT_CONSERVED SUM ammo_current + ammo_used == CONSTANT OVER 2
```

**Syntax:** `ASSERT_CONSERVED SUM q1 + q2 [+ q3...] == CONSTANT OVER duration`  
**duration:** seconds to observe conservation  
**Use case:** verify totals are preserved (health sum, ammo conservation, resource balance)

---

### ASSERT_CAPTURED

Verify captured value matches condition.

```
CAPTURE initial_pos
MOVE Player TO (100,100,0)
ASSERT_CAPTURED initial_pos != (100,100,0)
```

**Syntax:** `ASSERT_CAPTURED capture_key op value`

---

### ASSERT_CONSOLE_CLEAN

Verify no error/exception logs since last call.

```
ASSERT_CONSOLE_CLEAN
ASSERT_CONSOLE_CLEAN ignore="NullReferenceException,deprecation_warning"
```

**Syntax:** `ASSERT_CONSOLE_CLEAN [ignore="pattern1,pattern2"]`  
**Filters out:** comma-separated substring patterns from Console errors

---

### CAPTURE

Snapshot current value for later comparison.

```
CAPTURE health_before
SET Player Health 50
ASSERT_CAPTURED health_before != 50
```

**Syntax:** `CAPTURE label query`

---

### INVARIANT

Assert condition continuously through next phase.

```
INVARIANT Player/Health > 0
MOVE Player TO (100,0,0)  # health must stay > 0 during move
```

**Syntax:** `INVARIANT query op value`

---

### INVOKE

Call public method on component via reflection.

```
INVOKE Player/Rigidbody AddForce 10,0,0
INVOKE Enemy/HealthComponent TakeDamage 25
```

**Syntax:** `INVOKE path/component method [arg1 arg2 ...]`  
**Returns:** "PASS" if method executed, "ERR" if component/method not found

---

### LOG

Print message to results.

```
LOG Starting combat test
INVOKE Enemy Attack
LOG Combat finished
```

**Syntax:** `LOG message_text`

---

### MONITOR

Watch expression value during next step, show graph in results.

```
MONITOR Player/Health
WAIT 3.0
```

**Syntax:** `MONITOR query [interval=0.05]`

---

### MOVE

Pathfind and walk character to world position.

```
MOVE Player TO 100,50,0
MOVE TO 0,0,0  # auto-detect Player path
```

**Syntax:** `MOVE [path] TO x,y,z`  
**Speed:** 15 m/s default  
**Timeout:** default 5s  
**Returns:** "PASS" when within 0.1m of target

---

### SNAPSHOT

Capture game view (optional visual verification).

```
SNAPSHOT width=1280 height=720 camera="MainCamera"
```

**Syntax:** `SNAPSHOT [width=640] [height=480] [camera="name"]`  
**Output:** `.png` path in result

---

### SIMULATE

Run a named simulator for a duration with optional parameters.

```
SIMULATE physics DURATION 1.0
SIMULATE ai_patrol DURATION 2.0 TIMESCALE 2.0 TARGET "Enemy"
SIMULATE wave_spawner DURATION 5.0 FREQUENCY 10
```

**Syntax:** `SIMULATE name [DURATION n] [TIMESCALE n] [TARGET "path"] [FREQUENCY n]`  
**name:** simulator identifier (required)  
**DURATION:** seconds (stored as Timeout)  
**TIMESCALE:** time scale multiplier (stored as Delay)  
**TARGET:** GameObject path (quoted)  
**FREQUENCY:** rate parameter (stored as Value)

---

### SET

Set runtime property on object (Play Mode only).

```
SET Player Rigidbody velocity "0,10,0"
SET Enemy Health currentHealth 50
```

**Syntax:** `SET path component field value`  
**path:** GameObject path (token 1)  
**component:** component type name (token 2)  
**field:** field/property name (token 3)  
**value:** new value (token 4)

---

### TELEPORT

Instantly move GameObject to position (no pathfind).

```
TELEPORT Player 100,50,0
TELEPORT Boss 0,0,0
```

**Syntax:** `TELEPORT path x,y,z`

---

### TIMESCALE

Change Time.timeScale for slow-mo / speedup.

```
TIMESCALE 0.5  # half speed
TIMESCALE 2.0  # double speed
TIMESCALE 1.0  # normal
```

**Syntax:** `TIMESCALE scale_factor`

---

### TRACE_FLOW

Trace data/event flow between two GameObjects over a field.

```
TRACE_FLOW FROM /Player TO /Enemy FIELD health
TRACE_FLOW FROM /Spawner TO /WaveManager FIELD waveCount TIMEOUT 10
```

**Syntax:** `TRACE_FLOW FROM /path1 TO /path2 FIELD fieldName [TIMEOUT n]`  
**FROM:** source GameObject path  
**TO:** destination GameObject path  
**FIELD:** field name to observe  
**TIMEOUT:** seconds (default 5)

---

### WAIT

Pause execution for N seconds.

```
WAIT 2.0
WAIT 0.1
```

**Syntax:** `WAIT duration_seconds`  
**Blocks:** all subsequent steps until delay expires

---

### WAIT_UNTIL

Poll condition with timeout, continue when true.

```
WAIT_UNTIL Player/Health == 100 timeout=10
WAIT_UNTIL Enemy/IsDead == true
```

**Syntax:** `WAIT_UNTIL query op value [timeout=5]`  
**Default timeout:** 5s  
**Poll interval:** 0.05s

---

## DSL Structure

```csharp
// First pass: collect ALIAS definitions
ALIAS player_start (100,0,0)
ALIAS enemy_patrol (50,0,50)

// Second pass: parse + execute with alias substitution
LOG Test: Enemy patrol intercept
CAPTURE initial_pos Enemy
MOVE Enemy TO ${enemy_patrol}       // substituted → (50,0,50)
WAIT_UNTIL distance(Player, Enemy) < 5

ASSERT_BATCH
  ASSERT Player/Health > 0
  ASSERT Enemy/IsDead == false
END

TELEPORT Player ${player_start}     // substituted → (100,0,0)
ASSERT_CONSOLE_CLEAN ignore="warning"
SNAPSHOT
LOG Test completed
```

## Parsing Rules

1. **Aliases first:** All `ALIAS name value` lines collected before execution
2. **Substitution:** `${name}` replaced in all tokens of subsequent lines
3. **Comments:** Lines starting with `#` ignored
4. **Whitespace:** Leading/trailing trimmed; tokens split by space
5. **Case-insensitive:** Commands (`MOVE`, `move`, `Move` equivalent)
6. **Queries:** Path syntax = `Parent/Child/Component.field` or `Component.field` (scene root)

## Verification Rules

- **PASS:** condition satisfied
- **FAIL:** condition false or timeout expired
- **ERR:** exception during evaluation (e.g., path not found, component missing)
- **Result format:** `[step_number] COMMAND ... — PASS/FAIL/ERR`

---

**See also:** `run_playtest`, `fuzz_playtest` in AI/tools-reference.md; `.claude/skills/playmode-verification.md` for assertion patterns.
