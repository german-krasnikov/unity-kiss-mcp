"""Shared utilities."""
import re

# Paren group matches flat tuples: (1, 0, 0). Nested parens NOT supported.
_KV_RE = re.compile(r'(\w+)=("(?:[^"\\]|\\.)*"|\((?:[^)]*)\)|[^\s]+)')


def parse_kv(text: str) -> dict[str, str]:
    """Parse key=value pairs. Handles quotes and parens: value=(1, 0, 0)"""
    return {m.group(1): m.group(2).strip('"') for m in _KV_RE.finditer(text)}


def parse_kv_line(line: str) -> tuple[str, dict[str, str]]:
    """Extract command + kv dict from a batch line."""
    stripped = line.strip()
    if not stripped:
        return "", {}
    first_kv = _KV_RE.search(stripped)
    if not first_kv:
        return stripped.split()[0], {}
    cmd = stripped[: first_kv.start()].strip()
    return (cmd.split()[0] if cmd else ""), parse_kv(stripped[first_kv.start() :])


def _levenshtein(a: str, b: str) -> int:
    if a == b:
        return 0
    if not a:
        return len(b)
    if not b:
        return len(a)
    dp = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        prev = dp[0]
        dp[0] = i
        for j, cb in enumerate(b, 1):
            cur = dp[j]
            dp[j] = prev if ca == cb else 1 + min(prev, dp[j - 1], dp[j])
            prev = cur
    return dp[-1]
