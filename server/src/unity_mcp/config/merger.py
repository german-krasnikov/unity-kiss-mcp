"""Patch only <root_key>['unity-mcp'] in an existing config file."""
import json
import os
import pathlib
import re
import shutil
from typing import Callable, Optional


def merge_mcp_config(
    config_path: pathlib.Path,
    server_entry: dict,
    root_key: str = "mcpServers",
    entry_transformer: Optional[Callable[[dict], dict]] = None,
) -> None:
    """Read → parse → patch unity-mcp entry → write. Creates file if missing."""
    if config_path.exists():
        try:
            data = json.loads(config_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as e:
            raise ValueError(f"Corrupt JSON in {config_path}: {e}") from e
    else:
        config_path.parent.mkdir(parents=True, exist_ok=True)
        data = {}

    entry = entry_transformer(server_entry) if entry_transformer else server_entry
    data.setdefault(root_key, {})
    data[root_key]["unity-mcp"] = entry

    tmp = config_path.with_suffix(".tmp")
    tmp.write_text(json.dumps(data, indent=2), encoding="utf-8")
    os.replace(str(tmp), str(config_path))


def merge_toml_mcp(config_path: pathlib.Path, server_entry: dict) -> None:
    """Merge unity-mcp into a TOML config (Codex). Text-based, no TOML lib needed."""
    bak = config_path.with_suffix(".bak")
    if config_path.exists():
        if not bak.exists():
            shutil.copy2(config_path, bak)
        text = config_path.read_text(encoding="utf-8")
    else:
        text = ""

    # Strip stale bare [mcp_servers.unity] (no suffix) — causes "invalid transport"
    # Also strips any dotted sub-sections like [mcp_servers.unity.env].
    # \n? at end handles EOF without trailing newline.
    stale_re = re.compile(
        r'\[mcp_servers\.unity\]\n?(?:(?!\[)[^\n]*\n?)*'
        r'(?:\[mcp_servers\.unity\.[^\]]+\]\n?(?:(?!\[)[^\n]*\n?)*)*',
        re.MULTILINE,
    )
    text = stale_re.sub("", text)

    section_re = re.compile(
        r'\[mcp_servers\.unity-mcp\]\n(?:(?!\[)[^\n]*\n)*', re.MULTILINE
    )
    cmd = server_entry["command"]
    args = json.dumps(server_entry.get("args", []))
    block = f'[mcp_servers.unity-mcp]\ncommand = "{cmd}"\nargs = {args}\n'
    env = server_entry.get("env", {})
    if env:
        env_lines = "\n".join(f'{k} = "{v}"' for k, v in env.items())
        block += f'\n[mcp_servers.unity-mcp.env]\n{env_lines}\n'

    if section_re.search(text):
        text = section_re.sub(block, text)
    else:
        text = text.rstrip() + "\n\n" + block
    config_path.parent.mkdir(parents=True, exist_ok=True)
    tmp = config_path.with_suffix(".tmp")
    tmp.write_text(text, encoding="utf-8")
    os.replace(str(tmp), str(config_path))
