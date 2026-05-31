"""Property-based fuzzer for run_playtest DSL scripts."""
import random

ACTIONS = [
    "INVOKE /GridPlayer GridPlayer MoveTo {ix},{iz}",
    "INVOKE /GridPlayer GridPlayer Move north",
    "INVOKE /GridPlayer GridPlayer Move south",
    "INVOKE /GridPlayer GridPlayer Move east",
    "INVOKE /GridPlayer GridPlayer Move west",
    "WAIT 0.3",
    "WAIT 1.0",
    "ASSERT /GridPlayer|GridPlayer|PosX >= 0",
    "ASSERT /GridPlayer|GridPlayer|PosZ >= 0",
    "LOG Fuzzer step {step}",
    "ASSERT_CONSOLE_CLEAN",
]

INVARIANTS = [
    "INVARIANT /GridPlayer|GridPlayer|Score >= 0",
    "INVARIANT /GridPlayer|Transform|position.y >= -1",
]

_PREFIX_SKIP = ("INVARIANT", "TIMESCALE")


def generate_script(num_steps: int = 10, seed: int = None) -> str:
    """Generate random playtest script with invariants. Deterministic with seed."""
    rng = random.Random(seed)
    lines = list(INVARIANTS)
    lines.append("TIMESCALE 10")
    for i in range(num_steps):
        ix = rng.randint(0, 9)
        iz = rng.randint(0, 9)
        action = rng.choice(ACTIONS).format(ix=ix, iz=iz, step=i)
        lines.append(action)
    lines.append("ASSERT_CONSOLE_CLEAN")
    return "\n".join(lines)


def shrink_script(script: str, failing_step: int) -> str:
    """Return minimal subsequence ending at failing_step (line index in full script)."""
    lines = script.split("\n")
    header = [l for l in lines if any(l.startswith(p) for p in _PREFIX_SKIP)]
    actions = [l for l in lines if not any(l.startswith(p) for p in _PREFIX_SKIP)]
    # failing_step is index into actions list
    minimal = header + actions[max(0, failing_step - 2):failing_step + 1]
    return "\n".join(minimal)
