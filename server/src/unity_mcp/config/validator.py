"""Validate MCP config files for known clients."""
import json
import socket

from unity_mcp.config.clients import CLIENT_REGISTRY
from unity_mcp.config.resolver import find_port


def _port_reachable(port: int) -> bool:
    """Quick TCP probe — returns True if something is listening."""
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=1):
            return True
    except OSError:
        return False


def validate_config(client_key: str) -> str:
    """Check config for client. Return plain text report."""
    info = CLIENT_REGISTRY.get(client_key)
    if info is None:
        valid = ", ".join(sorted(CLIENT_REGISTRY))
        return f"Unknown client: {client_key!r}. Valid: {valid}"
    path = info.config_path
    lines = [f"Client: {info.name}", f"Config: {path}"]

    if not path.exists():
        lines.append("Status: not found")
        return "\n".join(lines)

    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        lines.append(f"Status: invalid JSON ({e})")
        return "\n".join(lines)

    servers = data.get("mcpServers", {})
    if "unity-mcp" not in servers:
        lines.append("Status: not configured (unity-mcp missing from mcpServers)")
        return "\n".join(lines)

    entry = servers["unity-mcp"]
    lines.append(f"unity-mcp entry: {entry}")

    port = find_port()
    reachable = _port_reachable(port)
    lines.append(f"Port {port}: {'reachable' if reachable else 'not reachable (Unity not running?)'}")
    return "\n".join(lines)
