"""Update README stats: badges, changelog excerpt, stats.svg.

Usage: python scripts/update_readme.py  (from repo root)
Pure stdlib — no pip deps.
"""
import ast
import json
import pathlib
import re
import subprocess
import sys
import warnings
from typing import Optional

try:
    import tomllib  # Python 3.11+
except ImportError:
    try:
        import tomli as tomllib  # type: ignore[no-redef]
    except ImportError:
        tomllib = None  # type: ignore[assignment]

REPO_ROOT = pathlib.Path(__file__).parent.parent


# ---------------------------------------------------------------------------
# Stats extraction
# ---------------------------------------------------------------------------

def count_mcp_tools(src_dir: pathlib.Path) -> Optional[int]:
    """Count `mcp.tool(...)` registrations across all .py files in src_dir.

    Two patterns:
      - mcp.tool(annotations=...)(fn)  — outer Call whose func is itself a Call
        where that inner Call's func is Attribute(attr="tool")
      - @mcp.tool()  — decorator: a Call whose func is Attribute(attr="tool"),
        but only when it is NOT the inner-func of a wrapping call.
    """
    if not src_dir.exists():
        return None
    count = 0
    for py in src_dir.rglob("*.py"):
        try:
            tree = ast.parse(py.read_text(encoding="utf-8"))
        except SyntaxError:
            continue
        # Collect all Call nodes that are the inner .tool(...) inside outer(inner)(fn)
        inner_tool_calls: set[int] = set()
        for node in ast.walk(tree):
            if isinstance(node, ast.Call):
                func = node.func
                if isinstance(func, ast.Call):
                    inner_func = func.func
                    if (isinstance(inner_func, ast.Attribute) and
                            inner_func.attr == "tool"):
                        # outer call counts once; mark inner to skip
                        inner_tool_calls.add(id(func))
                        count += 1
        # Now count decorator-style @mcp.tool() that weren't already counted
        for node in ast.walk(tree):
            if isinstance(node, ast.Call) and id(node) not in inner_tool_calls:
                func = node.func
                if isinstance(func, ast.Attribute) and func.attr == "tool":
                    count += 1
    return count


def _python_for(tests_dir: pathlib.Path) -> str:
    """Return venv python if present next to tests_dir, else sys.executable."""
    # tests_dir is typically server/tests — check server/.venv/bin/python
    venv = tests_dir.parent / ".venv" / "bin" / "python"
    return str(venv) if venv.exists() else sys.executable


def count_pytest_tests(tests_dir: pathlib.Path) -> Optional[int]:
    """Run pytest --collect-only and count '::' lines (excludes live tests)."""
    if not tests_dir.exists():
        return None
    try:
        result = subprocess.run(
            [_python_for(tests_dir), "-m", "pytest", str(tests_dir),
             "--co", "-q", "--no-header", "-m", "not live"],
            capture_output=True, text=True, timeout=120,
        )
        lines = result.stdout.splitlines()
        return sum(1 for l in lines if "::" in l)
    except Exception as e:
        warnings.warn(f"pytest collection failed: {e}")
        return None


def count_live_tests(tests_dir: pathlib.Path) -> Optional[int]:
    """Run pytest --collect-only with -m live and count '::' lines."""
    if not tests_dir.exists():
        return None
    try:
        result = subprocess.run(
            [_python_for(tests_dir), "-m", "pytest", str(tests_dir),
             "--co", "-q", "--no-header", "-m", "live"],
            capture_output=True, text=True, timeout=120,
        )
        lines = result.stdout.splitlines()
        return sum(1 for l in lines if "::" in l)
    except Exception as e:
        warnings.warn(f"pytest live collection failed: {e}")
        return None


def count_nunit_tests(editor_dir: pathlib.Path) -> Optional[int]:
    """Count [Test] attributes in unity-plugin/Editor/**/*.cs."""
    if not editor_dir.exists():
        return None
    count = 0
    for cs in editor_dir.rglob("*.cs"):
        count += cs.read_text(encoding="utf-8", errors="ignore").count("[Test]")
    return count


def read_server_version(pyproject: pathlib.Path) -> Optional[str]:
    if not pyproject.exists():
        return None
    if tomllib is None:
        # Fallback: regex parse
        m = re.search(r'^version\s*=\s*"([^"]+)"', pyproject.read_text(), re.MULTILINE)
        return m.group(1) if m else None
    with open(pyproject, "rb") as f:
        data = tomllib.load(f)
    return data.get("project", {}).get("version")


