# Code Execution Guide

Execute C# scripts directly in the Unity Editor.

## Overview

`execute_code()` runs arbitrary C# code in the Editor with full access to Unity APIs. Useful for complex mutations that don't fit simple tool parameters.

## Basic Usage

```python
# Simple script
await execute_code("""
var player = GameObject.Find("Player");
player.SetActive(false);
""")

# With return value
result = await execute_code("""
var enemies = FindObjectsOfType<Enemy>();
return enemies.Length.ToString();
""")
# → "3"
```

## Editor Access

You have full access to:

| API | Example |
|-----|---------|
| GameObject API | `GameObject.Find()`, `Instantiate()` |
| Transform | `transform.position`, `SetParent()` |
| Physics | `Physics.Raycast()`, `Physics2D.OverlapArea()` |
| Components | `GetComponent<>()`, `AddComponent<>()` |
| AssetDatabase | `AssetDatabase.LoadAssetAtPath()` |
| EditorApplication | Scene queries, serialization |
| Debug | `Debug.Log()`, `Debug.DrawRay()` |

## Security & Restrictions

**BLOCKED (cannot execute):**
- Code that calls `EditorApplication.Exit()` (can't quit editor)
- Infinite loops (timeout: 5s per script)
- Code accessing `File.Delete()` with system paths (only Assets/ allowed)
- Arbitrary DLL loading
- Network requests (exception: localhost)

**Why blocked:** Prevents accidental game-breaking scripts and security issues.

**Workaround:** Contact support to request additional APIs if needed.

## Undo Integration

```python
await execute_code("""
Undo.RecordObject(player, "custom edit");
player.health = 50;
""", undo_label="set health")
```

**undo_label:** Optional. Groups changes into one undo action with custom name.

**Automatic undo:** Any script changes are batched under "MCP Code Execution" label if not specified.

## Error Handling

Compilation errors stop execution:

```python
# This fails (Player not defined)
await execute_code("Player.SetActive(false)")
# → ERROR: The name 'Player' does not exist in the current context
```

**Solution:** Use GameObject.Find() or instantiate from prefabs:

```python
# Correct
await execute_code("GameObject.Find('Player').SetActive(false)")
```

## Common Patterns

**Batch property changes:**
```python
await execute_code("""
var objects = FindObjectsOfType<MyComponent>();
foreach (var obj in objects) {
    obj.health = 100;
}
""")
```

**Prefab instantiation:**
```python
result = await execute_code("""
var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab");
var instance = Instantiate(prefab, new Vector3(5, 0, 0), Quaternion.identity);
instance.name = "Enemy_1";
return instance.GetInstanceID().ToString();
""")
```

**Query and modify:**
```python
await execute_code("""
var colliders = FindObjectsOfType<Collider>();
foreach (var col in colliders) {
    if (!col.enabled) col.enabled = true;
}
""")
```

## Return Values

Return strings or serializable types:

```python
result = await execute_code("""
var obj = GameObject.Find("Player");
return obj.transform.position.ToString();
""")
# → "(5.0, 0.0, 3.0)"
```

**Types that serialize:**
- int, float, bool, string, Vector3, Quaternion
- Color, Bounds, Rect
- GameObject (as instance ID)

## Play Mode Restrictions

`execute_code()` works in **Edit Mode only**.

For Play Mode state mutations, use `set_runtime_property()` instead.

```python
# Edit Mode: OK
await execute_code("GameObject.Find('Player').SetActive(false)")

# Play Mode: Must use runtime API
await set_runtime_property("/Player", "PlayerController", "Health", "50")
```

## Timeout & Performance

- **Timeout:** 5 seconds per script
- **Long operations:** Split into multiple calls
- **Compilation:** First call warm (~500ms); cached after

```python
# Time-consuming: split it
for i in range(100):
    await execute_code(f'/* process batch {i} */')
```

---

**See also:** [Object Tools](../tools/objects.md) for scene editing APIs, [Batch Reference](../tools/batch.md) for batching strategies.
