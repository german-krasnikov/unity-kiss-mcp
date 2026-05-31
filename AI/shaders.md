# Feature: Shader Management (Phase 20)

## Overview
Consolidated `shader` MCP tool with 7 actions for ShaderLab code shaders, Shader Graph, and material property management.

## Architecture (for Architect)
- **ShaderSerializer** — reads shader/material properties via Unity API (`Shader.GetPropertyCount/Name/Type`)
- **ShaderHelper** — creates .shader files from presets (unlit/lit/transparent) or custom code, sets material properties with Undo
- **ShaderGraphHelper** — reads/creates/edits .shadergraph files; uses Unity API for compiled info, raw file parsing for graph structure (no public API exists)
- **CommandRouter** — routes `shader` command to appropriate helper based on `action` field
- All 7 actions consolidated into single MCP tool with 18 parameters

## Implementation Notes (for Developer)
- **Shader Graph has NO public C# API** for graph structure — must parse MultiJson format directly
- **MultiJson format**: multiple JSON blocks separated by `\n\n`, each with `m_Type`, `m_ObjectId`, `m_SGVersion`
- **GraphData root block**: contains `m_Nodes`, `m_Edges`, `m_Properties` arrays referencing other blocks by ID
- `Create` for .shadergraph copies from Unity's package templates (`Library/PackageCache/com.unity.shadergraph*`)
- `AssetDatabase.ImportAsset` wrapped in try/catch for node/edge edits (incomplete slot blocks cause warnings)
- Material property types: Color (`#RRGGBB`), Float, Range, Vector (`(x,y,z,w)`), Texture (asset path), Int
- ShaderLab presets use string concatenation (NOT verbatim `@""` strings — `{{` stays literal there)
- **JSON unescape** (Phase 20f): Custom shader code in `create` action requires JSON unescape (`\"` → `"`, `\\` → `\`, `\n` → newline, etc.) via new `UnescapeJsonString` method in CommandRouter
- **RemoveNode multi-line** (Phase 20f): ShaderGraphHelper.RemoveNode now uses `ParseJsonObjects` to handle multi-line JSON format in .shadergraph files instead of single-line string.Replace

### Actions
| Action | Helper | Description |
|--------|--------|-------------|
| get | ShaderSerializer | Read shader properties (from scene object or asset path) |
| create | ShaderHelper | Create .shader from preset or custom code |
| set | ShaderHelper | Set material property or keyword on scene object |
| graph_get | ShaderGraphHelper | Read .shadergraph structure (nodes, edges, properties) |
| graph_create | ShaderGraphHelper | Create .shadergraph from Unity template |
| graph_node | ShaderGraphHelper | Add/remove node in .shadergraph |
| graph_edge | ShaderGraphHelper | Add/remove edge in .shadergraph |

## Code Locations
- Python: `server/src/unity_mcp/tools/advanced.py` (shader tool)
- C#: `unity-plugin/Editor/ShaderSerializer.cs`, `ShaderHelper.cs`, `ShaderGraphHelper.cs` (331 lines)
- Router: `unity-plugin/Editor/CommandRouter.cs` (1053 lines, ExecShaderConsolidated + UnescapeJsonString)
- Tests: `server/tests/test_server_shader.py` (22), `unity-test-project/Assets/Tests/Editor/MCPShaderTests.cs` (41 tests incl. 2 regression)

## Review Checklist (for Reviewer)
- [ ] Security: no arbitrary file writes outside Assets/
- [ ] Performance: ShaderGraph parsing is O(n) on file size
- [ ] Token efficiency: text output format, no JSON overhead
- [ ] Edge cases: URP vs Standard pipeline, missing Shader Graph package, invalid presets

## Related
- Skill: `.claude/skills/csharp-unity.md`
- Knowledge: `AI/architecture.md`
