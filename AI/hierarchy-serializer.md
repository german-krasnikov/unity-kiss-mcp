# Feature: Serializers

## Overview

Сериализация Unity данных в компактный текстовый формат:
- **HierarchySerializer**: сцена → текстовое дерево (11x сжатие)
- **ComponentSerializer**: компоненты → ключ-значение (90% vs JSON)
- **ConsoleCapture**: логи → форматированный текст
- **ScreenshotCapture**: окно → base64 PNG

## Architecture (для Architect)

### Output Format (default, components=false)

```
Main Camera $a
Directional Light $b
GameManager $c
├─ UIRoot $d
│  ├─ HealthBar $e
│  └─ PauseMenu $f !
Player $g
├─ Body $h
└─ WeaponSlot $i
   └─ Sword $j
```

### Output Format (components=true)

```
Main Camera [Camera,AudioListener] $a
Directional Light [Light] $b
GameManager [GameManager,AudioSource] $c
├─ UIRoot [Canvas,CanvasScaler] $d
│  ├─ HealthBar [Image,HealthBarUI] $e
│  └─ PauseMenu [CanvasGroup] $f !
Player [Rigidbody,PlayerController] $g
├─ Body [SkinnedMeshRenderer] $h
└─ WeaponSlot [] $i
   └─ Sword [MeshFilter,MeleeWeapon] $j
```

### Format Rules

| Element | Format | Example |
|---------|--------|---------|
| Name | plain text | `Main Camera` |
| Short ref | `$a`-`$zz` (702 slots via RefManager) | `$a`, `$ab` |
| Components | `[Type1,Type2]` (only when `components=true`) | `[Camera,AudioListener]` |
| Transform | OMITTED (100% have it) | - |
| Inactive | `!` suffix | `PauseMenu ... !` |
| Depth-truncated | `+N` descendant count | `WeaponSlot $i +3` |
| Tree chars | `├─ └─ │` | Unicode box drawing |
| MAX_NODES | 3000, truncates with message | `... truncated at 3000 nodes` |
| Sibling truncation | when MAX_NODES hit mid-children | `... +5 siblings` |

### Token Budget

- 50 objects: ~350 tokens (vs ~4000 JSON)
- Compression: **11x**

## Implementation Notes (для Developer)

### HierarchySerializer.cs

```csharp
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    public static class HierarchySerializer
    {
        public const int MAX_NODES = 3000;

        public static string Serialize(int depth = 99, string root = null, string filter = null, bool components = false)
        {
            var sb = new StringBuilder();
            var roots = GetRootObjects(root);
            int nodeCount = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                var isLast = (i == roots.Length - 1);
                SerializeObject(sb, roots[i], depth, 0, new List<bool>(), isLast, filter, ref nodeCount, components);
                if (nodeCount >= MAX_NODES) break;
            }

            if (nodeCount >= MAX_NODES)
                sb.AppendLine($"... truncated at {MAX_NODES} nodes. Use filter/root/depth to narrow.");

            return sb.ToString();
        }

        // Key per-node logic (simplified):
        // 1. AppendIndent(sb, parentIsLast, isLast)  — parent continuation lines + connector
        // 2. sb.Append(go.name)
        // 3. if (components) → sb.Append(" [Type1,Type2]")  — Transform omitted, no space after comma
        // 4. sb.Append(' ').Append(RefManager.Assign(go))   — short ref ($a-$zz) instead of instance ID
        // 5. if (!go.activeSelf) sb.Append(" !")
        // 6. if (depth-truncated && has children) sb.Append(" +").Append(descendantCount)
        // 7. Recurse children; at MAX_NODES mid-children → "... +N siblings"

        // Incremental cache: SerializeIncremental() returns "NO_CHANGE" if hierarchy unchanged
        // Summary mode: SerializeSummary() returns compact root-level overview
        // Subtree: SerializeSubtree(go, depth) for single object

        private static GameObject[] GetRootObjects(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                // Check prefab stage first
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null)
                    return new[] { stage.prefabContentsRoot };

                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                return scene.GetRootGameObjects();
            }

            var root = ComponentSerializer.FindObject(rootPath);
            return root != null ? new[] { root } : new GameObject[0];
        }
    }
}
```

### Version Tracking

```csharp
[InitializeOnLoad]
public static class VersionTracker
{
    private static int _version = 0;

    static VersionTracker()
    {
        EditorApplication.hierarchyChanged += IncrementVersion;
        Undo.undoRedoPerformed += IncrementVersion;
    }

    public static int Version => System.Threading.Volatile.Read(ref _version);

    private static void IncrementVersion()
    {
        System.Threading.Interlocked.Increment(ref _version);
    }
}
```

### Incremental Cache

`SerializeIncremental()` stores last serialized result. Returns `"NO_CHANGE"` if hierarchy is identical.
`ResetIncrementalCache()` clears stored state.
No Python-side cache — all calls go to Unity via `bridge.send("get_hierarchy", {...})`.

## Code Locations

