# Scene Tools

Load, modify, inspect, and capture the state of your Unity scenes. Core operations for scene management and visual verification.

## get_hierarchy

Read the current scene's GameObject hierarchy as a text tree.

**Parameters:**
- `depth` (int, default=2) — Tree depth to traverse
- `root` (string, optional) — Scope to subtree (path or None for whole scene)
- `filter` (string, optional) — Filter objects by name substring
- `components` (bool, default=false) — Include component list `[Type1,Type2]` on each object
- `compress` (bool, default=false) — Group repeated slots/points/meshes
- `summary` (bool, default=false) — Compact root-only counts (60-100 tokens)
- `incremental` (bool, default=false) — Return NO_CHANGE if scene unchanged since last call
- `scene` (string, optional) — Filter to a single scene by name (multi-scene only)

**Output Format:**

Single scene:
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

Multi-scene:
```
[MainScene]
Main Camera $a
Directional Light $b

[AdditiveScene]
Player $c
├─ Body $d
```

With components:
```
Main Camera [Camera,AudioListener] $a
Player [Rigidbody,PlayerController] $b
├─ Body [SkinnedMeshRenderer] $c
```

**Example:**

```python
# Basic hierarchy
hier = await get_hierarchy()
print(hier)

# With components
hier = await get_hierarchy(components=true)
```

**Use Cases:**
- Verify scene structure before running tests
- Check if objects are active/inactive
- Quick reference for object paths in batch operations

---

## search_scene

Find GameObjects by name, component type, tag, or layer.

**Parameters:**
- `query` (string) — Search term. Syntax: `name text`, `t:Component`, `tag=Tag`, `layer=N`, `active=bool`. Combine with spaces.
- `root` (string, optional) — Scope search to subtree (path or None for whole scene)
- `limit` (int, default=50) — Max results (0 = unlimited)
- `scene` (string, optional) — Filter to a single scene by name (multi-scene only)

**Output Format:**
```
Player $a (layer=Default, tag=Player, active=true)
├─ Head $b
├─ Body $c
└─ Legs $d
```

**Example:**

```python
# Find by name
results = await search_scene(query="Player")

# Find all objects with Rigidbody
results = await search_scene(query="t:Rigidbody")

# Find all enemies (tag)
results = await search_scene(query="tag=Enemy")

# Scope to subtree
results = await search_scene(query="Health", root="Player")
```

---

## scene

Open, close, or manage scenes. Control which scenes are loaded additively.

**Parameters:**
- `action` (string) — "new" | "open" | "save" | "discard" | "open_additive" | "close" | "set_active" | "list"
- `path` (string) — Scene name or path (e.g., "MainScene" or "Assets/Scenes/MainScene.unity"). Required for open/save/open_additive/close/set_active.

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| list | Show all loaded scenes | — | `scene("list")` |
| open_additive | Load scene without unloading current | path | `scene("open_additive", path="AdditiveLevel")` |
| close | Unload scene | path | `scene("close", path="MainScene")` |
| set_active | Make scene the active scene | path | `scene("set_active", path="MainScene")` |

**Example:**

```python
# List loaded scenes
scenes = await scene("list")

# Load additional scene
await scene("open_additive", path="UI")

# Set MainScene as active
await scene("set_active", path="MainScene")

# Unload UI scene
await scene("close", path="UI")
```

---

## screenshot

Capture the current game view as a PNG image. Optionally annotate with object refs.

**Parameters:**
- `width` (int, default=1920) — Image width in pixels
- `height` (int, default=1080) — Image height in pixels
- `camera` (string, optional) — Camera name (default: main camera)
- `annotation_id` (string, optional) — Draw labels for object path

**Output:** Base64-encoded PNG, saved to `unity-test-project/ScreenShots/YYYY-MM-DD_HH-MM-SS.png`

**Example:**

```python
# Basic screenshot
img = await screenshot()

# Custom size
img = await screenshot(width=1280, height=720)

# Specific camera
img = await screenshot(camera="UICamera")

# With annotations (shows Player path labels)
img = await screenshot(annotation_id="Player")
```

**Use Cases:**
- Visual verification during playtests
- Save visual baselines for diff comparison
- Capture multi-view for debugging spatial issues

---

## editor

Control Play/Pause/Stop and selection. Core editor state operations.

**Parameters:**
- `action` (string) — "play" | "pause" | "stop" | "select" | "frame" | "focus"
- `path` (string, optional) — GameObject path for select/frame/focus

**Actions:**

| Action | Purpose | Example |
|--------|---------|---------|
| play | Enter Play Mode | `editor("play")` |
| pause | Pause Play Mode | `editor("pause")` |
| stop | Exit Play Mode | `editor("stop")` |
| select | Highlight object in Hierarchy | `editor("select", path="Player")` |
| frame | Zoom camera on object | `editor("frame", path="Player")` |
| focus | Activate editor window | `editor("focus")` |

**Example:**

```python
# Enter play mode
await editor("play")

# Select Player in Hierarchy
await editor("select", path="Player")

# Frame camera on Player
await editor("frame", path="Player")

# Pause simulation
await editor("pause")

# Exit play mode
await editor("stop")
```

---

## checkpoint

Create a named Undo checkpoint. Allows rollback via Ctrl+Z in Unity.

**Parameters:**
- `label` (string, default="checkpoint") — Checkpoint identifier

**Example:**

```python
# Save full scene state
await checkpoint(label="before_combat")

# Make changes...
await set_property("Player", "Health", "hp", "50")

# Compare later via scene_diff
diff = await scene_diff()
```

