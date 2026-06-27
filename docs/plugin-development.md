# Plugin Development Guide

Create custom MCP plugins to extend the Unity editor with domain-specific commands, tools, and settings UI.

## What is an MCP Plugin?

An MCP plugin is a C# class that implements `IMCPPlugin` interface, allowing you to:
- Register custom commands and tools callable from Claude or other AI backends
- Provide a settings panel in the MCP Hub for user configuration
- Hook into domain reload lifecycle events
- Isolate plugin settings from core MCP settings

Plugins are ideal for domain-specific workflows (asset management, shader optimization, animation rigging, etc.) without modifying the core MCP system.

## Quick Start

Create a minimal plugin in 15 lines:

```csharp
using UnityEditor;
using UnityMCP.Editor;

public class MyPlugin : IMCPPlugin
{
    public string Name => "My Plugin";
    public string CommandPrefix => "my_plugin";
    
    public void RegisterCommands()
    {
        CommandRegistry.Register(CommandPrefix, ctx =>
            ctx.Send(new { ok = true, message = "Hello from My Plugin!" }));
    }
    
    public void OnDomainReload() { }
}

// Auto-register on startup
[InitializeOnLoadMethod]
private static void AutoRegisterPlugin()
{
    PluginRegistry.Register(new MyPlugin());
}
```

Register via `[InitializeOnLoadMethod]` (runs once on editor startup) or manually call `PluginRegistry.Register(instance)`.

## IMCPPlugin Interface

All plugins implement this interface:

```csharp
public interface IMCPPlugin
{
    // Required
    string Name { get; }
    string CommandPrefix { get; }
    void RegisterCommands();
    void OnDomainReload();
    
    // Optional (Default Interface Members — DIMs)
    IReadOnlyList<string> AdditionalCommands => Array.Empty<string>();
    string GetToolSubcategory(string command) => null;
    VisualElement BuildSettingsUI() => null;
    bool HasSettingsUI => false;
    string Description => "";
}
```

### Required Members

| Member | Type | Purpose |
|--------|------|---------|
| `Name` | `string` | Unique plugin identifier, shown in Plugins hub card. Used as namespace for PluginConfig. |
| `CommandPrefix` | `string` | Command prefix; tools named `{prefix}` or `{prefix}_action` auto-grouped under this plugin. |
| `RegisterCommands()` | `void` | Called once at startup. Register all your tools here via `CommandRegistry.Register()`. |
| `OnDomainReload()` | `void` | Called when Unity recompiles (domain reload). Reset caches, reconnect sockets, etc. Can be empty. |

### Optional Members (DIMs)

| Member | Type | Default | Purpose |
|--------|------|---------|---------|
| `AdditionalCommands` | `IReadOnlyList<string>` | `Array.Empty<string>()` | Tools NOT matching prefix pattern (e.g., `"my_special_tool"`). Allows non-prefixed tools to belong to this plugin. |
| `GetToolSubcategory(cmd)` | `string` | `null` | Return subcategory label for a command, or null to fall back to plugin name. Used for tool grouping. |
| `BuildSettingsUI()` | `VisualElement` | `null` | Build per-plugin settings panel for the Plugins hub. Return null if no UI. |
| `HasSettingsUI` | `bool` | `false` | Set to `true` only if `BuildSettingsUI()` returns non-null. Gating for Plugins card display. |
| `Description` | `string` | `""` | Short description (1-2 sentences) shown on plugin card in Plugins settings hub. |

## Registering Commands

In `RegisterCommands()`, use `CommandRegistry.Register()` to add tools:

```csharp
public void RegisterCommands()
{
    // Prefixed tool: my_plugin or my_plugin_action
    CommandRegistry.Register($"{CommandPrefix}_action", ActionHandler);
    
    // Non-prefixed tool (use AdditionalCommands)
    CommandRegistry.Register("special_tool", SpecialHandler);
}

private static void ActionHandler(CommandContext ctx)
{
    var param = ctx.GetParameter<string>("param_name");
    ctx.Send(new { ok = true, result = param });
}

private static void SpecialHandler(CommandContext ctx)
{
    ctx.Send(new { ok = true });
}

public IReadOnlyList<string> AdditionalCommands 
    => new[] { "special_tool" };
```

**Tool Naming Patterns:**
- `{CommandPrefix}` — single-word tool, auto-matched
- `{CommandPrefix}_action` — multi-word tools, auto-matched (underscores allowed)
- Non-prefixed tools MUST be listed in `AdditionalCommands` to belong to this plugin

## Plugin Configuration Storage (PluginConfig API)

