# Asset Tools

Manage prefabs, materials, ScriptableObjects, and project settings. Control the asset pipeline without leaving chat.

## asset

Core asset database operations: search, copy, move, delete, import/export.

**Parameters:**
- `action` (string) — "find" | "get_info" | "create" | "move" | "validate_move" | "duplicate" | "delete" | "get_dependencies" | "import_settings" | "export_package" | "import_package"
- `path` (string) — Asset path (Assets-relative)
- `type` (string, optional) — Asset type filter
- `name` (string, optional) — Name for search
- `folder` (string, optional) — Folder scope for search
- `source`, `dest` (string, optional) — For move operations
- `recursive` (bool, default=false) — Include subfolders
- `output` (string, optional) — Export destination
- `include_deps` (bool, default=true) — Include dependencies

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| find | Search assets by name/type/labels | name OR type, folder (opt) | `asset("find", name="PlayerMesh", folder="Assets/Meshes")` |
| get_info | Read asset metadata | path | `asset("get_info", path="Assets/Models/Player.fbx")` |
| create | Create new asset | type, path | `asset("create", type="Folder", path="Assets/NewFolder")` |
| move | Relocate asset + .meta | source, dest | `asset("move", source="Assets/Old/X.mat", dest="Assets/Materials/X.mat")` |
| validate_move | Test move without executing | source, dest | `asset("validate_move", source="Assets/A.cs", dest="Assets/B.cs")` |
| duplicate | Copy asset | path | `asset("duplicate", path="Assets/Material.mat")` |
| delete | Remove asset + .meta | path | `asset("delete", path="Assets/Temp.prefab")` |
| get_dependencies | List asset dependencies | path | `asset("get_dependencies", path="Assets/Scene.unity", include_deps=true)` |
| import_settings | Configure import params | path, prop, value | `asset("import_settings", path="Assets/Mesh.fbx", prop="importer_type", value="humanoid")` |
| export_package | Create .unitypackage | path, output | `asset("export_package", path="Assets/MyFeature", output="/tmp/export.unitypackage")` |
| import_package | Load .unitypackage | path (file system) | `asset("import_package", path="/tmp/export.unitypackage")` |

**Example:**

```python
# Find materials in folder
mats = await asset("find", type="Material", folder="Assets/UI", labels="hud,animated")

# Get asset info
info = await asset("get_info", path="Assets/Models/Player.fbx")

# Create new folder
await asset("create", type="Folder", path="Assets/Materials")

# Move asset
await asset("move", source="Assets/Old/Player.mat", dest="Assets/Materials/Player.mat")

# Delete temp file
await asset("delete", path="Assets/Temp.prefab")

# Get dependencies
deps = await asset("get_dependencies", path="Assets/Scenes/Level1.unity", include_deps=true)

# Export package
await asset("export_package", path="Assets/MyFeature", output="/tmp/feature.unitypackage", include_deps=true)

# Import package
await asset("import_package", path="/tmp/feature.unitypackage")
```

---

## material

Manage materials and shaders. Create, modify, and assign materials to objects.

**Parameters:**
- `action` (string) — "create" | "get" | "set" | "copy" | "list_properties"
- `path` (string, optional) — Material asset path
- `object_path` (string, optional) — Scene object path
- `shader` (string, optional) — Shader name (e.g., "Standard", "Unlit/Color")
- `prop` (string) — Property name (e.g., "_Color", "_MainTexture")
- `value` (string) — Property value
- `source` (string, optional) — Source material for copy
- `targets` (string, optional) — Comma-separated scene paths for apply

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| create | Create material with shader | path, shader | `material("create", path="Assets/NewMat.mat", shader="Standard")` |
| get | Read material properties | path OR object_path | `material("get", path="Assets/PlayerMat.mat")` |
| set | Modify material property | path OR object_path, prop, value | `material("set", path="Assets/Mat.mat", prop="_Color", value="1,0,0,1")` |
| copy | Clone + assign to objects | source, targets | `material("copy", source="Assets/Base.mat", targets="Player,Enemy")` |
| list_properties | Enumerate all properties | path OR object_path | `material("list_properties", path="Assets/Mat.mat")` |

