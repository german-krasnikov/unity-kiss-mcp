# MCP Tools Reference (99 total)

All tools organized by category. CORE tools are always visible. Themed categories enable groups via `enable_category()`. Plugin tools discovered dynamically.

## CORE Tools (Always Visible)

Essential read/scene, meta, connection, repair.

| Tool | Purpose | Key Params | Category |
|------|---------|------------|----------|
| get_hierarchy | Read scene tree | summary, depth | scene read |
| get_component | Read component values | path, component | scene read |
| inspect | Batch-read N objects' components | query, filter | scene read |
| set_property | Write component value | path, component, prop, value | scene write |
| create_object | Spawn GameObject | name, parent, components | scene write |
| delete_object | Remove GameObject | path | scene write |
| manage_component | Add/remove components | path, action, component | scene write |
| batch | Run 2+ ops atomically | commands (JSON array) | orchestration |
| scene | List/load/save scenes | action, name | scene meta |
| search_scene | Find objects by pattern | query, type | scene read |
| set_parent | Reparent GameObject | path, parent | scene write |
| get_console | Read Editor.log tail | lines, severity | diagnostics |
| get_compile_errors | List C# compile errors | — | diagnostics |
| get_enabled_tools | List visible tools | — | meta |
| discover_tools | Get tool schemas | filter | meta |
| editor | Open Editor windows | window_type, path, action | editor control |
| do | Execute arbitrary code | prompt, context, action | code generation |
| ask | Query LLM about scene | query, context | LLM read |
| ask_user | Prompt human | question, options | human interaction |
| permission_prompt | Gate sensitive ops | operation, details | security |
| reconnect_unity | Reconnect TCP socket | port (auto-discover) | connection |
| list_connections | Show connection status | — | connection |
| resolve_tool_schema | Deferred schema fetch | tool_name | meta |
| doctor | Health diagnostics | fix (auto-fix stale PIDs) | diagnostics |

## SCENE_EDIT (8 tools)

Scene object manipulation: find, detail, components, active, material, delta, diff, transfer.

| Tool | Purpose | Key Params |
|------|---------|------------|
| find_objects | Search objects | query, type |
| get_object_detail | Get object state | path |
| get_components_list | List components on object | path |
| set_active | Toggle active flag | path, active |
| set_material | Assign material to object | path, material_path, slot |
| set_property_delta | Relative property change | path, component, prop, delta |
| object_diff | Compare two objects | path1, path2 |
| transfer_object | Move object between scenes | path, target_scene |

## COMPONENTS (2 tools)

Component event wiring: connect/disconnect event listeners.

| Tool | Purpose | Key Params |
|------|---------|------------|
| wire_event | Connect event to method | path, component, event, target_path, target_method |
| unwire_event | Disconnect event listener | path, component, event, target_path |

## ANIMATION (4 tools)

Animation playback, timeline, animator state.

| Tool | Purpose | Key Params |
|------|---------|------------|
| animation | Play clip on Animator/Anim | path, clip_name, speed, loop |
| timeline | Control Timeline | path, action (play/pause/stop), time |
| animator | Get/set Animator parameters | path, param_name, param_type, value |
| particle | Emit/stop particles | path, action (play/stop/clear), count |

## SHADERS_MATERIAL (3 tools)

Shader, material, and reference operations.

| Tool | Purpose | Key Params |
|------|---------|------------|
| shader | Find shader, list properties | name, action (find/get_props) |
| material | Assign/inspect material | path, material_path, slot |
| references | Find asset references | asset_path, include_indirect |

## VFX (1 tool)

Visual effects intent — AI-driven VFX tweaks.

| Tool | Purpose | Key Params |
|------|---------|------------|
| vfx_intent | AI vfx description → settings | path, description, context |

## UI (5 tools)

UI creation, rect layout, validation, spatial context.

| Tool | Purpose | Key Params |
|------|---------|------------|
| create_ui | Spawn UI elements | type, name, parent, rect |
| set_rect | Modify RectTransform | path, anchor, offset, size |
| validate_layout | Check UI constraints | path, fix (bool) |
| get_spatial_context | Proximity query | path, radius, layer_mask |
| ui_intent | AI ui description → components | parent, description, context |

## SCREENSHOTS (3 tools)

Visual capture, baseline diff, regression detection.

| Tool | Purpose | Key Params |
|------|---------|------------|
| screenshot | Capture frame | width, height, camera, path (output) |
| screenshot_baseline | Save baseline for regression | name, width, height, camera |
| screenshot_compare | Diff baseline ↔ current | name, mode (auto/pixel/structural/targeted), question |