Store plugin settings isolated from core MCP. Data lives in EditorPrefs with auto-namespacing.

### API Overview

```csharp
// String storage (text, paths, etc.)
string value = PluginConfig.GetString(pluginId, key, defaultValue: "");
PluginConfig.SetString(pluginId, key, value);

// Boolean storage
bool value = PluginConfig.GetBool(pluginId, key, defaultValue: false);
PluginConfig.SetBool(pluginId, key, value);

// Integer storage
int value = PluginConfig.GetInt(pluginId, key, defaultValue: 0);
PluginConfig.SetInt(pluginId, key, value);

// Float storage
float value = PluginConfig.GetFloat(pluginId, key, defaultValue: 0f);
PluginConfig.SetFloat(pluginId, key, value);

// Delete
PluginConfig.Delete(pluginId, key);
```

### How It Works

- **Namespacing:** Keys are auto-namespaced as `MCPPlugin_{pluginId}_{key}` in EditorPrefs
- **Thread Safety:** All calls must be from the main thread (EditorPrefs constraint)
- **Isolation:** Plugin settings never conflict with core MCP settings or other plugins
- **Use `Name` as pluginId:** Convention: pass `this.Name` as pluginId

### Example

```csharp
public class MyPlugin : IMCPPlugin
{
    public string Name => "AssetManager";
    public string CommandPrefix => "asset_mgr";
    
    public void RegisterCommands()
    {
        CommandRegistry.Register("asset_mgr_scan", ScanAssets);
    }
    
    private static void ScanAssets(CommandContext ctx)
    {
        var threshold = PluginConfig.GetInt("AssetManager", "min_size_kb", 100);
        var results = ScanAssetsImpl(threshold);
        ctx.Send(new { ok = true, count = results.Count });
    }
    
    public void OnDomainReload() { }
}
```

Internal storage: `MCPPlugin_AssetManager_min_size_kb`

## Plugin Settings UI

Provide a user-friendly settings panel in the MCP Hub under **MCP → Settings → Plugins**.

### Building Settings UI

```csharp
public bool HasSettingsUI => true;

public VisualElement BuildSettingsUI()
{
    var root = new VisualElement();
    
    // Build your UI here
    var label = new Label("Plugin Settings");
    root.Add(label);
    
    return root;
}
```

**Requirements:**
- Return `null` if no UI (default)
- Set `HasSettingsUI => true` ONLY if `BuildSettingsUI()` returns non-null
- Wrap in try-catch; exceptions logged, UI gracefully skipped
- Return quickly; slow UI blocking editor startup

### Styling

#### Styles Provided by MCP

When building UI inside MCP Hub (default), these styles are automatically available:

```csharp
// Bordered foldout section
var card = new Foldout { text = "Title" };
card.AddToClassList("sampling-card");
root.Add(card);

// Horizontal row (children stretch equally)
var row = new VisualElement();
row.AddToClassList("sampling-inline-row");
root.Add(row);
```

#### Standalone EditorWindows

If building UI in a custom EditorWindow, load MCP styles manually:

```csharp
public class MyEditorWindow : EditorWindow
{
    private void OnEnable()
    {
        var root = rootVisualElement;
        PluginUIHelpers.LoadStyles(root);  // Load MCP Hub + Settings styles
        
        // Build UI
        var card = PluginUIHelpers.MakeCard("Settings", open: false);
        root.Add(card);
    }
}
```

**Available Style Classes:**
- `sampling-card` — Bordered foldout
- `sampling-inline-row` — Horizontal flex row
- `hub-card` — Plugin card in hub navigation
- `nav-page` — Settings page container
- `nav-back-header` — Back button + title header
- `nav-back-btn` — Back button styling
- `nav-back-title` — Page title styling

## PluginUIHelpers Convenience Layer

Auto-persist user settings via PluginUIHelpers. Each helper creates a UI element and binds it to PluginConfig automatically.

### MakeCard(title, open)

Create a bordered, foldable section:

```csharp
var card = PluginUIHelpers.MakeCard("Advanced Settings", open: false);
root.Add(card);
```

### InlineRow()

Create a horizontal row where children stretch equally:

```csharp
var row = PluginUIHelpers.InlineRow();
var field1 = PluginUIHelpers.AddTextField(row, "Label 1", "MyPlugin", "key1");
var field2 = PluginUIHelpers.AddTextField(row, "Label 2", "MyPlugin", "key2");
root.Add(row);
```

### AddTextField(parent, label, pluginId, key, defaultValue)

Text input field with auto-save:

```csharp
PluginUIHelpers.AddTextField(
    parent,
    label: "Asset Path",
    pluginId: "MyPlugin",
    key: "asset_path",
    defaultValue: "Assets/"
);
// Changes immediately persisted to PluginConfig
```

### AddToggle(parent, label, pluginId, key, defaultValue)

Boolean toggle with auto-save:

```csharp
PluginUIHelpers.AddToggle(
    parent,
    label: "Enable Scanning",
    pluginId: "MyPlugin",
    key: "enable_scan",
    defaultValue: true
);
```

### AddSlider(parent, label, pluginId, key, defaultValue, min, max)

Float slider (0-100 range example):

```csharp
PluginUIHelpers.AddSlider(
    parent,
    label: "Quality",
    pluginId: "MyPlugin",
    key: "quality",
    defaultValue: 50f,
    min: 0f,
    max: 100f
);
```

### AddIntSlider(parent, label, pluginId, key, defaultValue, min, max)

Integer slider (1-10 range example):

```csharp
PluginUIHelpers.AddIntSlider(
    parent,
    label: "Iterations",
    pluginId: "MyPlugin",
    key: "iterations",
    defaultValue: 5,
    min: 1,
    max: 10
);
```

### AddDropdown(parent, label, pluginId, key, choices, defaultValue)

Dropdown selection with auto-save:

```csharp
PluginUIHelpers.AddDropdown(
    parent,
    label: "Mode",
    pluginId: "MyPlugin",
    key: "mode",
    choices: new[] { "Fast", "Quality", "Balanced" },
    defaultValue: "Balanced"
);
// If saved value not in choices, falls back to defaultValue
```

### LoadStyles(root)

Load MCP styles for standalone EditorWindows:

```csharp
public class MyWindow : EditorWindow
{
    private void OnEnable()
    {
        PluginUIHelpers.LoadStyles(rootVisualElement);
    }
}
```

## Complete Plugin Example

Here's a full-featured asset manager plugin with settings UI:

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor;

public class AssetManagerPlugin : IMCPPlugin
{
    public string Name => "AssetManager";
    public string CommandPrefix => "asset_mgr";
    public bool HasSettingsUI => true;
    public string Description => "Audit, organize, and optimize project assets";
    
    public void RegisterCommands()
    {
        CommandRegistry.Register("asset_mgr_scan", ScanAssets);
        CommandRegistry.Register("asset_mgr_report", GenerateReport);
    }
    
    public void OnDomainReload()
    {
        // Reset any cached state if needed
        Debug.Log("[AssetManager] Domain reload");
    }
    
    public VisualElement BuildSettingsUI()
    {
        var root = new VisualElement();
        
        // Main settings card
        var settingsCard = PluginUIHelpers.MakeCard("Scan Settings", open: true);
        
        PluginUIHelpers.AddIntSlider(
            settingsCard,
            label: "Min Size (KB)",
            pluginId: Name,
            key: "min_size_kb",
            defaultValue: 100,
            min: 10,
            max: 10000
        );
        
        PluginUIHelpers.AddToggle(
            settingsCard,
            label: "Include Unused",
            pluginId: Name,
            key: "include_unused",
            defaultValue: false
        );
        
        root.Add(settingsCard);
        
        // Advanced options card
        var advCard = PluginUIHelpers.MakeCard("Advanced", open: false);
        
        PluginUIHelpers.AddDropdown(
            advCard,
            label: "Sort By",
            pluginId: Name,
            key: "sort_by",
            choices: new[] { "Size", "Date", "Name" },
            defaultValue: "Size"
        );
        
        PluginUIHelpers.AddSlider(
            advCard,
            label: "Compression",
            pluginId: Name,
            key: "compression",
            defaultValue: 0.5f,
            min: 0f,
            max: 1f
        );
        
        root.Add(advCard);
        
        return root;
    }
    
    private static void ScanAssets(CommandContext ctx)
    {
        var minSize = PluginConfig.GetInt(
            "AssetManager",
            "min_size_kb",
            100
        );
        var includeUnused = PluginConfig.GetBool(
            "AssetManager",
            "include_unused",
            false
        );
        
        var results = new List<string>();
        // Scan implementation...
        
        ctx.Send(new
        {
            ok = true,
            count = results.Count,
            minSize,
            includeUnused
        });
    }
    
    private static void GenerateReport(CommandContext ctx)
    {
        var sortBy = PluginConfig.GetString(
            "AssetManager",
            "sort_by",
            "Size"
        );
        
        ctx.Send(new
        {
            ok = true,
            sortBy,
            reportPath = "Assets/Reports/asset_report.txt"
        });
    }
}