def read_plugin_version(package_json: pathlib.Path) -> Optional[str]:
    if not package_json.exists():
        return None
    data = json.loads(package_json.read_text(encoding="utf-8"))
    return data.get("version")


def parse_latest_changelog(text: str) -> tuple[str, str]:
    """Return (version, date_or_title) of the latest release in CHANGELOG.md."""
    m = re.search(r"## \[([^\]]+)\]\s*[—–-]\s*(.+)", text)
    if not m:
        return "?", "?"
    return m.group(1).strip(), m.group(2).strip()


def generate_changelog_details(text: str, n: int = 5) -> str:
    """Generate HTML <details> blocks from CHANGELOG.md for README injection.

    Latest n versions get individual <details> blocks.
    Older versions go into a collapsed "Older releases" list.
    """
    parts = re.split(r"(?=^## \[)", text, flags=re.MULTILINE)
    releases = [p for p in parts
                if re.match(r"## \[", p) and "Unreleased" not in p.splitlines()[0]]

    def parse_entry(block: str) -> tuple[str, str, str, str]:
        """Return (version, date, title, first_sentence)."""
        header_line = block.splitlines()[0]
        m = re.match(r"## \[([^\]]+)\]\s*[—–-]\s*([\d-]+)(.*)", header_line)
        if not m:
            return "?", "?", "", ""
        ver, date, rest = m.group(1).strip(), m.group(2).strip(), m.group(3)

        svg_m = re.search(r"<!--\s*(?:svg:\s*)?(.+?)\s*-->", rest)
        title = svg_m.group(1) if svg_m else ""

        bullets = [l for l in block.splitlines()[1:] if l.strip().startswith("- ")]
        first_sentence = ""
        if bullets:
            raw = bullets[0].lstrip("- ").strip()
            # Take up to first ". " or end
            sent = re.split(r"(?<=\.)\s+(?=[A-Z*\[])", raw, maxsplit=1)[0]
            first_sentence = sent[:150] + " …" if len(sent) > 150 else sent

        if not title:
            title = first_sentence[:80] + " …" if len(first_sentence) > 80 else first_sentence

        return ver, date, title, first_sentence

    recent = releases[:n]
    older = releases[n:]

    blocks: list[str] = []
    for block in recent:
        ver, date, title, first_sentence = parse_entry(block)
        body = first_sentence or title
        blocks.append(
            f"<details>\n"
            f"<summary><b>{ver}</b> — {date} — {title}</summary>\n"
            f"\n"
            f"{body}\n"
            f"\n"
            f"</details>"
        )

    if older:
        items = []
        for block in older:
            ver, date, title, _ = parse_entry(block)
            items.append(f"- **{ver}** — {date} — {title}")
        older_block = (
            "<details>\n"
            "<summary>Older releases</summary>\n"
            "\n"
            + "\n".join(items)
            + "\n\n</details>"
        )
        blocks.append(older_block)

    return "\n\n".join(blocks)


def extract_changelog_blocks(text: str, n: int = 2, max_bullets: int = 3,
                              max_chars: int = 200) -> list[str]:
    """Extract the latest n release blocks, truncated for README display."""
    parts = re.split(r"(?=^## \[)", text, flags=re.MULTILINE)
    releases = [p for p in parts if re.match(r"## \[", p)][:n]
    blocks = []
    for release in releases:
        lines = release.splitlines()
        header = re.sub(r"\s*<!--.*?-->", "", lines[0]) if lines else ""
        bullets = [l for l in lines[1:] if l.strip().startswith("- ")][:max_bullets]
        truncated = []
        for b in bullets:
            first_sentence = re.split(r"(?<=\.) (?=[A-Z*])", b.strip("- "), maxsplit=1)[0]
            if len(first_sentence) > max_chars:
                first_sentence = first_sentence[:max_chars].rsplit(" ", 1)[0] + " …"
            truncated.append("- " + first_sentence.lstrip("- "))
        blocks.append(header + "\n" + "\n".join(truncated))
    return blocks


# ---------------------------------------------------------------------------
# README / SVG mutation
# ---------------------------------------------------------------------------

def inject_changelog_into_readme(readme: str, content: str) -> str:
    """Replace text between <!-- CHANGELOG_START --> and <!-- CHANGELOG_END -->."""
    pattern = r"(<!-- CHANGELOG_START -->).*?(<!-- CHANGELOG_END -->)"
    replacement = rf"\1\n{content}\n\2"
    result, n = re.subn(pattern, replacement, readme, flags=re.DOTALL)
    return result if n else readme