**C#:**
- `unity-plugin/Editor/HierarchySerializer.cs` — Scene → text tree (MAX_NODES=3000, summary mode, incremental cache, $refs)
- `unity-plugin/Editor/ComponentSerializer.cs` — Component → key-value + ObjectReference + UnityEvent
- `unity-plugin/Editor/VersionTracker.cs` — Version tracking
- `unity-plugin/Editor/ConsoleCapture.cs` — Logs → text
- `unity-plugin/Editor/ScreenshotCapture.cs` — Camera modes: default, overview, overview_game, multi_view
- `unity-plugin/Editor/MultiViewCapture.cs` — 4-panel grid (Front, Left, Top, Isometric)
- `unity-plugin/Editor/MultiViewOverlay.cs` — Overlay rendering for multi-view
- `unity-plugin/Editor/OverlayDrawer.cs` — Drawing utilities
- `unity-plugin/Editor/RefManager.cs` — Short refs ($a-$zz, 702 slots)

## ComponentSerializer (427 lines)

### Output Format

```
name: Player
active: true
tag: Player
layer: Default
---
[Transform]
m_LocalPosition: (1.2, 3.4, 5.6)
m_LocalRotation: (0, 0.7, 0, 0.7)
m_LocalScale: (1, 1, 1)
---
[Rigidbody]
m_Mass: 1
m_Drag: 0
m_UseGravity: true
```

**Features:**
- Uses `SerializedProperty` API — serializes all visible fields via `prop.name: value`
- Separator `: ` (colon-space), sections separated by `---`
- Component headers: `[TypeName]`
- Handles all built-in types: int, float, bool, string, Vector3, Color, Enum, ObjectReference, etc.
- **Float format**: ToString("G4") — 4 significant figures (e.g., 1.234, 0.005678 → "0.005678") for 300-600 token savings per response
- Null reference → `null`
- Arrays → element-per-line with `[i]` indices
- Skips internal Unity properties (m_Script, m_ObjectHideFlags, etc.)
- ~90% compression vs JSON

### Usage

```csharp
string text = ComponentSerializer.Serialize("/Player", "Rigidbody");
```

## ConsoleCapture (196 lines)

### Output Format

```
[Log] 14:30:22.123 Log message here
[Warning] 14:30:22.456 Warning message
[Error] 14:30:22.789 Error message
```

**Features:**
- Captures Unity debug logs via `Application.logMessageReceived`
- Two-phase buffer: init buffer (50 entries, 5s window) + ring buffer (450 entries)
- Filters by level: Log, Warning, Error
- `count=-1` returns all; `first=N` returns first N from init + last from ring
- Thread-safe (lock-based)
- `GetErrorsSince(DateTime since)` for post-playtest error checks

### Usage

```csharp
var logs = ConsoleCapture.GetLogs(count: 20, level: "Error");
var errors = ConsoleCapture.GetErrorsSince(startTime, maxCount: 5);
ConsoleCapture.Clear();
```

## ScreenshotCapture

### Output Format

Base64-encoded PNG or file path.

**Features:**
- Camera modes: `default`, `overview`, `overview_game`, `multi_view`
- Custom width/height, supersample (1-4x)
- `multi_view`: 4-panel grid (Front, Left, Top, Isometric) of target object
  - Params: path, cellSize, custom angles, zoom, offset, fixed_size, highlight
  - Returns file path + optional manifest (for highlight markers)
- `overview`: Top-down scene capture
- `highlight`: Draw markers on specific objects
- Saves to file or returns base64

## TDD Scenarios (для Developer)

### Implemented Tests (all passing)

**HierarchySerializer** (Unity plugin, no Python tests for C# code):
- Serialize empty scene → empty string
- Serialize single object → correct format
- Components only shown when `components=true`
- Omit Transform from component list
- Short refs via RefManager (`$a`-`$zz`)
- Mark inactive objects with `!`
- Depth limiting works + `+N` descendant count suffix
- MAX_NODES=3000 truncation + sibling truncation
- Tree characters render correctly `├─ └─ │`
- Incremental cache returns `NO_CHANGE` when unchanged
- Summary mode (`SerializeSummary`)
- Version tracking increments on hierarchy change (thread-safe)

**ComponentSerializer**:
- Serialize built-in types (Vector3, Color, Quaternion)
- Handle null references
- Array serialization
- Nested object handling

**ConsoleCapture**:
- Capture debug logs
- Filter by level
- Limit to last N entries
- Preserve timestamps

**ScreenshotCapture**:
- Render viewport to texture
- Export as PNG
- Support custom dimensions

## Review Checklist (для Reviewer)

- [ ] Transform always omitted from component list
- [ ] Tree chars render correctly
- [ ] Short refs ($a-$zz) via RefManager, not instance IDs
- [ ] Inactive objects marked with `!`
- [ ] Components only shown when `components=true`
- [ ] Depth limiting works + `+N` descendant suffix
- [ ] MAX_NODES=3000 truncation works
- [ ] Version increments on hierarchy change (thread-safe)
- [ ] Prefab stage detection in GetRootObjects

## Related

- Skill: `.claude/skills/token-optimization.md`
- Knowledge: `AI/tcp-bridge.md`
