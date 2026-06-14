"""Pure render: reads _meta.json, writes README/SVG/badges. Stdlib only."""
import json, pathlib, re, sys
from typing import Optional

REPO_ROOT = pathlib.Path(__file__).parent.parent


def substitute_svg_markers(svg: str, meta: dict) -> str:
    """Replace STAT markers. Idempotent."""
    tools, tests = meta.get("tools"), meta.get("tests_total")
    if tools is not None:
        svg = re.sub(r"<!-- STAT:TOOLS -->[^<]*<!-- /STAT -->",
                     f"<!-- STAT:TOOLS -->{tools}<!-- /STAT -->", svg)
    if tests is not None:
        svg = re.sub(r"<!-- STAT:TESTS -->[^<]*<!-- /STAT -->",
                     f"<!-- STAT:TESTS -->{tests}<!-- /STAT -->", svg)
    p, u, lv = meta.get("tests_python"), meta.get("tests_unity"), meta.get("tests_live")
    if p is not None and u is not None and lv is not None:
        bd_card = f"{p} Python &#x00B7; {u} Unity &#x00B7; {lv} Live"
        svg = re.sub(r"<!-- STAT:BREAKDOWN -->[^<]*<!-- /STAT -->",
                     f"<!-- STAT:BREAKDOWN -->{bd_card}<!-- /STAT -->", svg)
        bd_aria = f"({p} Python + {u} Unity + {lv} Live)"
        svg = re.sub(r"\(\d+ Python \+ \d+ Unity \+ \d+ Live\)", bd_aria, svg)
    if tools is not None:
        svg = re.sub(r"\b\d+ MCP [Tt]ools\b", f"{tools} MCP tools", svg)
        svg = re.sub(r"\b\d+ token-minimized tools\b", f"{tools} token-minimized tools", svg)
    if tests is not None:
        svg = re.sub(r"\b\d+ Tests?\b", f"{tests} Tests", svg)
    return svg


def update_readme_badges(readme: str, stats: dict) -> str:
    """Update badge shield URLs and alt text."""
    tests = stats.get("tests") or stats.get("tests_total")
    tools = stats.get("tools")
    server_ver = stats.get("server_ver") or stats.get("server_version")
    plugin_ver = stats.get("plugin_ver") or stats.get("plugin_version")
    if tests is not None:
        readme = re.sub(r'(alt=")\d+ tests passing', rf"\g<1>{tests} tests passing", readme)
        readme = re.sub(r"(<code>)\d+(</code></h2><sub>TESTS PASSING)", rf"\g<1>{tests}\2", readme)
    if tools is not None:
        readme = re.sub(r'(alt=")\d+ MCP tools', rf"\g<1>{tools} MCP tools", readme)
        readme = re.sub(r"(<code>)\d+(</code></h2><sub>MCP TOOLS)", rf"\g<1>{tools}\2", readme)
    if server_ver is not None:
        readme = re.sub(r'(alt="server v)[\d.]+', rf'\g<1>{server_ver}', readme)
    if plugin_ver is not None:
        readme = re.sub(r'(alt="plugin v)[\d.]+', rf'\g<1>{plugin_ver}', readme)
    return readme


def _substitute_readme_tool_counts(readme: str, meta: dict) -> str:
    """C1: 3 hard-coded tool-count spots not covered by badge regexes."""
    tools, tests = meta.get("tools"), meta.get("tests_total")
    if tools is not None:
        readme = re.sub(r'(\d+)( token-minimized MCP tools)',
                        lambda m: f'{tools}{m.group(2)}', readme)
        readme = re.sub(r'(Full access to )(\d+)( MCP tools)',
                        lambda m: f'{m.group(1)}{tools}{m.group(3)}', readme)
        readme = re.sub(r'(MCP Server with )(\d+)( tools)',
                        lambda m: f'{m.group(1)}{tools}{m.group(3)}', readme)
    if tests is not None:
        readme = re.sub(r'(\d+)( tests passing)(?![\w-])',
                        lambda m: f'{tests}{m.group(2)}', readme)
    return readme


def read_meta_json(repo_root: pathlib.Path) -> dict:
    return json.loads((repo_root / "docs" / "assets" / "_meta.json").read_text(encoding="utf-8"))


def parse_latest_changelog(text: str) -> tuple[str, str]:
    """Return (version, date_or_title) of the latest DATED release."""
    m = re.search(r"## \[([^\]]+)\]\s*[—–-]\s*(.+)", text)
    if not m:
        return "?", "?"
    return m.group(1).strip(), m.group(2).strip()


