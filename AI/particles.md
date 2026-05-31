# Feature: Particle System Management (Phase 17)

## Overview

1 consolidated MCP tool `particle` with 4 actions for reading/creating/modifying ParticleSystem components. Supports 11 module types and 10 built-in presets (fire, smoke, sparks, rain, snow, explosion, magic, dust, blood, trail). Uses ParticleSerializer (read-only) and ParticleHelper (write/CRUD).

## Architecture

```
Claude Code ←─stdio─→ Python MCP Server ←─TCP:9500─→ Unity Editor Plugin
                            │                              │
                     particle tool               CommandRouter (1 case)
                     4 actions                   ExecParticleConsolidated
                                                         │
                                              ┌──────────┴──────────┐
                                              │                     │
                                        ParticleSystem         ParticleSystem
                                        Serializer (read)      Helper (write/CRUD)
```

## Tool Actions

| Action | Description |
|--------|-------------|
| `get` | Read particle system: overview all modules or specific module detail |
| `create` | Create empty ParticleSystem or with preset (name required, path optional, preset optional) |
| `set` | Set single module property (module, prop, value required) |
| `apply` | Apply preset to existing ParticleSystem (overrides current settings) |

## Modules (11 total)

| Module | Type | Readable | Writable | Use Case |
|--------|------|----------|----------|----------|
| `main` | Main settings | ✓ | ✓ | Duration, loop, start speed/size/color/rotation |
| `emission` | Emission | ✓ | ✓ | Rate over time/distance, burst events |
| `shape` | Shape | ✓ | ✓ | Sphere/box/cone/circle emission area |
| `colorOverLifetime` | Gradient | ✓ | ✓ | Color fade/transition over lifetime |
| `sizeOverLifetime` | Size curve | ✓ | ✓ | Size scaling over lifetime |
| `velocityOverLifetime` | Velocity | ✓ | ✓ | Speed/direction over lifetime |
| `noise` | Noise | ✓ | ✓ | Turbulence, wind, randomness |
| `renderer` | Rendering | ✓ | ✓ | Material, render mode, order |
| `trails` | Trails | ✓ | ✓ | Particle trails (lifetime, material) |
| `collision` | Physics | ✓ | ✓ | Bounce, lifetime on hit |
| `rotationOverLifetime` | Rotation | ✓ | ✓ | Angular velocity curves |

## Presets (10 total)

| Preset | Main | Emission | Shape | Color | Size | Use Case |
|--------|------|----------|-------|-------|------|----------|
| `fire` | loop, default | 40/s rate | cone | orange→red→black fade | grow+shrink | Fire, explosions |
| `smoke` | loop, default | 15/s rate | cone | gray fade → transparent | slow grow | Smoke, fog |
| `sparks` | !loop, 0.5s | burst 30-60 | cone | yellow→orange fade | tiny shrink | Sparks, magic |
| `rain` | loop, default | 500/s rate | box | default (white) | 3D stretch | Rain, weather |
| `snow` | loop, default | 100/s rate | box | white | default | Snow, blizzard |
| `explosion` | !loop, 0.5s | burst 50-100 | sphere | white→orange→dark fade | fast shrink | Explosions |
| `magic` | loop, default | 30/s rate | sphere | blue→purple fade | pulse | Magic spells |
| `dust` | loop, default | 5/s rate | box | brown fade in/out | default | Dust clouds |
| `blood` | !loop, 0.3s | burst 20-40 | cone | red→dark fade | grow | Blood splatter |
| `trail` | loop, default | 10/distance | shape disabled | cyan→fade | default | Trails, streaks |

## Key Implementation Details

### Create
- Auto-creates GameObject with ParticleSystem if path doesn't exist
- If preset provided, applies all preset values immediately
- Returns overview text showing module settings
- Empty ParticleSystem has sensible defaults (duration=5, loop=true, 10 particles/s)

### Get
- Without `module` parameter: returns all 11 modules in overview format (1-line summary each)
- With `module` parameter: returns detailed properties for that module
- Read-only, no state changes
- Handles missing ParticleSystem gracefully (error message with suggestion)

### Set
- Single property mutation: `module`, `prop`, `value` all required
- Only works on existing ParticleSystem
- Auto-records Undo before modification
- Returns updated module state for verification

### Apply
- Replaces all 11 modules with preset values
- Works on existing ParticleSystem only
- Creates/enables modules as needed (e.g., trails, collision)
- All changes recorded with single Undo action
- Returns final state showing all applied values

## Text Output Format

```
ParticleSystem on '/FX'
main: duration=3 loop=true startLifetime=0.5..1.5 startSpeed=1..3 startSize=0.3..0.8 maxParticles=200
emission: enabled rateOverTime=40
shape: enabled type=Cone angle=15 radius=0.3
colorOverLifetime: enabled
sizeOverLifetime: enabled
velocityOverLifetime: disabled
noise: enabled
trails: disabled
collision: disabled
rotationOverLifetime: disabled
renderer: Billboard
```

## Files

| File | Lines | Role |
|------|-------|------|
| `ParticleSerializer.cs` | 175 | Read ParticleSystem → text (all 11 modules) |
| `ParticleHelper.cs` | 377 | CRUD: create/set/apply + 10 presets + defaults |
| `tools/animation.py` | +26 | Python tool definition (4 actions) |
| `CommandRouter.cs` | +18 | Routing + ExecParticleConsolidated handler |
| `MCPSettings.cs` | +1 | Tool toggle (particle enabled by default) |

## Test Coverage (Phase 18)

| Test Suite | Count | Coverage |
|------------|-------|----------|
| `test_server_particle.py` (Python) | 16 tests | get/create/set workflows, hex startcolor, noise strength+frequency, shape radius, startsize normalization |
| `MCPParticleTests.cs` (C#) | 29 tests | toggle modules (color/trails/size), workflow (create→set→get), presets, renderer stretch, shape collider |
| Live scenarios (25-29) | 5 scenarios | Fire preset + variations, smoke with noise, sparks color override, rain shape config, snow trails |

**Total:** Python ~1533 tests, C# ~727 tests (project-wide). Particle-specific tests in both suites.

## Error Handling

- Missing GameObject → "ParticleSystem not found at path, create one first"
- Missing ParticleSystem component → "Add ParticleSystem component to path"
- Invalid module name → "Unknown module: {name}"
- Invalid preset → "Unknown preset: {name}"
- Read-only `get` → always succeeds (or friendly not-found message)

## Notes for Agents

### Senior Developer
- ParticleHelper uses explicit switch statements for module property dispatch
- Presets stored as static methods within ParticleHelper class
- Create uses Undo.RegisterCreatedObjectUndo(), Set/Apply use Undo.RecordObject()
- Batch presets update all 11 modules in single Undo action

### Architect
- Consolidation reduces MCP tools from 32→19 (Phase 17)
- Particle system is read-lightweight (inspecting costs nothing)
- Presets enable quick scene setup (fire effect in 1 call vs 10+ manual sets)
- Module structure mirrors Unity ParticleSystem API hierarchy

### Code Reviewer
- Check ParticleSerializer handles all 11 modules consistently
- Verify ParticleHelper apply() disables unused modules (keeps scene clean)
- Confirm presets have sensible defaults (visible, useful, not extreme)
- Ensure text output format matches architecture.md for consistency