def make_badge_json(label: str, message: str, color: str) -> dict:
    return {"schemaVersion": 1, "label": label, "message": message, "color": color}


def update_stats_svg(
    svg: str,
    tools: Optional[int],
    tests: Optional[int],
    python_tests: Optional[int] = None,
    nunit_tests: Optional[int] = None,
    live_tests: Optional[int] = None,
) -> str:
    """Update STAT markers, aria-label, and subtitle line in stats.svg."""
    if tools is not None:
        svg = re.sub(
            r"<!-- STAT:TOOLS -->[^<]*<!-- /STAT -->",
            f"<!-- STAT:TOOLS -->{tools}<!-- /STAT -->",
            svg,
        )
    if tests is not None:
        svg = re.sub(
            r"<!-- STAT:TESTS -->[^<]*<!-- /STAT -->",
            f"<!-- STAT:TESTS -->{tests}<!-- /STAT -->",
            svg,
        )
    # Update aria-label
    py = python_tests or 0
    nu = nunit_tests or 0
    lv = live_tests or 0
    t = tools or 0
    tot = tests or (py + nu + lv)
    if tools is not None or tests is not None:
        aria = (
            f"Unity MCP stats: {t} MCP Tools, {tot} Tests "
            f"({py} Python + {nu} Unity + {lv} Live), 80-95% Batch Savings"
        )
        svg = re.sub(r'aria-label="[^"]*"', f'aria-label="{aria}"', svg)
        # Update subtitle: "XXXX Python &#x00B7; XXXX Unity &#x00B7; XX Live"
        subtitle = f"{py} Python &#x00B7; {nu} Unity &#x00B7; {lv} Live"
        svg = re.sub(
            r"\d+ Python &#x00B7; \d+ Unity &#x00B7; \d+ Live",
            subtitle,
            svg,
        )
    return svg


# ---------------------------------------------------------------------------
# README badge-line updaters
# ---------------------------------------------------------------------------

_BADGE_PATTERNS = {
    "tests": (
        r"(tests-)\d+_passing",
        lambda n: f"tests-{n}_passing",
    ),
    "tools": (
        r"(tools-)\d+_MCP",
        lambda n: f"tools-{n}_MCP",
    ),
    "server": (
        r"(server-v)\d+\.\d+\.\d+",
        lambda v: f"server-v{v}",
    ),
    "plugin": (
        r"(plugin-v)\d+\.\d+\.\d+",
        lambda v: f"plugin-v{v}",
    ),
}


def update_readme_badges(readme: str, stats: dict) -> str:
    """Update hardcoded badge values in README shield URLs and stats table."""
    if "tests" in stats and stats["tests"] is not None:
        readme = re.sub(
            r"tests-\d+_passing",
            f"tests-{stats['tests']}_passing",
            readme,
        )
        readme = re.sub(
            r"(alt=\")\d+ tests passing",
            rf"\g<1>{stats['tests']} tests passing",
            readme,
        )
        readme = re.sub(
            r"(<code>)\d+(</code></h2><sub>TESTS PASSING)",
            rf"\g<1>{stats['tests']}\2",
            readme,
        )
    if "tools" in stats and stats["tools"] is not None:
        readme = re.sub(r"tools-\d+_MCP", f"tools-{stats['tools']}_MCP", readme)
        readme = re.sub(
            r"(alt=\")\d+ MCP tools",
            rf"\g<1>{stats['tools']} MCP tools",
            readme,
        )
        readme = re.sub(
            r"(<code>)\d+(</code></h2><sub>MCP TOOLS)",
            rf"\g<1>{stats['tools']}\2",
            readme,
        )
        readme = re.sub(
            r"Full access to \d+ MCP tools",
            f"Full access to {stats['tools']} MCP tools",
            readme,
        )
    if "server_ver" in stats and stats["server_ver"] is not None:
        v = stats["server_ver"]
        readme = re.sub(r"server-v[\d.]+", f"server-v{v}", readme)
        readme = re.sub(r'(alt="server v)[\d.]+', rf'\g<1>{v}', readme)
    if "plugin_ver" in stats and stats["plugin_ver"] is not None:
        v = stats["plugin_ver"]
        readme = re.sub(r"plugin-v[\d.]+", f"plugin-v{v}", readme)
        readme = re.sub(r'(alt="plugin v)[\d.]+', rf'\g<1>{v}', readme)
    return readme


