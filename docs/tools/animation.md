# Animation Tools

Manage AnimationClips, Timeline sequences, Animator Controllers, and Particle Systems. Use these tools for keyframe animation, cinematic authoring, state machine setup, and particle effects.

## animation

Read or author keyframe animation on AnimationClips. Use this for per-object AnimationClips, not for Animator state machine control (use `animator` for that).

**Parameters:**
- `action` (string) — "get" | "create" | "edit" | "preview"
- `path` (string) — Scene path to target GameObject
- `clip` (string, optional) — AnimationClip name (required for edit/preview)
- `clip_name` (string, optional) — New clip name (used with create)
- `property` (string, optional) — Property to animate (e.g., "localPosition.x", "scale.y", "m_Color.a")
- `keys` (string, optional) — Keyframe data: `t:0 v:(0,0,0); t:1 v:(0,2,0)` (time in seconds, value)
- `time` (float, optional) — Time position for preview (seconds)

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| get | List clips and keys | path | `animation("get", path="Player")` |
| create | New AnimationClip on object | path, clip_name | `animation("create", path="Player", clip_name="Walk")` |
| edit | Add/replace keyframes | path, clip, property, keys | `animation("edit", path="Player", clip="Walk", property="localPosition.x", keys="t:0 v:0; t:1 v:5")` |
| preview | Scrub to time | path, clip, time | `animation("preview", path="Player", clip="Walk", time=0.5)` |

**Example:**

```python
# Create animation clip
await animation("create", path="Player", clip_name="Jump")

# Add keyframes (0→1 second, position 0→10 on X-axis)
await animation("edit", path="Player", clip="Jump", property="localPosition.x",
                keys="t:0 v:0; t:1 v:10")

# Preview at 0.5 seconds
await animation("preview", path="Player", clip="Jump", time=0.5)
```

---

## timeline

Manage Unity Timeline (PlayableDirector / TimelineAsset) for multi-track cinematic sequences. Use for mixing animation, audio, activation, and custom tracks.

**Parameters:**
- `path` (string) — Scene path to GameObject with PlayableDirector
- `action` (string) — "get" | "create" | "add_track" | "remove_track" | "add_clip" | "remove_clip" | "set_binding" | "set_timing" | "mute" | "unmute" | "lock" | "unlock" | "preview"
- `track` (string, optional) — Track name for targeting specific track
- `track_type` (string, optional) — Track type: "Animation" | "Audio" | "Activation" | "Signal" | "Control"
- `clip` (string, optional) — AnimationClip name
- `binding` (string, optional) — Scene object path to bind track
- `start` (float, optional) — Clip start time (seconds)
- `duration` (float, optional) — Clip duration (seconds)
- `blend_in` (float, optional) — Blend-in duration (seconds)
- `blend_out` (float, optional) — Blend-out duration (seconds)
- `asset_path` (string, optional) — TimelineAsset path (Assets/...)
- `director_path` (string, optional) — PlayableDirector path
- `tracks` (string, optional) — Track list (get action)
- `time` (float, optional) — Scrub to time (seconds)

**Actions:**

| Action | Purpose | Example |
|--------|---------|---------|
| get | Inspect tracks and clips | `timeline(path="Cutscene", action="get")` |
| create | New TimelineAsset | `timeline(path="Cutscene", action="create", asset_path="Assets/Cinematics/Intro.playable")` |
| add_track | Create track | `timeline(path="Cutscene", action="add_track", track="AnimTrack1", track_type="Animation")` |
| set_binding | Bind track to object | `timeline(path="Cutscene", action="set_binding", track="AnimTrack1", binding="Player")` |
| add_clip | Place clip on track | `timeline(path="Cutscene", action="add_clip", track="AnimTrack1", clip="Walk", start=0, duration=2)` |
| preview | Scrub to time | `timeline(path="Cutscene", action="preview", time=1.5)` |

**Example:**

```python
# Create new timeline
await timeline(path="Director", action="create", asset_path="Assets/Intro.playable")

# Add animation track
await timeline(path="Director", action="add_track", track="PlayerAnim", track_type="Animation")

# Bind track to Player
await timeline(path="Director", action="set_binding", track="PlayerAnim", binding="Player")

# Place animation clip
await timeline(path="Director", action="add_clip", track="PlayerAnim", 
               clip="Walk", start=0, duration=2)

# Preview at 1 second
await timeline(path="Director", action="preview", time=1.0)
```

---

## animator

Manage Animator Controller state machines. Add states, parameters, and transitions.

