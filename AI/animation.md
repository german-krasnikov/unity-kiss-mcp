# Feature: Animation Support (Phase 7)

## Overview

1 consolidated MCP tool `animation` with 4 actions (get, create, edit, preview) for reading, creating, editing, and previewing animations in Unity Editor. Integrates with AnimationSerializer (reads clips/curves) and AnimationHelper (creates/edits/previews), following the pattern: Python tool → bridge.send(cmd) → CommandRouter → handler → text response.

## Architecture

```
Claude Code ←─stdio─→ Python MCP Server ←─TCP:9500─→ Unity Editor Plugin
                            │                              │
                     animation tool (1)          CommandRouter (1 case)
                     4 actions                   ExecAnimationConsolidated (switch)
                                                         │
                                              ┌──────────┴──────────┐
                                              │                     │
                                    AnimationSerializer     AnimationHelper
                                    (read: clips → text)    (write/preview)
```

## Implementation Notes

### Data Storage
- Animation clips live in `.anim` asset files (created in `Assets/Animations/`)
- Curves stored in AnimationClip via EditorCurveBinding
- Keyframes hold (time, value) with interpolation info

### Constraints
- Editor-only: AnimationMode API (sampling, preview)
- Property names map to internal bindings: `localPosition` → `m_LocalPosition.x/.y/.z`
- Vector3 properties require 3 float curves (one per axis)
- Keyframe output limited to 50 per curve (shows `,...+N more` if exceeded)

### Edge Cases
- No Animator/Animation component: return clear error
- AnimationMode already active: check `InAnimationMode()` before starting
- Legacy Animation vs Animator: support both (check Animator first, then Animation)

### Sub-Actions Flattening (Bug Fix)
- Phase 16 fix: `animation action=edit` was broken — sub-actions (add_key, remove_key, etc.) were passed as separate args but re-extracted, losing the actual sub-action
- **Solution:** Sub-actions now routed as top-level cases in CommandRouter.ExecAnimationConsolidated() switch statement
- **New pattern:** `action` param contains the sub-action directly (add_key|remove_key|remove_curve|set_keys|set_loop)
- See `unity-plugin/Editor/CommandRouter.cs` ExecAnimationConsolidated() for implementation

## Code Locations

- Python tool: `server/src/unity_mcp/tools/animation.py` (animation, timeline, animator, particle)
- Serializer: `unity-plugin/Editor/AnimationSerializer.cs`
- Helper: `unity-plugin/Editor/AnimationHelper.cs`
- Commands: `unity-plugin/Editor/CommandRouter.cs` (ExecAnimationConsolidated case)
- Python tests: `server/tests/test_server.py` + `test_server_edge_cases.py`
- C# tests: `unity-test-project/Assets/Tests/Editor/MCPPluginTests.cs`

## MCP Tools

### `animation` (single consolidated tool)

**Parameters:** `action` (required), `path` (required), `clip` (optional), `clip_name` (optional), `property` (optional), `keys` (optional), `time` (optional)

#### action=get — List clips or show details
```
# List all clips on object
animation(action="get", path="/Player")
→ Animator: Idle, Walk, Jump
  ---
  Idle | 1.0s | 3 curves
  Walk | 0.8s | 6 curves | loop

# Show clip detail (value@time format, comma-separated)
animation(action="get", path="/Player", clip="Walk")
→ clip: Walk | 0.8s | loop
  ---
  m_LocalPosition.x: 0@0,1.5@0.4,0@0.8

# Sample at time
animation(action="get", path="/Player", clip="Walk", time=0.4)
→ sample: Walk @ 0.40s
  ---
  m_LocalPosition.x: 1.5
  m_LocalPosition.y: 0.5
```

#### action=create — Create new AnimationClip
**Params:** `path`, `clip_name`, `property` (default="localPosition"), `keys` (keyframe string)

Creates new AnimationClip with curves, saves to `Assets/Animations/{clip_name}.anim`, attaches to object's Animator.

Key format: `t:<time> v:<value>` separated by `;`
- Vector3: `t:0 v:(0,0,0); t:1 v:(0,2,0)`
- Float: `t:0 v:0; t:0.5 v:1; t:1 v:0`

#### action=edit (or sub-action directly: add_key|remove_key|remove_curve|set_keys|set_loop)
**Params:** `path`, `clip`, `action`, `property` (optional), `keys` (optional)

Modify existing clip. Sub-actions passed as `action` value:
- `add_key` — insert keyframes (property + keys required)
- `remove_key` — delete keyframe at time (property + `t:0.5` required)
- `remove_curve` — delete entire curve (property required)
- `set_keys` — replace all keyframes (property + keys required)
- `set_loop` — toggle clip looping (keys="false" to disable, anything else to enable)

#### action=preview — Preview in Edit Mode
**Params:** `path`, `clip`, `time` (optional, default=0.0)

The `action` value is one of: `sample` (default on C# side), `start`, `stop`.
- `sample` — pose object at time, return sampled values
- `start` — enter AnimationMode
- `stop` — exit AnimationMode, restore original pose

## TDD Scenarios

### Red Phase
1. **test_get_animation_calls_bridge**: path only → sends correct command
2. **test_get_animation_with_clip**: with clip name → sends clip arg
3. **test_get_animation_with_time**: with time → sends time arg
4. **test_get_animation_error**: error response → formatted error string
5. **test_create_animation_calls_bridge**: creates clip → sends all args
6. **test_edit_animation_calls_bridge**: edits clip → sends correct action
7. **test_preview_animation_calls_bridge**: preview with time → sends correct args
8. **test_preview_animation_defaults**: action/time defaults applied

C# tests (6 total):
1. **CreateAnimation_CreatesClipWithKeyframes**: create → get_animation → verify clip listed
2. **GetAnimation_ListsAllClips**: list clips → verify names in output
3. **GetAnimation_ClipDetail_ShowsCurvesAndKeyframes**: clip detail → verify curves + keyframes
4. **EditAnimation_AddKey_InsertsKeyframe**: add_key → get_animation → verify keyframe added
5. **EditAnimation_RemoveCurve_DeletesCurve**: remove_curve → verify count reduced
6. **PreviewAnimation_Sample_ReturnsSampledValues**: sample at time → verify interpolated values

### Green Phase
- Python: 1 tool function (`animation`) + 8 tests (all passing)
- C#: AnimationSerializer (Serialize, SerializeClipList, SerializeClipDetail, SerializeClipAtTime)
- C#: AnimationHelper (CreateClip, EditClip, Preview, ParseKeys methods)
- CommandRouter: 1 registered command (`animation`) → ExecAnimationConsolidated switch → 4 Exec methods

## Review Checklist

- [x] Security: no path traversal (GameObject.Find validates), API calls safe
- [x] Performance: keyframe limit 50/curve prevents token bloat, no unnecessary sampling
- [x] Token efficiency: text format ~5x smaller than JSON equivalent
- [x] Edge cases: no Animator → error, AnimationMode → checked before start, vector expansion → handled

## Related

- Skill: `.claude/skills/csharp-unity.md` (Editor API)
- Knowledge: `AI/architecture.md`