**Color Format:** RGB or hex
- Vector: `"0.5,0.2,1,1"` (RGBA)
- Hex: `"#FF0000"` (red)
- Shorthand: `"red"` (standard colors)

**Example:**

```python
# Create material
await material("create", path="Assets/RedMat.mat", shader="Standard")

# Read properties
props = await material("get", path="Assets/RedMat.mat")

# Set color
await material("set", path="Assets/RedMat.mat", prop="_Color", value="#FF0000")

# Set texture
await material("set", path="Assets/Mat.mat", prop="_MainTexture", value="Assets/Textures/Wood.png")

# Copy material to scene objects
await material("copy", source="Assets/BaseMat.mat", targets="Player,Enemy,Boss")

# List all properties
props = await material("list_properties", path="Assets/Mat.mat")

# Modify material on scene object
await material("set", object_path="Player", prop="_Metallic", value="0.8")
```

---

## prefab

Create, modify, and manage prefabs. Save instances as prefabs or edit prefabs directly (v0.56.0+).

**Parameters:**
- `action` (string) — "save" | "create_variant" | "apply" | "revert" | "get_overrides" | "unpack" | "edit"
- `path` (string, optional) — Scene instance path
- `asset_path` (string, optional) — Prefab asset path (Assets-relative)
- `base_path` (string, optional) — Base prefab for variant
- `variant_path` (string, optional) — New variant path
- `component` (string, optional) — Component for edit action
- `add_component`, `remove_component` (string, optional) — For component management
- `prop`, `value` (string, optional) — Property to modify
- `recursive` (bool, default=false) — Apply/revert recursively

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| save | Scene instance → prefab asset | path, asset_path | `prefab("save", path="Player", asset_path="Assets/Prefabs/Player.prefab")` |
| create_variant | Make variant from base | base_path, variant_path | `prefab("create_variant", base_path="Assets/Enemy.prefab", variant_path="Assets/Variants/EnemyFast.prefab")` |
| apply | Push instance changes → base | path | `prefab("apply", path="Player")` |
| revert | Discard instance changes | path | `prefab("revert", path="Player")` |
| get_overrides | List property modifications | path | `prefab("get_overrides", path="Enemy")` |
| unpack | Convert instance → GameObject | path | `prefab("unpack", path="SpawnedPrefab")` |
| edit | Modify prefab asset directly | asset_path, component, prop, value | `prefab("edit", asset_path="Assets/Player.prefab", component="Health", prop="MaxHP", value="200")` |
| edit (add) | Add component to prefab | asset_path, add_component | `prefab("edit", asset_path="Assets/Player.prefab", add_component="Rigidbody")` |
| edit (remove) | Remove component | asset_path, remove_component | `prefab("edit", asset_path="Assets/Player.prefab", remove_component="AudioSource")` |

**Workflow: Two-step prefab creation**

```python
# 1. Design in scene
await create_object("Player")
await manage_component("Player", "add", "Health")
await set_property("Player", "Health", "maxHp", "100")

# 2. Save as prefab
await prefab("save", path="Player", asset_path="Assets/Prefabs/Player.prefab")
```

**Workflow: Direct prefab editing (v0.56.0+)**

```python
# Modify prefab asset without unpacking
await prefab("edit", 
  asset_path="Assets/Prefabs/Player.prefab",
  component="Health",
  prop="MaxHP",
  value="200"
)

# Add component to prefab
await prefab("edit",
  asset_path="Assets/Prefabs/Player.prefab",
  add_component="Rigidbody"
)

# Remove component
await prefab("edit",
  asset_path="Assets/Prefabs/Player.prefab",
  remove_component="AudioSource"
)
```

**Workflow: Variant management**

```python
# Create variant (inherits from base)
await prefab("create_variant",
  base_path="Assets/Prefabs/Enemy.prefab",
  variant_path="Assets/Prefabs/Variants/EnemyFast.prefab"
)

# Modify variant (doesn't affect base)
await prefab("edit",
  asset_path="Assets/Prefabs/Variants/EnemyFast.prefab",
  component="Health",
  prop="maxHp",
  value="50"
)
```

