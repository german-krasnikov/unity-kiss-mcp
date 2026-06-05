"""Shared utilities."""


def parse_kv_line(line: str) -> tuple[str, dict[str, str]]:
    parts = line.split()
    if not parts:
        return "", {}
    cmd = parts[0]
    kv: dict[str, str] = {}
    for p in parts[1:]:
        if "=" in p:
            k, v = p.split("=", 1)
            kv[k] = v
    return cmd, kv


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
