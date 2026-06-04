# Feature: Spatial Queries

## Overview

Spatial queries for proximity, overlap, raycasting, and scene visualization. Single MCP tool `spatial_query` with action-based routing: `nearest`, `in_front_of`, `objects_in_radius`, `bounds_info`, `raycast`, `spatial_map`.

## Architecture

```
Claude Code ←─stdio─→ Python MCP Server ←─TCP:9500─→ Unity Editor Plugin
                            │                              │
                     spatial_query tool            CommandRouter (1 case)
                                                         │
                                                   SpatialHelper.cs
                                    (Physics casts, grid layout, distance math)
```

## Implementation Notes

### Actions

- `nearest` — find closest object (optionally filtered by component name)
- `in_front_of` — position in front of object at distance (returns world position)
- `objects_in_radius` — list all objects within radius (around path OR center)
- `bounds_info` — detailed bounds/dimensions of object
- `raycast` — cast ray from path/pos to target, returns hits sorted by distance
- `spatial_map` — ASCII grid map of objects in XZ plane (cell_size in meters)

### Parameters

**Core:**
- `action` (required) — one of the 6 actions above
- `path` (required for most actions, optional for `objects_in_radius`) — object path or scene path
- `center` (optional, new in 0.5.0) — world-position origin as `"x,y,z"` string. For `objects_in_radius`, alternative to `path` (center wins when both given)

**Action-specific:**
- `nearest`: `component` (filter by component type)
- `in_front_of`: `distance` (how far in front, in meters)
- `objects_in_radius`: `radius` (search radius in meters)
- `raycast`: `target` (destination path/pos), `component` (optional filter)
- `spatial_map`: `cell_size` (grid cell size in meters)
- `raycast`, `objects_in_radius`: `layer_mask` (optional physics layer filter)

### Output Format

**nearest:**
```
Enemy #3001 [Rigidbody,EnemyAI] (distance: 5.23m)
```

**in_front_of:**
```
Position: (10.5, 0.0, 15.2)
```

**objects_in_radius:**
```
Rock_1 #4050 [Renderer] (5.1m)
Rock_2 #4051 [Renderer,Rigidbody] (6.8m)
```

**bounds_info:**
```
Center: (5.0, 1.0, 3.0)
Size: (2.0, 2.5, 1.5)
Extents: (1.0, 1.25, 0.75)
```

**raycast:**
```
Enemy_Boss #2000 [Rigidbody,Health,Animator] (distance: 10.5m)
Rock_Barrier #3050 [BoxCollider] (distance: 8.2m)
```

**spatial_map:**
```
XZ Plane Grid (cell=1.0m):
  10 |               * * *
  09 |             * B *
  08 |           *       *
  ...
  00 +---+---+---+---+---+
     0   5  10  15  20
  * = object, B = breadcrumb/reference
```

## Code Locations

- Python tool: `server/src/unity_mcp/tools/spatial.py` (1 tool with 6 actions)
- C# helper: `unity-plugin/Editor/SpatialHelper.cs` (Physics.Raycast, bounds, grid layout)
- C# command: `unity-plugin/Editor/CommandRouter.cs` (`CommandRegistry.Register`)
- Python tests: `server/tests/test_spatial_center.py` (7 tests), `server/tests/test_spatial_new.py` (3 tests)
- C# tests: `unity-test-project/Assets/Tests/Editor/MCSpatialTests.cs` (16 tests)

## MCP Tool

### `spatial_query`

**Parameters:**
- `action` (required) — nearest | in_front_of | objects_in_radius | bounds_info | raycast | spatial_map
- `path` (required for most actions, optional for objects_in_radius)
- `center` (optional, new in 0.5.0) — `"x,y,z"` world position (used for objects_in_radius; overrides path when both given)
- `target`, `distance`, `radius`, `component`, `cell_size`, `layer_mask` — action-specific

```
# Find closest Rigidbody
spatial_query(action="nearest", path="/Player", component="Rigidbody")
→ Enemy #3001 [Rigidbody,EnemyAI] (distance: 5.23m)

# Position in front of object
spatial_query(action="in_front_of", path="/Player", distance=3.0)
→ Position: (10.5, 0.0, 15.2)

# Objects within radius around world position (new in 0.5.0)
spatial_query(action="objects_in_radius", center="10,1,20", radius=5.0)
→ Rock_1 #4050 [Renderer] (5.1m)
  Rock_2 #4051 [Renderer,Rigidbody] (6.8m)

# Raycast from/to objects
spatial_query(action="raycast", path="/Player", target="/Enemy_Boss")
→ Enemy_Boss #2000 [Rigidbody,Health,Animator] (distance: 10.5m)
  Rock_Barrier #3050 [BoxCollider] (distance: 8.2m)

# Scene grid map
spatial_query(action="spatial_map", path="/Level", cell_size=1.0)
→ XZ Plane Grid (cell=1.0m):
  ...ASCII grid...
```

## TDD Scenarios

### Red Phase
1. **test_spatial_center_forwarded**: center → sends to bridge
2. **test_spatial_center_none_omitted**: center=None → not sent (default limit optimization)
3. **test_spatial_center_and_path_both_forwarded**: both given → both sent (let C# decide precedence)
4. **test_spatial_center_ignored_for_nearest**: center ignored for nearest action
5. **test_spatial_objects_in_radius_center_only_no_path**: center works without path

C# tests (16 total):
1. **SpatialHelper_Nearest_FindsClosest**: distance calculation
2. **SpatialHelper_ObjectsInRadius_WithCenter**: center origin for objects_in_radius
3. **SpatialHelper_Raycast_ReturnsHitsSorted**: raycast hit order
4. **SpatialHelper_SpatialMap_GeneratesGrid**: ASCII grid generation

### Green Phase
- Python: 9 unit tests (center override, omit logic)
- C#: SpatialHelper.cs (Physics.Raycast, bounds caching, grid building)

## Review Checklist

- [ ] Security: Physics.Raycast safe, no eval, correct layer mask handling
- [ ] Performance: radius queries use Physics.OverlapSphere (not O(n)), grid layout bounded
- [ ] Token efficiency: text format ~8x smaller than JSON
- [ ] Edge cases: no objects found, invalid path/center handled

## Related

- Skill: `.claude/skills/csharp-unity.md` (Editor API, Physics)
- Knowledge: `AI/hierarchy-serializer.md` (object formatting)
- Tool: `search_scene` (complementary name/tag/component search)
