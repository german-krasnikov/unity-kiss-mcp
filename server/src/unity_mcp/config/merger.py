"""Patch only mcpServers['unity-mcp'] in an existing config file."""
import json
import pathlib


def merge_mcp_config(config_path: pathlib.Path, server_entry: dict) -> None:
    """Read → parse → patch unity-mcp entry → write. Creates file if missing."""
    if config_path.exists():
        try:
            data = json.loads(config_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            data = {}
    else:
        config_path.parent.mkdir(parents=True, exist_ok=True)
        data = {}

    data.setdefault("mcpServers", {})
    data["mcpServers"]["unity-mcp"] = server_entry

    tmp = config_path.with_suffix(".tmp")
    tmp.write_text(json.dumps(data, indent=2), encoding="utf-8")
    import os
    os.replace(str(tmp), str(config_path))