---

## fingerprint

Generate hash of scene state for comparison across runs.

**Parameters:**
- `path` (string, optional) — Scope to subtree (default: whole scene)
- `depth` (int, default=3) — Depth to hash

**Output:** Single-line fingerprint (~5 tokens): `fp:XXXXXXXX`

**Example:**

```python
# Get scene fingerprint
fp1 = await fingerprint()

# Modify objects...
await set_property("Player", "Transform", "position", "10,0,0")

# Compare
fp2 = await fingerprint()
assert fp1 != fp2, "Scene state should have changed"
```

---

## scene_diff

Compare current scene state with last snapshot. First call saves the snapshot; subsequent calls return the diff.

**Parameters:** None

**Output:** Added/removed lines showing what changed.

**Example:**

```python
await scene_diff()  # saves snapshot

await invoke_method("Enemy", "HealthComponent", "TakeDamage", args="10")

diff = await scene_diff()
# → "Enemy/Health == 90 (was 100)"
```

---

## run_tests

Execute NUnit tests in EditMode or PlayMode.

**Parameters:**
- `mode` (string) — "EditMode" | "PlayMode" (required)
- `filter` (string, optional) — Pipe-separated test class names for fast focused runs

**Returns immediately** with message "tests-started|{mode}|...". Poll `get_test_results()` every 5 seconds.

**Example:**

```python
# Start Edit Mode tests
result = await run_tests(mode="EditMode")
# → "tests-started|EditMode|poll get_test_results every 5s"

# Poll for results (in a loop)
import asyncio
for i in range(24):  # 2 minutes
    status = await get_test_results()
    if status not in ("pending", "none"):
        print(f"Tests complete: {status}")
        break
    await asyncio.sleep(5)

# Run only failing tests (much faster)
await run_tests(mode="EditMode", filter="HealthTest|DamageTest")
```

**Full workflow:**
1. Run all EditMode tests first (fast gate)
2. If pass, run PlayMode tests
3. PlayMode must run AFTER all MCP mutations (reconnects to Unity)

---

## get_test_results

Poll test execution status after `run_tests()`.

**Parameters:** None

**Output:** Test result summary with pass/fail counts, or "pending" if still running.

**Example:**

```python
# After run_tests()...
result = await get_test_results()
# → "EditMode: 150 passed, 0 failed (45.2s)"
# → "pending" (still running)
```

---

## save_session

Save current scene state and console history for recovery.

**Parameters:**
- `name` (string) — Session identifier

**Example:**

```python
await save_session(name="checkpoint_1")
```

---

## load_session

Restore previously saved scene state.

**Parameters:**
- `name` (string) — Session identifier

**Example:**

```python
await load_session(name="checkpoint_1")
```

---

## screenshot_baseline

Save current screenshot as reference for visual regression testing.

**Parameters:**
- `name` (string) — Baseline identifier
- `camera` (string, optional) — Camera name

**Example:**

```python
# Capture reference screenshot
await screenshot_baseline(name="main_menu")

# Later: compare current view
diff = await screenshot_compare(baseline="main_menu")
```

---

## screenshot_compare

Compare current view against saved baseline. Highlights differences.

**Parameters:**
- `baseline` (string) — Baseline name from screenshot_baseline()
- `threshold` (float, default=0.1) — Pixel diff tolerance (0.0-1.0)

**Output:** Diff image showing changed pixels, similarity percentage.

**Example:**

```python
# Setup
await screenshot_baseline(name="level_1")

# Later: verify identical
diff = await screenshot_compare(baseline="level_1")
# → "Similarity: 99.8% (5 pixels different)"
```

---

## get_changes

List all objects and properties modified since last checkpoint.

**Parameters:**
- `since` (string, optional) — Checkpoint name (default: start of session)

**Output:** List of modified objects with property deltas.

**Example:**

```python
await get_changes()
# → Player/Transform/position: (0,0,0) → (5,0,0)
# → Player/Health/hp: 100 → 80
```

---

## recompile

Trigger Unity to reimport C# scripts. Returns immediately; use `await_compile` to block until done.

**Parameters:** None

**Example:**

```python
await recompile()
await await_compile(timeout=30)
```

---

## checkpoint (continued) — Advanced Usage

Use checkpoints + scene_diff for test assertions:

```python
# Playtest sequence
await checkpoint(label="start")
await editor("play")
await wait_until(path="Player", component="Health", field="hp", value="100", timeout=10)

# Simulate combat
await invoke_method(path="Player", component="Health", method="TakeDamage", args="10")

# Verify state change
diff = await scene_diff()
assert "90" in diff, "Health should drop to 90"

await editor("stop")
```

---

## Common Patterns

| Task | Tools | Example |
|------|-------|---------|
| Verify scene structure | get_hierarchy + search_scene | `hier = await get_hierarchy()` |
| Track object changes | checkpoint + scene_diff | `await checkpoint(label="before"); ...; diff = await scene_diff()` |
| Visual regression testing | screenshot_baseline + screenshot_compare | `await screenshot_baseline(name="x"); diff = await screenshot_compare(baseline="x")` |
| Run tests after changes | run_tests + get_test_results | `await run_tests(mode="EditMode"); await asyncio.sleep(5); result = await get_test_results()` |
| Load scenes additively | scene("open_additive") | `await scene("open_additive", path="AdditiveScene")` |

---

**See also:** [Runtime Tools](runtime.md) for Play Mode operations, [Batch](batch.md) for multi-operation efficiency.