// Auto-register on startup
[InitializeOnLoadMethod]
private static void RegisterAssetManager()
{
    PluginRegistry.Register(new AssetManagerPlugin());
}
```

This plugin:
- Registers 2 tools (`asset_mgr_scan`, `asset_mgr_report`)
- Provides settings UI with 5 different control types
- Reads settings via PluginConfig
- Shows card icons and description in Plugins hub

## Testing Plugins

### Unit Tests with NUnit

Use the `FakePlugin` test double pattern:

```csharp
[TestFixture]
public class AssetManagerTests
{
    [SetUp]
    public void SetUp() => PluginRegistry.Clear();
    
    [TearDown]
    public void TearDown() => PluginRegistry.Clear();
    
    [Test]
    public void PluginRegisters_CommandsPresent()
    {
        var plugin = new AssetManagerPlugin();
        PluginRegistry.Register(plugin);
        PluginRegistry.RegisterAllPlugins();
        
        var commands = PluginRegistry.GetCommandsForPlugin(plugin);
        Assert.Contains("asset_mgr_scan", commands);
        Assert.Contains("asset_mgr_report", commands);
    }
    
    [Test]
    public void SettingsUI_RendersCards()
    {
        var plugin = new AssetManagerPlugin();
        Assert.IsTrue(plugin.HasSettingsUI);
        
        var ui = plugin.BuildSettingsUI();
        Assert.IsNotNull(ui);
        
        // Query for child elements
        var cards = ui.Query<Foldout>().ToList();
        Assert.Greater(cards.Count, 0, "UI should contain foldout cards");
    }
}

// Test double
internal class FakePlugin : IMCPPlugin
{
    private readonly VisualElement _ui;
    
    public FakePlugin(string name, VisualElement ui)
    {
        Name = name;
        _ui = ui;
    }
    
    public string Name { get; }
    public string CommandPrefix => "";
    public bool HasSettingsUI => _ui != null;
    
    public void RegisterCommands() { }
    public void OnDomainReload() { }
    public VisualElement BuildSettingsUI() => _ui;
}
```

### Manual Testing

1. Create a test plugin in your project
2. Use `[InitializeOnLoadMethod]` to auto-register
3. Open **MCP → Settings → Plugins**
4. Verify plugin card appears with name, description, icon
5. Click card to open settings UI
6. Test PluginConfig persistence: change a value, reopen window, verify value persists
7. Test commands: use `send` tool from Chat to invoke `asset_mgr_scan`

## Best Practices

### 1. Use `this.Name` as pluginId

Always pass plugin name to PluginConfig methods:

```csharp
// Good
PluginConfig.SetString(this.Name, "key", value);
PluginConfig.GetString(this.Name, "key");

// Bad — hardcoded string, breaks if Name changes
PluginConfig.SetString("MyPlugin", "key", value);
```

### 2. BuildSettingsUI May Return Null

It's safe to return null if your plugin has no settings:

```csharp
public bool HasSettingsUI => false;  // Don't set true if returning null
public VisualElement BuildSettingsUI() => null;
```

### 3. Wrap BuildSettingsUI in Try-Catch

Complex UI building may throw. Exceptions are caught by SettingsPageFactory:

```csharp
public VisualElement BuildSettingsUI()
{
    try
    {
        // May throw if assets missing, etc.
        var ui = BuildComplexUI();
        return ui;
    }
    catch (Exception e)
    {
        Debug.LogError($"[{Name}] BuildSettingsUI failed: {e.Message}");
        return null;  // Graceful degrade
    }
}
```

### 4. Tool Naming Conventions

- **Prefixed tools:** `prefix`, `prefix_action`, `prefix_get_status` (CamelCase with underscores)
- **Non-prefixed tools:** Add to `AdditionalCommands` explicitly
- **Avoid conflicts:** Check plugin list before choosing prefix

```csharp
public string CommandPrefix => "my_unique_tool";  // Lower_snake_case

public void RegisterCommands()
{
    // Matches prefix pattern
    CommandRegistry.Register("my_unique_tool_action", Handler1);  // OK
    CommandRegistry.Register("my_unique_tool_get", Handler2);     // OK
    
    // Non-matching pattern — need AdditionalCommands
    CommandRegistry.Register("special", Handler3);  // Must add to list
}

public IReadOnlyList<string> AdditionalCommands 
    => new[] { "special" };
```

### 5. Description Should Be Concise

Keep Description 1-2 sentences for hub card display:

```csharp
// Good
public string Description 
    => "Audit and optimize project assets with size/dependency analysis";

