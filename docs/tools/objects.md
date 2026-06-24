# Object Tools

Create, modify, and inspect GameObjects. Manage components, properties, and object relationships.

## get_component

Read a component's properties from a scene object.

**Parameters:**
- `path` (string) — GameObject path (e.g., "Player" or "Player/Head")
- `component` (string) — Component type (e.g., "Transform", "Rigidbody", "Health")

**Output Format:**
```
Transform
  position: 0,1,0
  rotation: 0,0,0,1
  scale: 1,1,1
  eulerAngles: 0,0,0
```

**Example:**

```python
# Read Transform
transform = await get_component("Player", "Transform")

# Read custom component
health = await get_component("Player", "Health")
# → "maxHp: 100"
# → "currentHp: 85"

# Read from nested object
renderer = await get_component("Player/Body", "SkinnedMeshRenderer")
```

**Use Cases:**
- Verify object state before/after operations
- Read numeric values (position, health, score) for test assertions
- Check component enablement and settings

---

## inspect

Read multiple components from one or more objects in one call.

**Parameters:**
- `paths` (list or string) — Single path or comma-separated list
- `components` (list or string, optional) — Components to read (default: all)

**Output Format:**
```
Player
  Transform: position=(0,1,0), rotation=(0,0,0,1)
  Rigidbody: mass=1.0, useGravity=true
  Health: maxHp=100, currentHp=85

Enemy
  Transform: position=(5,0,0), rotation=(0,0,0,1)
  Health: maxHp=50, currentHp=30
```

**Example:**

```python
# Inspect single object (all components)
info = await inspect(paths="Player")

# Inspect multiple objects
info = await inspect(paths="Player,Enemy,Boss")

# Inspect specific components only
info = await inspect(paths="Player", components="Transform,Health")

# Combine with batch for max efficiency
result = await batch("""
inspect paths=Player,Enemy components=Health,Rigidbody
""")
```

**Use Cases:**
- Quick snapshot of multiple objects
- Verify component state in test assertions
- Much faster than individual get_component calls (use in batch for 80% token savings)

---

## set_property

Change a component property on a scene object.

**Parameters:**
- `path` (string) — GameObject path
- `component` (string) — Component type
- `prop` (string) — Property name
- `value` (string) — New value (always as string; types inferred by Unity)

**Type Inference:**
- `"true"` / `"false"` → bool
- `"1.5"` → float
- `"100"` → int
- `"0,1,0"` → Vector3
- `"1,0,0,1"` → Quaternion (normalized)
- `"#FF0000"` → Color (hex)
- Plain text → string

**Example:**

```python
# Position
await set_property("Player", "Transform", "position", "10,5,0")

# Rotation (quaternion or euler)
await set_property("Player", "Transform", "rotation", "0,90,0")

# Scale
await set_property("Player", "Transform", "scale", "2,2,2")

# Health (custom component)
await set_property("Player", "Health", "maxHp", "150")

# Color
await set_property("Player/Renderer", "Material", "_Color", "#FF0000")

# Boolean
await set_property("Player", "Rigidbody", "isKinematic", "true")

# Batch multiple (93% token savings!)
await batch("""
set_property path=Player component=Transform prop=position value=0,1,0
set_property path=Player component=Health prop=maxHp value=100
set_property path=Enemy component=Transform prop=position value=5,0,0
""")
```

---

## create_object

Spawn a new GameObject in the scene.

**Parameters:**
- `name` (string) — Name or path for new object (e.g., "Enemy" or "Enemies/Goblin")
- `primitive` (string, optional) — Template type: "Cube", "Sphere", "Capsule", "Cylinder", "Quad", "Plane"
- `parent` (string, optional) — Parent object path
- `prefab_path` (string, optional) — Prefab asset path (instantiate from prefab)
- `components` (string, optional) — Comma-separated components to add
- `scene` (string, optional) — Scene name (for multi-scene projects)

**Example:**

