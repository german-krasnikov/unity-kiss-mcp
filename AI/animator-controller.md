# Feature: Animator Controller Management (Phase 16)

## Overview

1 consolidated MCP tool `animator` with 6 actions for managing Unity AnimatorController state machines. Handles parameters, states, transitions with conditions, default state. Uses AnimatorControllerSerializer (read) and AnimatorControllerHelper (write).

## Architecture

```
Claude Code ←─stdio─→ Python MCP Server ←─TCP:9500─→ Unity Editor Plugin
                            │                              │
                     animator tool              CommandRouter (1 case)
                     6 actions                  ExecAnimatorConsolidated
                                                         │
                                              ┌──────────┴──────────┐
                                              │                     │
                                    AnimatorController      AnimatorController
                                    Serializer (read)       Helper (write/CRUD)
```

## Tool Parameters

**Python signature:** `animator(action, path, state?, states?, params?, source?, target?, conditions?, duration?, exit_time?, has_exit_time?, type?, name?)`

## Tool Actions

| Action | Description | Key params |
|--------|-------------|------------|
| `get` | Read controller structure (params, states, transitions). Pass `state` to get single state detail. | `state` (optional) |
| `add_param` | Add parameters: `"Speed:float:0; Jump:trigger"` | `params` |
| `add_state` | Add states: `"Idle:Idle.anim; Walk"` | `states` |
| `add_transition` | Add transition with conditions, duration, exit_time | `source`, `target`, `conditions`, `duration`, `exit_time`, `has_exit_time` |
| `set_default` | Set default state | `state` |
| `remove` | Remove param/state/transition | `type` (param\|state\|transition), `name`, `source`, `target` |

## Condition Format

```
"Speed>0.1"    → Greater
"Speed<0.1"    → Less
"Type=2"       → Equals (also "Type==2")
"State!=0"     → NotEqual
"IsGrounded"   → If (bool/trigger true)
"!IsGrounded"  → IfNot (bool false)
"Param==true"  → Greater 0.5 (bool shorthand)
"Param==false" → Less 0.5 (bool shorthand)
```

Multiple conditions: `"Speed>0.1; IsGrounded"` (AND logic, `;` separator).
Output format uses ` & ` separator between conditions.

## Key Implementation Details

- `GetOrCreateController(path)` auto-creates Animator + controller if missing
- Controller saved to `Assets/Animations/{objectName}.controller`
- `source="*"` maps to `stateMachine.AddAnyStateTransition()` with `canTransitionToSelf=false`
- States auto-positioned at (300, i*80, 0) for clean layout
- Duplicate params/states are skipped with `(exists)` marker
- All mutations use `Undo.RecordObject()` for undo support
- `AssetDatabase.SaveAssets()` after each write operation
- Clip lookup: exact path → Assets/Animations/ → FindAssets search

## Files

| File | Lines | Role |
|------|-------|------|
| `unity-plugin/Editor/AnimatorControllerSerializer.cs` | ~187 | Read controller → text |
| `unity-plugin/Editor/AnimatorControllerHelper.cs` | ~391 | CRUD operations |
| `server/src/unity_mcp/tools/animation.py` | ~70 | Python `animator` tool definition |
| `server/src/unity_mcp/tools/animator_intent_tool.py` | ~127 | NL → DSL → batch (uses sampling) |
| `unity-plugin/Editor/CommandRouter.cs` | +35 | Routing + action handlers |

## Text Output Format

### Overview (action=get, no state param)
```
AnimatorController: Player | 1 layer | 3 params | 4 states
---
params:
  Speed : float = 0
  IsGrounded : bool = true
  Jump : trigger
---
states [Base Layer]:
  * Idle | Idle.anim | 1x
  Walk | Walk.anim | 1x
---
transitions:
  Idle → Walk | Speed>0.1 | 0.15s
  Walk → Idle | Speed<0.1 | 0.15s
  [Any] → Jump | Jump & IsGrounded | 0.1s
```

Note: transitions also show `exit:X` when `hasExitTime` is true. States show `tag:X` if tagged.

### State detail (action=get, state="Idle")
```
state: Idle | Idle | speed:1
---
transitions:
  → Walk | Speed>0.1 | 0.15s
```

## `animator_intent` Tool

Separate NL-to-DSL tool that converts natural language intent into `animator` batch commands via Haiku sampling.

**Python signature:** `animator_intent(target, intent, dry_run=False)`

**DSL keywords:**
```
PARAM <name> <type> <default>    (types: float|int|bool|trigger)
STATE <name> <clip.anim>
DEFAULT <state>
TRANS <src> -> <dst> dur=<float> [if <Param><op><value>]
```

Pipeline: NL intent → Haiku generates DSL → parse + validate (undeclared state/param checks) → build batch lines → execute via `batch` command. `dry_run=True` returns the plan without executing.
