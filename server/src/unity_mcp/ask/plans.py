"""ToolPlan dataclass and canonical templates for ask() tool."""
from dataclasses import dataclass, field


@dataclass
class ToolPlan:
    steps: list[tuple[str, dict]]  # [(tool_name, args), ...]
    hint: str                       # summarizer hint
    key: str = ""                   # pattern key e.g. "BROKEN_REFS"


CANONICAL_PLANS: dict[str, ToolPlan | None] = {
    "BROKEN_REFS": ToolPlan(
        [("validate_references", {"path": "/", "depth": "5"})],
        "broken refs only",
        "BROKEN_REFS",
    ),
    "SCENE_HEALTH": ToolPlan(
        [
            ("scan_scene", {}),
            ("validate_references", {"path": "/", "depth": "3"}),
            ("get_console", {"count": "10", "level": "Error"}),
            ("get_compile_errors", {}),
        ],
        "summarize top issues",
        "SCENE_HEALTH",
    ),
    "COUNT_ACTIVE": None,  # needs filter extraction — use Haiku
    "EDITOR_STATE": ToolPlan(
        [("editor", {"action": "state"})],
        "play state",
        "EDITOR_STATE",
    ),
    "COMPILE_ERRORS": ToolPlan(
        [("get_compile_errors", {})],
        "compile errors",
        "COMPILE_ERRORS",
    ),
}