```python
# Create empty object
await create_object(name="NewObject")

# Create with primitive mesh
await create_object(name="Ground", primitive="Plane")

# Create under parent
await create_object(name="Weapon", parent="Player/WeaponSlot")

# Instantiate from prefab
await create_object(name="Enemy1", prefab_path="Assets/Prefabs/Enemy.prefab")

# Create multiple (use batch)
await batch("""
create_object name=Player primitive=Cube
create_object name=Enemy primitive=Cube parent=Enemies
create_object name=Ground primitive=Plane
""")
```

---

## delete_object

Remove a GameObject from the scene.

**Parameters:**
- `id` (int, optional) — Instance ID
- `path` (string, optional) — GameObject path
- `force` (bool, default=false) — Delete non-empty containers

Provide either `id` or `path`.

**Example:**

```python
# Delete single object by path
await delete_object(path="Temp")

# Delete multiple (use batch)
await batch("""
delete_object path=Temp1
delete_object path=Temp2
""")
```

---

## set_active

Enable or disable a GameObject.

**Parameters:**
- `path` (string) — GameObject path
- `active` (bool) — true to enable, false to disable

**Example:**

```python
# Hide UI panel
await set_active("UI/PauseMenu", active=false)

# Show it again
await set_active("UI/PauseMenu", active=true)

# Batch multiple
await batch("""
set_active path=Enemy1 active=false
set_active path=Enemy2 active=false
""")
```

---

## manage_component

Add or remove components from a scene object.

**Parameters:**
- `path` (string) — GameObject path
- `type` (string) — Component type (short name like "Rigidbody" or full namespace like "UnityEngine.UI.Button")
- `action` (string) — "add" | "remove"

**Example:**

```python
# Add component
await manage_component(path="Player", type="Rigidbody", action="add")

# Add custom script
await manage_component(path="Player", type="Health", action="add")

# Remove component
await manage_component(path="Player", type="AudioSource", action="remove")

# Batch multiple
await batch("""
manage_component path=Enemy type=Rigidbody action=add
manage_component path=Enemy type=Health action=add
""")
```

---

## set_parent

Change an object's parent in the hierarchy.

**Parameters:**
- `path` (string) — GameObject to move
- `parent` (string) — New parent path (empty string = unparent)

**Example:**

```python
# Parent to container
await set_parent("Sword", parent="Player/WeaponSlot")

# Unparent (root level)
await set_parent("Player", parent="")
```

---

## wire_event

Connect a button or event to a method.

**Parameters:**
- `path` (string) — Object with the event
- `component` (string) — Component type owning the event field
- `event` (string) — Serialized field name (e.g., "onClick", "_onComplete")
- `target` (string) — Target scene path or asset path
- `method` (string) — Method name (e.g., "SetActive", "Play")
- `arg_type` (string, default="void") — "void" | "bool" | "int" | "float" | "string" | "object"
- `arg_value` (string, optional) — Required when arg_type != void

**Example:**

```python
# Connect button click
await wire_event("UI/StartButton", "Button", "onClick", "GameManager", "StartGame")

# Connect UI event
await wire_event("UI/QuitButton", "Button", "onClick", "GameManager", "QuitGame")
```

---

## unwire_event

Disconnect an event listener.

**Parameters:**
- `path` (string) — Event source
- `component` (string) — Component type owning the event field
- `event` (string) — Serialized field name (e.g., "onClick")
- `index` (int, optional) — Remove specific entry (0-based). Omit to clear all.

**Example:**

```python
# Clear all listeners on onClick
await unwire_event("UI/Button", "Button", "onClick")

# Remove specific listener at index 0
await unwire_event("UI/Button", "Button", "onClick", index=0)
```

---

## set_material

Set object material color.

**Parameters:**
- `path` (string) — Object with Renderer
- `color` (string) — Hex color (e.g., "#FF0000")
- `shader` (string, optional) — Shader name; auto-selects URP/Standard if omitted

**Example:**

```python
# Set color to red
await set_material("Player", color="#FF0000")

# With explicit shader
await set_material("Player", color="#0000FF", shader="Universal Render Pipeline/Lit")
```

---

## find_objects

Search for GameObjects by name, component, tag, or layer (Category: `object`).