def generate_changelog_details(text: str, n: int = 5) -> str:
    """HTML <details> blocks from CHANGELOG, skipping [Unreleased]."""
    parts = re.split(r"(?=^## \[)", text, flags=re.MULTILINE)
    releases = [p for p in parts if re.match(r"## \[v", p) and re.search(r"\d{4}-\d{2}-\d{2}", p.splitlines()[0])]

    def _parse(block: str) -> tuple[str, str, str, str]:
        hdr = block.splitlines()[0]
        m = re.match(r"## \[([^\]]+)\]\s*[—–-]\s*([\d-]+)(.*)", hdr)
        if not m:
            return "?", "?", "", ""
        ver, date, rest = m.group(1).strip(), m.group(2).strip(), m.group(3)
        svg_m = re.search(r"<!--\s*svg:\s*(.+?)\s*-->", rest)
        title = svg_m.group(1) if svg_m else ""
        bullets = [l for l in block.splitlines()[1:] if l.strip().startswith("- ")]
        sent = ""
        if bullets:
            raw = re.split(r"(?<=\.)\s+(?=[A-Z*\[])", bullets[0].lstrip("- ").strip(), maxsplit=1)[0]
            sent = (raw[:150].rsplit(" ", 1)[0] + " …") if len(raw) > 150 else raw  # C11
        if not title:
            title = (sent[:80].rsplit(" ", 1)[0] + " …") if len(sent) > 80 else sent  # C11
        return ver, date, title, sent

    blocks: list[str] = []
    for block in releases[:n]:
        ver, date, title, sent = _parse(block)
        blocks.append(
            f"<details>\n<summary><b>{ver}</b> — {date} — {title}</summary>\n\n{sent or title}\n\n</details>"
        )
    older = releases[n:]
    if older:
        items = "\n".join(f"- **{v}** — {d} — {t}" for block in older for v, d, t, _ in [_parse(block)])
        blocks.append(f"<details>\n<summary>Older releases</summary>\n\n{items}\n\n</details>")
    return "\n\n".join(blocks)


def extract_changelog_blocks(text: str, n: int = 2, max_bullets: int = 3, max_chars: int = 200) -> list[str]:
    """Extract the latest n release blocks, truncated."""
    parts = re.split(r"(?=^## \[)", text, flags=re.MULTILINE)
    releases = [p for p in parts if re.match(r"## \[", p)][:n]
    blocks = []
    for release in releases:
        lines = release.splitlines()
        header = re.sub(r"\s*<!--.*?-->", "", lines[0]) if lines else ""
        bullets = [l for l in lines[1:] if l.strip().startswith("- ")][:max_bullets]
        truncated = []
        for b in bullets:
            fs = re.split(r"(?<=\.) (?=[A-Z*])", b.strip("- "), maxsplit=1)[0]
            if len(fs) > max_chars:
                fs = fs[:max_chars].rsplit(" ", 1)[0] + " …"
            truncated.append("- " + fs.lstrip("- "))
        blocks.append(header + "\n" + "\n".join(truncated))
    return blocks


def inject_changelog_into_readme(readme: str, content: str) -> str:
    result, n = re.subn(
        r"(<!-- CHANGELOG_START -->).*?(<!-- CHANGELOG_END -->)",
        rf"\1\n{content}\n\2", readme, flags=re.DOTALL)
    return result if n else readme

def make_badge_json(label: str, message: str, color: str) -> dict:
    return {"schemaVersion": 1, "label": label, "message": message, "color": color}


def _apply_or_check(changes: list[tuple[pathlib.Path, str]], check: bool) -> None:
    stale: list[str] = []
    for path, new_content in changes:
        if path.exists() and path.read_text(encoding="utf-8") == new_content:
            if not check:
                print(f"  unchanged {path.name}")
            continue
        if check:
            stale.append(str(path))
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(new_content, encoding="utf-8")
            print(f"  updated {path.name}")
    if check:
        if stale:
            print("STALE (run python scripts/update_readme.py --render to fix):")
            for p in stale:
                print(f"  {p}")
            sys.exit(1)
        else:
            print("OK — all generated files up to date")


def render(repo_root: pathlib.Path, meta: dict, check: bool = False) -> list[pathlib.Path]:
    """Regenerate all outputs from meta. Returns list of processed file paths."""
    _v = lambda val, fb="?": str(val) if val is not None else fb  # noqa: E731
    changes: list[tuple[pathlib.Path, str]] = []
    badges_dir = repo_root / ".github" / "badges"
    badges_dir.mkdir(parents=True, exist_ok=True)
    _bj = lambda lbl, msg, c: json.dumps(make_badge_json(lbl, msg, c), indent=2) + "\n"  # noqa: E731
    changes.append((badges_dir / "tests.json", _bj("tests", f"{_v(meta.get('tests_total'))} passing", "3ad29f")))
    changes.append((badges_dir / "tools.json", _bj("tools", f"{_v(meta.get('tools'))} MCP", "e94560")))

    readme_path = repo_root / "README.md"
    readme = _substitute_readme_tool_counts(update_readme_badges(readme_path.read_text(encoding="utf-8"), meta), meta)
    tools_n: Optional[int] = meta.get("tools")
    tests_n: Optional[int] = meta.get("tests_total")
    if tools_n is not None and tests_n is not None:  # C8
        readme = re.sub(r'(alt=")[^"]*MCP Tools[^"]*(")', lambda m: (
            f'{m.group(1)}{tools_n} MCP Tools · {tests_n} Tests '
            f'({meta.get("tests_python", 0)} Python · {meta.get("tests_unity", 0)} Unity · '
            f'{meta.get("tests_live", 0)} Live) · {meta.get("batch_savings", "80–95%")} Batch Savings{m.group(2)}'
        ), readme)
    cl = (repo_root / "CHANGELOG.md").read_text(encoding="utf-8")
    changes.append((readme_path, inject_changelog_into_readme(readme, generate_changelog_details(cl, n=5))))

    for svg_path in (repo_root / "docs" / "assets").glob("*.svg"):
        if svg_path.name.startswith("_"):
            continue
        original = svg_path.read_text(encoding="utf-8")
        updated = substitute_svg_markers(original, meta)
        if updated != original:
            changes.append((svg_path, updated))

    _apply_or_check(changes, check=check)
    return [path for path, _ in changes]