// Bad — too long
public string Description 
    => "This plugin audits your project assets by analyzing their sizes, "
       + "dependencies, and usage patterns. It provides detailed reports "
       + "and recommendations...";
```

### 6. Domain Reload Safety

Reset caches and reconnections in OnDomainReload:

```csharp
private static Socket _socket;  // Static state lost on domain reload

public void OnDomainReload()
{
    _socket?.Close();
    _socket = null;  // Force reconnect on next use
    _cachedAssets?.Clear();
}
```

### 7. Thread Safety for PluginConfig

All PluginConfig calls must be from the main thread:

```csharp
// OK — called from UI or command handler (main thread)
public VisualElement BuildSettingsUI()
{
    var value = PluginConfig.GetString(Name, "key");  // Main thread
    return new Label(value);
}

// BAD — called from background thread
async void AsyncMethod()
{
    await Task.Delay(100);
    PluginConfig.SetString(Name, "key", "value");  // CRASHES!
}

// FIXED
async void AsyncMethod()
{
    await Task.Delay(100);
    EditorApplication.delayCall += () =>
        PluginConfig.SetString(Name, "key", "value");  // Main thread
}
```

### 8. Organize Settings by Logical Groups

Use cards (foldouts) to group related settings:

```csharp
// Basic settings card (default open)
var basicCard = PluginUIHelpers.MakeCard("Basic", open: true);
PluginUIHelpers.AddTextField(basicCard, ...);
PluginUIHelpers.AddToggle(basicCard, ...);
root.Add(basicCard);

// Advanced settings card (default closed)
var advCard = PluginUIHelpers.MakeCard("Advanced", open: false);
PluginUIHelpers.AddSlider(advCard, ...);
PluginUIHelpers.AddDropdown(advCard, ...);
root.Add(advCard);
```

### 9. Use Inline Rows for Related Controls

Place related controls side-by-side:

```csharp
var row = PluginUIHelpers.InlineRow();
PluginUIHelpers.AddIntSlider(row, "Min", Name, "min_val", 0, 0, 100);
PluginUIHelpers.AddIntSlider(row, "Max", Name, "max_val", 100, 0, 100);
root.Add(row);
```

### 10. Test Settings Persistence

Verify PluginConfig saves and loads correctly:

```csharp
[Test]
public void PluginConfig_StringPersists()
{
    PluginConfig.SetString("TestPlugin", "key", "value1");
    var loaded = PluginConfig.GetString("TestPlugin", "key");
    Assert.AreEqual("value1", loaded);
    
    PluginConfig.SetString("TestPlugin", "key", "value2");
    loaded = PluginConfig.GetString("TestPlugin", "key");
    Assert.AreEqual("value2", loaded);
}

[Test]
public void PluginConfig_DefaultValue_WhenKeyMissing()
{
    PluginConfig.Delete("TestPlugin", "missing_key");
    var value = PluginConfig.GetString("TestPlugin", "missing_key", "default");
    Assert.AreEqual("default", value);
}
```

## Troubleshooting

### Plugin Not Appearing in Settings

1. Verify `[InitializeOnLoadMethod]` is present and calling `PluginRegistry.Register()`
2. Check `HasSettingsUI => true` AND `BuildSettingsUI()` returns non-null
3. Restart Unity editor to trigger `[InitializeOnLoadMethod]`
4. Check Console for registration errors

### Settings Not Persisting

1. Verify you're calling `PluginConfig.Set*()` methods (not just reading)
2. Check main thread: PluginConfig calls must run on main thread only
3. Verify pluginId matches `this.Name` exactly (case-sensitive)
4. Check EditorPrefs size limit (rarely an issue, but possible)

### Settings UI Crashes/Doesn't Render

1. Add try-catch to `BuildSettingsUI()` — exceptions are silently caught
2. Test UI building in isolation (not in OnEnable)
3. Verify all UIElements and PluginUIHelpers calls are valid
4. Check Console for error messages from `SettingsPageFactory`

### Domain Reload Issues

1. Implement `OnDomainReload()` to reset static state
2. Test in Play Mode: close/edit scripts/resume Play to trigger reload
3. Verify sockets/streams closed in `OnDomainReload()`

## See Also

- `/docs/tools/` — MCP command/tool documentation
- `/unity-plugin/Editor/Tests/PluginSettingsPageTests.cs` — Complete test examples
- `/unity-plugin/Editor/PluginRegistry.cs` — Plugin discovery and registration
- `/unity-plugin/Editor/PluginConfig.cs` — Settings storage API
- `/unity-plugin/Editor/PluginUIHelpers.cs` — UI convenience methods
