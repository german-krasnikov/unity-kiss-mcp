# Playtest Guide

Run deterministic gameplay scenarios with assertions and state snapshots.

## Overview

Playtesting executes a script of gameplay steps: move, wait, assert state, check console. Results are compressed for readability.

## Quick Start

```python
# Simple playtest
script = """MOVE Player TO 5,0,0
WAIT 1.0
ASSERT Player/Health > 0
ASSERT_CONSOLE_CLEAN"""

await run_playtest(script, timeout=30.0)
# → [1/4] MOVE ... — PASS
#   [2/4] WAIT ... — PASS
#   [3/4] ASSERT ... — PASS
#   [4/4] ASSERT_CONSOLE_CLEAN ... — PASS
```

## Fire-and-Forget Pattern

`run_playtest()` returns immediately; you must poll for results.

```python
# Start test (returns immediately)
await run_playtest(script)  # → "tests-started|..."

# Poll every 5 seconds
for i in range(24):  # up to 2 minutes
    result = await get_test_results()
    if result not in ("pending", "none"):
        print(result)
        break
    await asyncio.sleep(5)
```

## DSL Quick Reference (21 Steps)

| Command | Purpose | Example |
|---------|---------|---------|
| ALIAS | Define macro | `ALIAS player_start (100,50,0)` |
| ASSERT | Test condition | `ASSERT Player/Health == 100` |
| ASSERT_BATCH | Multiple asserts | `ASSERT_BATCH ... END` |
| ASSERT_NEAR | Distance check | `ASSERT_NEAR Player Enemy 5.0` |
| ASSERT_CTA | UI button interactable | `ASSERT_CTA StartButton` |
| ASSERT_CONSOLE_CLEAN | No errors | `ASSERT_CONSOLE_CLEAN ignore="warning"` |
| ASSERT_CONSERVED | Value unchanged | `ASSERT_CONSERVED captured_key` |
| ASSERT_CAPTURED | Compare snapshot | `ASSERT_CAPTURED key == 100` |
| CAPTURE | Save value | `CAPTURE health_before = Player/Health` |
| COMMENT | No-op | `# This is a comment` |
| INVARIANT | Always true check | `INVARIANT Player/Health > 0` |
| LOG | Print to results | `LOG Starting combat test` |
| MONITOR | Watch value over time | `MONITOR Player/Health` |
| MOVE | Walk to position | `MOVE Player TO 100,50,0` |
| SCREENSHOT | Capture frame | `SCREENSHOT width=1280 height=720` |
| SIMULATE | Run physics | `SIMULATE duration=2.0 physics=true` |
| SET | Set runtime field | `SET Player/Health 50` |
| TELEPORT | Instant move | `TELEPORT Player 0,0,0` |
| TIMESCALE | Time speed | `TIMESCALE 0.5` |
| WAIT | Sleep | `WAIT 2.0` |
| WAIT_UNTIL | Poll condition | `WAIT_UNTIL Player/IsDead == true timeout=10` |

## Full Example: Combat Test

```python
script = """
# Setup
ALIAS spawn_pos (0,0,0)
ALIAS enemy_pos (5,0,0)
LOG Starting combat scenario

# Snapshot initial state
CAPTURE initial_health = Player/Health

# Move player to enemy
MOVE Player TO ${enemy_pos}
WAIT_UNTIL distance(Player, Enemy) < 1.0 timeout=5

# Trigger combat
INVOKE Enemy AttackPlayer
WAIT 2.0

# Verify damage
ASSERT Player/Health < ${initial_health}
ASSERT_CONSOLE_CLEAN ignore="test_warning"
ASSERT Player/Health > 0  # Should not be dead

LOG Combat completed
SCREENSHOT
"""

await run_playtest(script, timeout=60.0)
```

## Comparison Operators

| Op | Meaning | Example |
|----|---------|---------|
| `==` | Equals | `Health == 50` |
| `!=` | Not equals | `Status != dead` |
| `>` | Greater | `Score > 100` |
| `<` | Less | `Health < 50` |
| `>=` | Greater-equal | `Distance >= 2.0` |
| `<=` | Less-equal | `Time <= 10.0` |
| `~=` | Approx equal (float) | `Position ~= (0,0,0)` |

## Aliases & Substitution

Define once, use everywhere:

```
ALIAS spawn (100,0,0)
ALIAS alive true

TELEPORT Player ${spawn}
ASSERT Player/IsAlive == ${alive}
```

## Common Patterns

**Stress test with fuzz:**
```python
# Generate random playtest script
await fuzz_playtest(steps=50, seed=42)
# → Random moves, waits, assertions; catches edge cases
```

**Before/after snapshots:**
```
CAPTURE health_before = Player/Health
SET Player/Health 25
ASSERT_CAPTURED health_before != Player/Health
```

**Physics conservation check:**
```
MONITOR Player/Velocity
SIMULATE duration=3.0 physics=true
ASSERT Player/Position != <start>
```

## Timeout & Performance

| Config | Default | Notes |
|--------|---------|-------|
| Script timeout | 120s | Total execution time |
| MOVE timeout | 15s | Per movement command |
| WAIT_UNTIL timeout | 5s | Per poll condition |
| SIMULATE steps | 60 fps | physics=true uses fixedDeltaTime |

**Long scenarios:** Increase timeout parameter.
```python
await run_playtest(script, timeout=300.0)  # 5 minutes
```

## Error Handling

| Result | Meaning | Example |
|--------|---------|---------|
| PASS | Assertion true | `ASSERT Health > 0 — PASS` |
| FAIL | Assertion false | `ASSERT Health > 100 — FAIL` |
| ERR | Exception | `ASSERT NonExistent/Field — ERR` |
| TIMEOUT | Deadline exceeded | `WAIT_UNTIL X timeout=5 — TIMEOUT` |

**Stops at first failure.** Check logs for root cause.

## Console Filtering

Ignore known warnings:
```
ASSERT_CONSOLE_CLEAN ignore="DeprecationWarning,test_info"
```

## Report Compression

Long reports (>300 chars) are auto-summarized by Haiku:

```
[Compressed] 24/25 passed
  [3] ASSERT Player/Health > 0 — FAIL
  [15] WAIT_UNTIL enemy_dead timeout=5 — TIMEOUT
```

Full report saved to `.claude/playtest-results.txt` for debugging.

---

**See also:** [Runtime Tools](../tools/runtime.md) for multi-step verification.