**Parameters:**
- `query` (string) — Search term
- `type` (string, optional) — "name" | "component" | "tag" | "layer"
- `recursive` (bool, default=true) — Search entire hierarchy

**Example:**

```python
# Find by name
enemies = await find_objects(query="Enemy")

# Find all with Rigidbody
rigidbodies = await find_objects(query="Rigidbody", type="component")

# Find all with "Collectible" tag
items = await find_objects(query="Collectible", type="tag")
```

---

## get_object_detail

Read full object metadata including components, tags, layers, active state (Category: `object`).

**Parameters:**
- `id` (int) — Instance ID (use `$a`-style IDs from get_hierarchy)
- `full` (bool, default=false) — Bypass distillation, return raw response

**Output:**
```
Name: Player
Active: true
Layer: Default
Tag: Player
Components: [Transform, Rigidbody, Health, PlayerController]
Children: [Body, Head, WeaponSlot]
```

**Example:**

```python
# Get hierarchy first to obtain instance ID
hier = await get_hierarchy()  # → Player $a
detail = await get_object_detail(id=<instance_id>)
```

---

## get_components_list

List all components on an object (Category: `object`).

**Parameters:**
- `id` (int) — Instance ID (use `$a`-style IDs from get_hierarchy)

**Output:**
```
Transform
Rigidbody
PlayerController
Health
AudioSource
```

**Example:**

```python
# Get hierarchy first to obtain instance ID
hier = await get_hierarchy()  # → Player $a
components = await get_components_list(id=<instance_id>)
```

---

## object_diff

Compare two objects' properties (Category: `object`).

**Parameters:**
- `path1` (string) — First object
- `path2` (string) — Second object

**Output:** Diff showing matching/different components and values.

**Example:**

```python
diff = await object_diff("PlayerTemplate", "Player")
# → Transform: MATCH
# → Health: maxHp=100 vs 85
```

---

## set_property_delta

Modify a numeric property by adding/subtracting (Category: `object`).

**Parameters:**
- `path` (string) — GameObject path
- `component` (string) — Component type
- `prop` (string) — Property name
- `delta` (string) — Amount to add as string: `"+10"`, `"-5"`, `"(+1,2,0)"` for vectors

**Example:**

```python
# Add 10 to health
await set_property_delta("Player", "Health", "hp", delta="+10")

# Subtract 5 from score
await set_property_delta("Player", "ScoreManager", "score", delta="-5")
```

---

## transfer_object

Move or copy a GameObject to another loaded scene (Category: `object`).

**Parameters:**
- `path` (string) — Object to move/copy
- `action` (string) — "move" | "copy"
- `target_scene` (string, optional) — Destination scene name (omit = same scene, copy = duplicate)
- `parent` (string, optional) — Target parent path in destination scene
- `world_position_stays` (bool, default=true) — Preserve world transform

**Example:**

```python
# Move Player to AdditiveScene
await transfer_object(path="Player", action="move", target_scene="AdditiveScene")

# Copy Player to same scene
await transfer_object(path="Player", action="copy")
```

---

## Common Patterns

| Task | Tools | Example |
|------|-------|---------|
| Verify object exists | get_hierarchy + search_scene | `hier = await get_hierarchy()` |
| Read object state | get_component + inspect | `health = await get_component("Player", "Health")` |
| Modify multiple objects | batch + set_property | `await batch("set_property path=Player component=Transform prop=position value=0,1,0\nset_property path=Enemy component=Health prop=maxHp value=50")` |
| Create + configure | create_object + set_property | `await create_object("Enemy"); await set_property("Enemy", "Health", "maxHp", "50")` |
| Parent objects | set_parent | `await set_parent("Sword", "Player/Hand")` |
| Add physics | manage_component | `await manage_component(path="Player", type="Rigidbody", action="add")` |
| Wire UI events | wire_event | `await wire_event("UI/Button", "Button", "onClick", "Manager", "OnClick")` |

---

**See also:** [Scene Tools](scene.md) for hierarchy inspection, [Batch](batch.md) for combining operations.
