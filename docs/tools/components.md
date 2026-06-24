# Event Wiring Tools

Connect and disconnect UnityEvent persistent listeners. Wire UI buttons to methods, trigger events, and manage event callbacks without manual serialization.

## wire_event

Connect a persistent listener to a UnityEvent field. Wire buttons to methods, hook lifecycle events, and create event chains.

**Parameters:**
- `path` (string) — Scene path to GameObject with the event
- `component` (string) — Component type owning the event field (e.g., "Button", "CustomController")
- `event` (string) — Serialized field name (e.g., "onClick", "_onComplete", "onDamaged")
- `target` (string) — Scene path (/Player) or asset path (Assets/Prefabs/Handler.prefab) for target object/prefab
- `method` (string) — Method name to invoke (e.g., "SetActive", "Play", "TakeDamage")
- `arg_type` (string, default="void") — Parameter type: "void" | "bool" | "int" | "float" | "string" | "object"
- `arg_value` (string, optional) — Parameter value (required if arg_type != void). For object: scene path or asset path.

**Argument Types:**

| Type | Description | arg_value Example |
|------|-------------|------------------|
| void | No argument | (omit) |
| bool | Boolean | "true" or "false" |
| int | Integer | "10" or "-5" |
| float | Decimal number | "0.5" or "3.14" |
| string | Text | "Hello World" |
| object | GameObject/Component reference | "/Player" (scene) or "Assets/Obj.prefab" (asset) |

**Example:**

```python
# Wire button click to SetActive
await wire_event(path="Canvas/PlayButton", component="Button", 
                event="onClick", target="PauseMenu",
                method="SetActive", arg_type="bool", arg_value="false")

# Wire trigger enter to TakeDamage
await wire_event(path="DamageZone", component="DamageTrigger",
                event="onTriggerEnter", target="Player",
                method="TakeDamage", arg_type="int", arg_value="10")

# Wire audio play
await wire_event(path="SoundButton", component="Button",
                event="onClick", target="AudioSource",
                method="Play")

# Wire with string parameter
await wire_event(path="NameInput", component="InputField",
                event="onEndEdit", target="SceneController",
                method="SetPlayerName", arg_type="string", arg_value="Hero")

# Wire to prefab method (asset reference)
await wire_event(path="Canvas/Confirm", component="Button",
                event="onClick", target="Assets/Handlers/GameController.prefab",
                method="StartGame")
```

**Common Patterns:**

| Pattern | Example |
|---------|---------|
| Button → Activate/Deactivate | `wire_event(path="Button", component="Button", event="onClick", target="Panel", method="SetActive", arg_type="bool", arg_value="true")` |
| Trigger → Damage | `wire_event(path="Spike", component="Collider", event="onTriggerEnter", target="Player", method="TakeDamage", arg_type="int", arg_value="10")` |
| Input → Method Call | `wire_event(path="Canvas/Input", component="InputField", event="onEndEdit", target="Handler", method="ProcessInput", arg_type="string", arg_value="...")` |
| UI → Animation | `wire_event(path="Button", component="Button", event="onClick", target="Character", method="PlayAnimation", arg_type="string", arg_value="Attack")` |

**Verification:**
After wiring, verify with:
```python
comp = await get_component(path="Canvas/PlayButton", type="Button")
# Should show onClick listeners connected
```

---

## unwire_event

Remove persistent listener(s) from a UnityEvent. Clear specific listeners or clear all listeners at once.

**Parameters:**
- `path` (string) — Scene path to GameObject with the event
- `component` (string) — Component type owning the event field
- `event` (string) — Serialized field name
- `index` (int, optional) — Remove specific listener (0-based). Omit to clear all listeners.

**Example:**

```python
# Remove all listeners from button
await unwire_event(path="Canvas/PlayButton", component="Button", event="onClick")

# Remove specific listener (first one, index 0)
await unwire_event(path="Canvas/PlayButton", component="Button", 
                  event="onClick", index=0)

# Remove second listener
await unwire_event(path="Canvas/PlayButton", component="Button",
                  event="onClick", index=1)

# Clear all listeners from custom event
await unwire_event(path="Enemy", component="EnemyController",
                  event="onDeath")
```

**Verification:**
After unwiring, verify with:
```python
comp = await get_component(path="Canvas/PlayButton", type="Button")
# onClick listeners should be empty
```

---

## Workflow: Complete Event Setup

**Scenario:** Create a button that pauses the game on click.

1. **Create UI**
   ```python
   await create_ui(type="Button", name="PauseButton", anchor="top-right",
                  text="Pause", fontSize="24")
   ```

2. **Wire to game controller**
   ```python
   await wire_event(path="Canvas/PauseButton", component="Button",
                   event="onClick", target="GameController",
                   method="TogglePause")
   ```

3. **Verify connection**
   ```python
   button = await get_component(path="Canvas/PauseButton", type="Button")
   print(button)  # Should show onClick listener
   ```

4. **Test in play mode**
   ```python
   await editor("play")
   await wait_until(timeout=10)  # Simulate gameplay
   await editor("stop")
   ```

5. **If needed, clear listeners**
   ```python
   await unwire_event(path="Canvas/PauseButton", component="Button",
                     event="onClick")
   ```

---

## Advanced Patterns

### Multi-Listener Event Chain
```python
# Wire multiple listeners to same event
await wire_event(path="Button", component="Button", event="onClick",
                target="AudioManager", method="PlaySound", 
                arg_type="string", arg_value="click")

await wire_event(path="Button", component="Button", event="onClick",
                target="UI", method="ShowMessage",
                arg_type="string", arg_value="Button pressed!")

await wire_event(path="Button", component="Button", event="onClick",
                target="Analytics", method="LogEvent",
                arg_type="string", arg_value="button_clicked")
```

### Conditional Wiring Based on State
```python
# Check if already wired
comp = await get_component(path="Button", type="Button")
if "onClick" not in comp:
    await wire_event(path="Button", component="Button", event="onClick",
                    target="Handler", method="OnClick")
```

### Toggle Active State
```python
# Button activates panel
await wire_event(path="OpenButton", component="Button", event="onClick",
                target="MenuPanel", method="SetActive",
                arg_type="bool", arg_value="true")

# Close button deactivates
await wire_event(path="CloseButton", component="Button", event="onClick",
                target="MenuPanel", method="SetActive",
                arg_type="bool", arg_value="false")
```

---

## Common Errors & Solutions

| Error | Cause | Solution |
|-------|-------|----------|
| "Method not found" | Typo or wrong component | Verify method name and component type match |
| "Target not found" | Wrong path | Use `search_scene()` to find target path |
| "Listener not added" | Event field name wrong | Check serialized field name (e.g., onClick, onValueChanged) |
| Multiple listeners | Wired same event twice | Use `unwire_event()` to clear first |

---

**See also:** [Objects Tools](objects.md) for component management, [UI Tools](ui.md) for creating UI elements, [Scene Tools](scene.md) for editor control.