## UNIT_TESTS (5 tools)

Test execution, playtest DSL runner, fuzz testing.

| Tool | Purpose | Key Params |
|------|---------|------------|
| run_tests | Execute NUnit tests | mode (EditMode/PlayMode), filter |
| get_test_results | Poll test status | — |
| run_playtest | Run playtest DSL script | script (21-step DSL), config |
| fuzz_playtest | Random input fuzzing | count, duration, seed |
| test_step | Execute single DSL step | step (JSON), config |

## RUNTIME (5 tools)

Runtime-only (Play Mode): invoke methods, set properties, wait conditions, movement.

| Tool | Purpose | Key Params |
|------|---------|------------|
| invoke_method | Call method at runtime | path, component, method, args (JSON) |
| set_runtime_property | Set field/property at runtime | path, component, prop, value |
| wait_until | Busy-wait on condition | query, op, value, timeout |
| move_to | Pathfind + walk to position | path, dest_pos, speed, timeout |
| query_state | Read runtime GameObject state | path, queries (CSV) |

## ASSETS (4 tools)

Asset database: import/export, prefab, ScriptableObject, project settings.

| Tool | Purpose | Key Params |
|------|---------|------------|
| asset | Asset DB operations | action (find/get_info/create/move/duplicate/delete/import/export), path, type, name |
| prefab | Prefab lifecycle | action (save/create_variant/apply/revert), path, asset_path |
| scriptable_object | ScriptableObject create/read/write | action, type, path, values |
| project_settings | Project config | action (get/set), target (tags/layers/quality), prop, value |

## ADVANCED_CODE (14 tools)

Code generation, refactoring, compilation, static analysis.

| Tool | Purpose | Key Params |
|------|---------|------------|
| execute_code | Run C# in Editor | code (C# method body), undo_label |
| recompile | Force script compilation | — |
| sync_unity | Reload and restart | reason, wait (bool) |
| find_references | Locate usages of symbol | symbol_name, include_tests |
| semantic_at | Language server: definition/hover | path, line, col, action |
| compile_preflight | Check compile readiness | fix (bool) |
| await_compile | Block until compile done | timeout |
| get_schema | Inspect class/type schema | type_name, include_bases |
| auto_fix | Apply code fix suggestion | file_path, fix_id |
| smart_build | Rebuild affected assemblies | affected_paths |
| checkpoint | Save named revision | name, description |
| validate_references | Check all refs valid | fix (bool) |
| menu | Execute Editor menu item | menu_path |
| diagnose | Deep troubleshooting | system (compile/tcp/memory/reload) |

## SESSION_SKILLS (11 tools)

Persistent reusable skills, templates, session snapshots, change tracking.

| Tool | Purpose | Key Params |
|------|---------|------------|
| save_skill | Store reusable C# or batch | name, description, code (auto-detects kind) |
| use_skill | Execute saved skill | name, params (key=value CSV) |
| list_skills | Show all skills + usage | — |
| save_template | Store scene template | name, description, template_code |
| apply_template | Instantiate template | name, params (key=value CSV) |
| list_templates | Show all templates | — |
| fingerprint | Hash scene state | — |
| scene_diff | Compare two fingerprints | fp1, fp2 |
| get_changes | Log editor events since last call | clear (bool) |
| save_session | Snapshot hierarchy to .claude/session-context.json | — |
| load_session | Load + diff previous session | — |

## META (9 tools)

Batch setup, batch property set, metrics, scene scanning, collider checks, spatial queries, config.

| Tool | Purpose | Key Params |
|------|---------|------------|
| animator_intent | AI animator description → setup | path, description, context |
| get_metrics | Scene stats (mesh count, draw calls, etc) | filter |
| setup_objects | Batch create + wire objects | spec (JSON template array) |
| set_properties | Batch set properties | objects_and_values (JSON) |
| configure_objects | Batch configure components | objects_and_config (JSON) |
| scan_scene | Audit for issues | checks (CSV: refs/colliders/physics/null_components) |
| check_colliders | Collision layer conflicts | fix (bool) |
| spatial_query | Radial/box search + filter | origin, radius, layer_mask, type_filter |
| set_llm_config | Store LLM settings | param, value |

---

**See also:** AI/architecture.md (design), AI/mcp-server.md (protocol), .claude/skills/ (token patterns).
