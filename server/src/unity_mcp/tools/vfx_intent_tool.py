"""vfx_intent — NL → VFX DSL → particle/shader batch commands."""
import re
from typing import Optional
from ..sampling import SamplingService
from .intent_common import strip_fences, build_batch_line
from ._annotations import RW as _RW

_send = None
_sampling: SamplingService = SamplingService()

# 5 built-in presets — bypass Haiku entirely
_PRESETS: dict[str, dict] = {
    "fire_explosion": {
        "sets": [("startColor", "#FF2200"), ("startSize", "0.5,2.0"), ("startSpeed", "3,8"), ("maxParticles", "200")],
        "modules": [("colorOverLifetime", "ENABLED"), ("sizeOverLifetime", "ENABLED")],
        "gradients": [("color", "#FF8800@0;#FF2200@0.5;#33000080@1")],
    },
    "magic_burst": {
        "sets": [("startColor", "#8800FF"), ("startSize", "0.2,0.8"), ("startSpeed", "2,5"), ("maxParticles", "100")],
        "modules": [("colorOverLifetime", "ENABLED")],
        "gradients": [("color", "#CC88FF@0;#8800FF@0.6;#00000000@1")],
    },
    "dissolve": {
        "sets": [("startColor", "#FFFFFF"), ("startSize", "0.1,0.3"), ("startSpeed", "0.5,1.5"), ("maxParticles", "300")],
        "modules": [("sizeOverLifetime", "ENABLED")],
        "gradients": [],
    },
    "glow_outline": {
        "sets": [("startColor", "#FFFF00"), ("startSize", "0.05,0.15"), ("startSpeed", "0"), ("maxParticles", "50")],
        "modules": [("colorOverLifetime", "ENABLED")],
        "gradients": [("color", "#FFFF00@0;#FFFF0000@1")],
    },
    "smoke_trail": {
        "sets": [("startColor", "#888888"), ("startSize", "0.3,1.0"), ("startSpeed", "0.5,1.0"), ("maxParticles", "150")],
        "modules": [("colorOverLifetime", "ENABLED"), ("sizeOverLifetime", "ENABLED")],
        "gradients": [("color", "#66666680@0;#33333300@1")],
    },
}

_PARTICLE_KEYWORDS = {"explosion", "burst", "emit", "particle", "spark", "fire", "smoke", "rain", "snow"}
_SHADER_KEYWORDS = {"dissolve", "glow", "outline", "shader", "material"}

_PROMPT_TEMPLATE = """\
Generate a VFX DSL for Unity particle system. Use ONLY:
SET <prop> = <value>
MODULE <moduleName> ENABLED|DISABLED
GRADIENT <prop> = <color>@<time>;...

Example:
SET startColor = #FF2200
SET startSize = 0.5,1.0
MODULE colorOverLifetime ENABLED
GRADIENT color = #FF8800@0;#FF2200@1

Target: {target}
VFX kind: {kind}
Intent: {intent}

Output ONLY the DSL, no explanation, no fences."""


def get_preset_config(name: str) -> Optional[dict]:
    return _PRESETS.get(name)


def detect_kind(intent: str) -> str:
    low = intent.lower()
    if any(k in low for k in _SHADER_KEYWORDS):
        return "shader"
    if any(k in low for k in _PARTICLE_KEYWORDS):
        return "particle"
    return "particle"


def parse_vfx_dsl(dsl: str) -> dict:
    result = {"sets": [], "modules": [], "gradients": []}
    for line in dsl.splitlines():
        line = line.strip()
        if not line:
            continue
        if line.startswith("SET "):
            m = re.match(r"SET\s+(\w+)\s*=\s*(.+)", line)
            if m:
                result["sets"].append((m.group(1), m.group(2).strip()))
        elif line.startswith("MODULE "):
            parts = line.split()
            if len(parts) >= 3:
                result["modules"].append((parts[1], parts[2]))
        elif line.startswith("GRADIENT "):
            m = re.match(r"GRADIENT\s+(\w+)\s*=\s*(.+)", line)
            if m:
                result["gradients"].append((m.group(1), m.group(2).strip()))
    return result


def build_vfx_batch(target: str, data: dict) -> list[str]:
    lines = []
    for prop, val in data.get("sets", []):
        lines.append(build_batch_line("particle", action="set", path=target, prop=prop, value=val))
    for mod, state in data.get("modules", []):
        enabled = "true" if state.upper() == "ENABLED" else "false"
        lines.append(build_batch_line("particle", action="set", path=target, module=mod, prop="enabled", value=enabled))
    for prop, grad in data.get("gradients", []):
        lines.append(build_batch_line("particle", action="set", path=target, module="colorOverLifetime", prop=prop, value=grad))
    return lines


async def vfx_intent(target: str, intent: str, kind: str = "auto", dry_run: bool = False) -> str:
    """Convert NL intent to Unity VFX setup. Presets bypass Haiku entirely.

    kind: particle|shader|material|auto. dry_run=True skips execution.
    """
    # Preset bypass
    preset = get_preset_config(intent.strip())
    if preset is not None:
        data = preset
    else:
        if kind == "auto":
            kind = detect_kind(intent)
        from .intent_common import sanitize_intent
        prompt = _PROMPT_TEMPLATE.format(target=sanitize_intent(target), kind=sanitize_intent(kind), intent=sanitize_intent(intent))
        dsl_raw = await _sampling.generate(prompt, feature='vfx_intent')
        if not dsl_raw:
            return "ERROR: Haiku unavailable (set UNITY_MCP_VISUAL_VERIFY=1)"
        data = parse_vfx_dsl(strip_fences(dsl_raw))

    batch_lines = build_vfx_batch(target, data)
    if not batch_lines:
        return "ERROR: DSL produced no commands"

    if dry_run:
        return "\n".join(batch_lines)

    result = await _send("batch", {"commands": "\n".join(batch_lines)})
    return f"vfx_intent: {len(batch_lines)} ops\n{result}"


def register(mcp, send, args):
    global _send
    _send = send
    mcp.tool(annotations=_RW)(vfx_intent)