**Parameters:**
- `action` (string) — "get" | "add_param" | "add_state" | "add_transition" | "set_default" | "remove"
- `path` (string) — Scene path to GameObject with Animator
- `state` (string, optional) — State name
- `states` (string, optional) — State definitions: "Idle:Idle.anim; Walk:Walk.anim; Run"
- `params` (string, optional) — Parameters: "Speed:float:0; Jump:trigger; IsGrounded:bool:false"
- `source` (string, optional) — Transition source state (use "*" for AnyState)
- `target` (string, optional) — Transition target state
- `conditions` (string, optional) — Transition conditions: "Speed>0.1; IsGrounded"
- `duration` (float, optional) — Transition duration (seconds)
- `exit_time` (float, optional) — Exit time threshold (0-1)
- `has_exit_time` (bool, optional) — Whether transition has exit time
- `type` (string, optional) — Parameter type (float|bool|int|trigger)
- `name` (string, optional) — Parameter or state name

**Actions:**

| Action | Purpose | Example |
|--------|---------|---------|
| get | Inspect states, params, transitions | `animator("get", path="Player")` |
| add_param | Create parameter | `animator("add_param", path="Player", type="float", name="Speed")` |
| add_state | Create state | `animator("add_state", path="Player", state="Walk")` |
| add_transition | Create transition | `animator("add_transition", path="Player", source="Idle", target="Walk", conditions="Speed>0.1", duration=0.2)` |
| set_default | Set default state | `animator("set_default", path="Player", state="Idle")` |

**Example:**

```python
# Create parameters
await animator("add_param", path="Player", type="float", name="Speed")
await animator("add_param", path="Player", type="bool", name="IsGrounded")

# Add states
await animator("add_state", path="Player", state="Idle")
await animator("add_state", path="Player", state="Walk")
await animator("add_state", path="Player", state="Run")

# Add transitions
await animator("add_transition", path="Player", source="Idle", target="Walk",
              conditions="Speed>0.1", duration=0.2)
await animator("add_transition", path="Player", source="Walk", target="Run",
              conditions="Speed>2.0", duration=0.3)

# Set default state
await animator("set_default", path="Player", state="Idle")
```

---

## particle

Create and configure Particle Systems with preset or custom modules.

**Parameters:**
- `action` (string) — "get" | "create" | "set" | "apply"
- `path` (string) — Scene path to target GameObject
- `name` (string, optional) — Particle system name
- `module` (string, optional) — Module: "main" | "emission" | "shape" | "colorOverLifetime" | "sizeOverLifetime" | "velocityOverLifetime" | "noise" | "renderer" | "trails" | "collision" | "rotationOverLifetime"
- `prop` (string, optional) — Module property name
- `value` (string, optional) — Property value
- `preset` (string, optional) — Preset type: "fire" | "smoke" | "sparks" | "rain" | "snow" | "explosion" | "magic" | "dust" | "blood" | "trail"

**Actions:**

| Action | Purpose | Example |
|--------|---------|---------|
| get | Inspect particle system | `particle("get", path="Effects/Fire")` |
| create | New ParticleSystem | `particle("create", path="Enemy", name="Explosion", preset="explosion")` |
| set | Change module property | `particle("set", path="Effects/Fire", module="emission", prop="rateOverTime", value="50")` |
| apply | Apply changes | `particle("apply", path="Effects/Fire")` |

**Example:**

```python
# Create particle system with preset
await particle("create", path="Effects", name="ExplosionFX", preset="explosion")

# Customize emission
await particle("set", path="Effects/ExplosionFX", module="emission", 
              prop="rateOverTime", value="100")

# Customize renderer
await particle("set", path="Effects/ExplosionFX", module="renderer",
              prop="maxParticleSize", value="10")

# Apply
await particle("apply", path="Effects/ExplosionFX")
```

---

## Common Patterns

| Task | Tools | Example |
|------|-------|---------|
| Create looping animation | animation → editor(play) | `await animation("create", path="Player", clip_name="Idle"); await animation("edit", path="Player", clip="Idle", property="localPosition.x", keys="t:0 v:0; t:1 v:0")` |
| Build animator state machine | animator (add_param → add_state → add_transition) | Add all parameters first, then states, then transitions |
| Create cinematic sequence | timeline (add_track → set_binding → add_clip → preview) | Use multiple tracks for layered sequences |
| Particle effect with animation | particle(preset) → timeline(add_clip) | Add particle system to Timeline for synchronized effects |

---

**See also:** [Scene Tools](scene.md) for playback control, [Runtime Tools](runtime.md) for Play Mode state inspection.