---

## scriptable_object

Manage ScriptableObject assets. Create, modify, and save ScriptableObject configurations.

**Parameters:**
- `action` (string) — "create" | "get" | "set" | "save"
- `path` (string) — Asset path or scene instance
- `type` (string, optional) — ScriptableObject class name
- `prop` (string, optional) — Property name
- `value` (string, optional) — Property value

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| create | Create new ScriptableObject | path, type | `scriptable_object("create", path="Assets/GameConfig.asset", type="GameSettings")` |
| get | Read ScriptableObject | path | `scriptable_object("get", path="Assets/GameConfig.asset")` |
| set | Modify property | path, prop, value | `scriptable_object("set", path="Assets/GameConfig.asset", prop="maxLevel", value="50")` |
| save | Save to asset | path | `scriptable_object("save", path="Assets/GameConfig.asset")` |

**Example:**

```python
# Create new ScriptableObject
await scriptable_object("create", 
  path="Assets/GameConfig.asset",
  type="GameSettings"
)

# Read configuration
config = await scriptable_object("get", path="Assets/GameConfig.asset")

# Modify property
await scriptable_object("set",
  path="Assets/GameConfig.asset",
  prop="maxLevel",
  value="100"
)

# Save changes
await scriptable_object("save", path="Assets/GameConfig.asset")
```

---

## project_settings

Access and modify project-wide settings.

**Parameters:**
- `action` (string) — "get" | "set"
- `key` (string) — Setting key (e.g., "Physics/DefaultSolverIterations")
- `value` (string, optional) — New value

**Actions:**

| Action | Purpose | Example |
|--------|---------|---------|
| get | Read project setting | `project_settings("get", key="Physics/GravityScale")` |
| set | Modify setting | `project_settings("set", key="Physics/GravityScale", value="9.81")` |

**Example:**

```python
# Read gravity
gravity = await project_settings("get", key="Physics/GravityScale")

# Set gravity
await project_settings("set", key="Physics/GravityScale", value="15.0")

# Read frame rate
fps = await project_settings("get", key="Time/FixedTimestep")
```

---

## shader

Look up and configure shaders (Category: `asset`).

**Parameters:**
- `action` (string) — "find" | "get_properties"
- `name` (string) — Shader name
- `material_path` (string, optional) — Material using shader

**Example:**

```python
# Find shader
await shader("find", name="Standard")

# Get properties
props = await shader("get_properties", name="Standard")
```

---

## references

Find all asset references. Track dependencies and usage (Category: `asset`).

**Parameters:**
- `path` (string) — Asset path to analyze
- `include_indirect` (bool, default=true) — Include transitive dependencies

**Example:**

```python
# What depends on this prefab?
refs = await references(path="Assets/Prefabs/Player.prefab")

# Direct dependencies only
deps = await references(path="Assets/Scenes/Level1.unity", include_indirect=false)
```

---

## Common Patterns

| Task | Tools | Example |
|------|-------|---------|
| Create material + assign | material("create") + material("copy") | `await material("create", path="Assets/New.mat", shader="Standard"); await material("copy", source="Assets/New.mat", targets="Player")` |
| Save scene instance as prefab | prefab("save") | `await prefab("save", path="Player", asset_path="Assets/Prefabs/Player.prefab")` |
| Edit prefab without unpacking | prefab("edit") | `await prefab("edit", asset_path="Assets/Prefabs/Player.prefab", component="Health", prop="maxHp", value="200")` |
| Create variant | prefab("create_variant") | `await prefab("create_variant", base_path="Assets/Enemy.prefab", variant_path="Assets/Variants/EnemyFast.prefab")` |
| Organize assets | asset("move") + asset("create") | `await asset("create", type="Folder", path="Assets/Materials"); await asset("move", source="Assets/Old.mat", dest="Assets/Materials/Old.mat")` |
| Export for sharing | asset("export_package") | `await asset("export_package", path="Assets/MyFeature", output="/tmp/export.unitypackage")` |

---

**See also:** [Batch](batch.md) for combining asset operations, [Objects](objects.md) for scene-instance material assignment.