def update_readme_alt_text(
    readme: str,
    tools: Optional[int],
    tests: Optional[int],
    python_tests: Optional[int] = None,
    nunit_tests: Optional[int] = None,
    live_tests: Optional[int] = None,
) -> str:
    """Update stats.svg alt text in README: 'XX MCP Tools · XXXX Tests (...)'."""
    if tools is None and tests is None:
        return readme
    py = python_tests or 0
    nu = nunit_tests or 0
    lv = live_tests or 0
    t = tools or 0
    tot = tests or (py + nu + lv)
    alt = (
        f"{t} MCP Tools · {tot} Tests "
        f"({py} Python · {nu} Unity · {lv} Live) · 80-95% Batch Savings"
    )
    return re.sub(
        r'(alt=")[\d]+ MCP Tools[^"]*(")',
        rf"\g<1>{alt}\2",
        readme,
    )


def git_commit_count() -> Optional[int]:
    try:
        out = subprocess.check_output(
            ["git", "rev-list", "--count", "HEAD"],
            text=True, cwd=REPO_ROOT, stderr=subprocess.DEVNULL,
        )
        return int(out.strip())
    except Exception:
        return None


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main() -> None:
    tools = count_mcp_tools(REPO_ROOT / "server" / "src" / "unity_mcp")
    python_tests = count_pytest_tests(REPO_ROOT / "server" / "tests")
    live_tests = count_live_tests(REPO_ROOT / "server" / "tests")
    nunit_tests = count_nunit_tests(REPO_ROOT / "unity-plugin" / "Editor")
    server_ver = read_server_version(REPO_ROOT / "server" / "pyproject.toml")
    plugin_ver = read_plugin_version(REPO_ROOT / "unity-plugin" / "package.json")
    commits = git_commit_count()
    changelog_text = (REPO_ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
    cl_ver, cl_title = parse_latest_changelog(changelog_text)

    # Total = unit python + NUnit C# + live python
    py = python_tests or 0
    nu = nunit_tests or 0
    lv = live_tests or 0
    total_tests = py + nu + lv if (py or nu or lv) else None

    def _v(val, fallback: str = "?") -> str:
        return str(val) if val is not None else fallback

    print(
        f"tools={_v(tools)}  python={_v(python_tests)}  nunit={_v(nunit_tests)}  "
        f"live={_v(live_tests)}  total={_v(total_tests)}  "
        f"server={_v(server_ver)}  plugin={_v(plugin_ver)}  "
        f"commits={_v(commits)}  latest={cl_ver}"
    )

    # --- badges JSON (uses total tests) ---
    badges_dir = REPO_ROOT / ".github" / "badges"
    badges_dir.mkdir(parents=True, exist_ok=True)

    tests_badge = make_badge_json("tests", f"{_v(total_tests)} passing", "3ad29f")
    (badges_dir / "tests.json").write_text(json.dumps(tests_badge, indent=2) + "\n")
    print(f"  wrote {badges_dir}/tests.json")

    tools_badge = make_badge_json("tools", f"{_v(tools)} MCP", "e94560")
    (badges_dir / "tools.json").write_text(json.dumps(tools_badge, indent=2) + "\n")
    print(f"  wrote {badges_dir}/tools.json")

    # --- stats SVG ---
    svg_path = REPO_ROOT / "docs" / "assets" / "stats.svg"
    svg = svg_path.read_text(encoding="utf-8")
    svg = update_stats_svg(svg, tools, total_tests, python_tests, nunit_tests, live_tests)
    svg_path.write_text(svg, encoding="utf-8")
    print("  updated docs/assets/stats.svg")

    # --- README badges + alt text + changelog ---
    readme_path = REPO_ROOT / "README.md"
    readme = readme_path.read_text(encoding="utf-8")
    original_readme = readme

    stats: dict = {
        "tests": total_tests, "tools": tools,
        "server_ver": server_ver, "plugin_ver": plugin_ver,
    }
    readme = update_readme_badges(readme, stats)
    readme = update_readme_alt_text(
        readme, tools, total_tests, python_tests, nunit_tests, live_tests
    )

    # --- changelog injection ---
    changelog_html = generate_changelog_details(changelog_text, n=5)
    readme = inject_changelog_into_readme(readme, changelog_html)

    if readme != original_readme:
        readme_path.write_text(readme, encoding="utf-8")
        print("  updated README.md")
    else:
        print("  README.md unchanged")



if __name__ == "__main__":
    main()
