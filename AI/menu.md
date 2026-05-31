# Menu Execution Feature (Phase 25)

## Overview
MCP tool for executing and listing Unity Editor menu items (MenuItem).

## Architecture

### Python (server.py)
```python
@mcp.tool(annotations=_RW)
async def menu(action: str, path: str | None = None) -> str
```

### C# (MenuHelper.cs)
- `Execute(path)` — validates existence + enabled → `EditorApplication.ExecuteMenuItem`
- `List(path)` — reflection `Menu.ExtractSubmenus` → text list
- `ListRoots()` — enumerates File/Edit/Assets/GameObject/Component/Window/Help/Tools

### CommandRouter
- `case "menu"` → `ExecMenu(args)` with action switch
- Added to `IsMutatingCommand` (execute can modify scene)

## API

### Actions
| Action | Args | Description |
|--------|------|-------------|
| `execute` | `path` (required) | Run menu item by full path |
| `list` | `path` (optional) | List sub-items; omit for all roots |

### Examples
```
menu action=execute path="GameObject/3D Object/Cube"
menu action=execute path="Window/General/Console"
menu action=list path="GameObject"
menu action=list  # lists all root menus
```

## Known Limitations
- `Edit/` menu items not supported by Unity API (long-standing bug since 2011)
- Menu items with validation functions may fail if context requirements not met
- `execute` may open dialogs (Build Settings, Project Settings, etc.)

## Unity Internal API (via reflection)
- `Menu.ExtractSubmenus(path)` — returns `string[]` of sub-items
- `Menu.MenuItemExists(path)` — returns `bool`
- `Menu.GetEnabled(path)` — public API, checks if item is enabled

## Tests
- Python: `server/tests/test_server_menu.py` (8 tests)
- C#: `unity-test-project/Assets/Tests/Editor/MCPMenuTests.cs` (10 tests)

## Senior Developer Notes
- Reflection cached in static constructor for performance
- Graceful fallback when internal APIs unavailable
- `Debug.LogWarning` on startup if reflection fails
