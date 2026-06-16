"""Shared DSL utilities for Tier B intent tools."""
from typing import Optional
from ..sampling_postproc import strip_fences  # backward-compat re-export
from ..utils import parse_kv  # consolidated — was local copy

__all__ = ["strip_fences", "parse_kv", "parse_indent_tree", "sanitize_intent", "build_batch_line"]


def parse_indent_tree(dsl: str) -> list[dict]:
    """Parse 2-space-indent DSL into flat list with depth + parent refs."""
    nodes: list[dict] = []
    for raw_line in dsl.splitlines():
        if not raw_line.strip():
            continue
        stripped = raw_line.rstrip()
        indent = len(stripped) - len(stripped.lstrip(" "))
        depth = indent // 2
        # Find parent: last node with depth = current - 1
        parent = None
        for n in reversed(nodes):
            if n["depth"] == depth - 1:
                parent = n
                break
        nodes.append({"line": stripped.strip(), "depth": depth, "parent": parent})
    return nodes


def sanitize_intent(text: str, max_len: int = 500) -> str:
    """Cap length, strip newlines and braces to mitigate prompt injection."""
    return text[:max_len].replace("\n", " ").replace("{", "").replace("}", "")


def build_batch_line(cmd: str, **kwargs) -> str:
    """Build a batch command line, skipping None values. Quote values with spaces."""
    parts = [cmd]
    for k, v in kwargs.items():
        if v is not None:
            sv = str(v)
            parts.append(f'{k}="{sv}"' if " " in sv else f"{k}={sv}")
    return " ".join(parts)
