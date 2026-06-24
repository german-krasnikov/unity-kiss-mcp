# Shader & Material Tools

Inspect, create, and modify shader assets, materials, and Shader Graph networks. Use these tools for shader development, material configuration, and visual effects.

## shader

Read or write shader assets (.shader / .shadergraph). Inspect shader properties and keywords, create new shaders from presets or raw HLSL, or edit Shader Graph node networks.

**Parameters:**
- `action` (string) — "get" | "create" | "set" | "graph_get" | "graph_create" | "graph_node" | "graph_edge"
- `path` (string) — Shader asset path (Assets/...)
- `target` (string, optional) — Shader compilation target
- `preset` (string, optional) — Shader preset: "unlit" | "lit" | "transparent"
- `code` (string, optional) — Raw HLSL shader code (used with create)
- `shader_name` (string, optional) — Shader name identifier
- `prop` (string, optional) — Property name (for set action)
- `value` (string, optional) — Property value
- `keyword` (string, optional) — Shader keyword name
- `enabled` (string, optional) — Keyword enabled state
- `node_type` (string, optional) — Shader Graph node type
- `node_id` (string, optional) — Shader Graph node ID
- `node_action` (string, optional) — Node action: "add" | "remove" | "configure"
- `output_node` (string, optional) — Output node ID (for edge)
- `output_slot` (int, optional) — Output slot index (for edge)
- `input_node` (string, optional) — Input node ID (for edge)
- `input_slot` (int, optional) — Input slot index (for edge)
- `edge_action` (string, optional) — Edge action: "connect" | "disconnect"

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| get | Inspect shader properties and keywords | path | `shader("get", path="Assets/Shaders/MyShader.shader")` |
| create | New shader from preset or code | path, preset OR code | `shader("create", path="Assets/Shaders/Custom.shader", preset="lit")` |
| set | Change property or keyword | path, (prop+value) OR (keyword+enabled) | `shader("set", path="Assets/Shaders/MyShader.shader", prop="_Color", value="#FF0000")` |
| graph_get | Read Shader Graph nodes/edges | path | `shader("graph_get", path="Assets/Shaders/MyGraph.shadergraph")` |
| graph_create | New .shadergraph | path | `shader("graph_create", path="Assets/Shaders/NewGraph.shadergraph")` |
| graph_node | Add/remove/configure node | path, node_type, node_id, node_action | `shader("graph_node", path="Assets/Shaders/MyGraph.shadergraph", node_type="ColorNode", node_id="node_1", node_action="add")` |
| graph_edge | Connect/disconnect slots | path, output_node, output_slot, input_node, input_slot, edge_action | `shader("graph_edge", path="Assets/Shaders/MyGraph.shadergraph", output_node="node_1", output_slot=0, input_node="node_2", input_slot=0, edge_action="connect")` |

**Example:**

```python
# Inspect shader
info = await shader("get", path="Assets/Shaders/Standard.shader")

# Create new shader from preset
await shader("create", path="Assets/Shaders/MyUnlit.shader", preset="unlit")

# Modify shader property
await shader("set", path="Assets/Shaders/MyShader.shader", 
            prop="_MainColor", value="#FF5500")

# Enable keyword
await shader("set", path="Assets/Shaders/MyShader.shader",
            keyword="USE_NORMALMAP", enabled="true")

# Create Shader Graph
await shader("graph_create", path="Assets/Shaders/MyGraph.shadergraph")

# Add node
await shader("graph_node", path="Assets/Shaders/MyGraph.shadergraph",
            node_type="ColorNode", node_id="node_1", node_action="add")

# Connect nodes
await shader("graph_edge", path="Assets/Shaders/MyGraph.shadergraph",
            output_node="node_1", output_slot=0,
            input_node="node_2", input_slot=0,
            edge_action="connect")
```

**Use Cases:**
- Inspect built-in shader properties and keywords
- Create custom shaders without manual file editing
- Build Shader Graph networks visually
- Modify material shader assignments via `material` tool

**Note:** For material shader assignment (applying a shader to a scene material), use `material` tool instead.

---

## material

Create and configure materials. Assign shaders, set properties, and copy materials across scene objects.

**Parameters:**
- `action` (string) — "create" | "get" | "set" | "copy" | "list_properties"
- `path` (string, optional) — Material asset path (Assets/...) for asset-level operations
- `object_path` (string, optional) — Scene object path for scene-level operations
- `shader` (string, optional) — Shader name (used with create or set)
- `prop` (string, optional) — Property name to read/write
- `value` (string, optional) — Property value
- `source` (string, optional) — Source material to copy from
- `targets` (string, optional) — Comma-separated scene paths to paste to

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| create | New material asset | path, shader | `material("create", path="Assets/Materials/NewMat.mat", shader="Standard")` |
| get | Read material properties | path OR object_path | `material("get", path="Assets/Materials/Player.mat")` |
| set | Change property on material | path OR object_path, prop, value | `material("set", path="Assets/Materials/Player.mat", prop="_Color", value="#FF0000")` |
| copy | Duplicate material to targets | source, targets | `material("copy", source="Assets/Materials/Base.mat", targets="Player,Enemy,NPC")` |
| list_properties | Show all properties | path OR object_path | `material("list_properties", path="Assets/Materials/Player.mat")` |

**Object Path vs Asset Path:**
- `path`: Asset in project (Assets/Materials/Player.mat) — modifies the asset
- `object_path`: Scene object (/Player/Mesh) — modifies the material instance on that object

**Example:**

```python
# Create new material with Standard shader
await material("create", path="Assets/Materials/Enemy.mat", shader="Standard")

# Set color on asset material
await material("set", path="Assets/Materials/Enemy.mat",
              prop="_Color", value="#FF3333")

# Get material properties
props = await material("get", path="Assets/Materials/Enemy.mat")

# List all properties
await material("list_properties", path="Assets/Materials/Enemy.mat")

# Copy material to multiple scene objects
await material("copy", source="Assets/Materials/Base.mat",
              targets="Enemy1,Enemy2,Enemy3")

# Set property on scene object's material directly
await material("set", object_path="Player/Mesh",
              prop="_MainColor", value="#00FF00")
```

**Typical Workflow:**
1. Use `shader("create", ...)` or `shader("get", ...)` to work with shaders
2. Use `material("create", ..., shader=...)` to create materials with specific shader
3. Use `material("set", ...)` to configure material properties
4. Use `material("copy", ...)` to apply material across multiple objects

---

## Common Patterns

| Task | Tools | Example |
|------|-------|---------|
| Inspect shader properties | shader("get", path) | `await shader("get", path="Assets/Shaders/Standard.shader")` |
| Create custom shader | shader("create", path, preset) | `await shader("create", path="Assets/Shaders/Custom.shader", preset="unlit")` |
| Create material with shader | material("create", path, shader) | `await material("create", path="Assets/Materials/New.mat", shader="Standard")` |
| Change material color | material("set", path, prop, value) | `await material("set", path="Assets/Materials/Player.mat", prop="_Color", value="#FF0000")` |
| Apply material to scene object | material("copy", source, targets) | `await material("copy", source="Assets/Materials/Base.mat", targets="Player")` |
| Build Shader Graph | shader("graph_create") → shader("graph_node") → shader("graph_edge") | Sequential node/edge operations |

---

**See also:** [Assets Tools](assets.md) for material asset management, [Objects Tools](objects.md) for `set_material` quick helper.
