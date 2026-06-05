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


def count_pytest_tests(tests_dir: pathlib.Path) -> Optional[int]:
    """Run pytest --collect-only and count '::' lines."""
    if not tests_dir.exists():
        return None
    try:
        result = subprocess.run(
            [sys.executable, "-m", "pytest", str(tests_dir),
             "--co", "-q", "--no-header", "-m", "not live"],
            capture_output=True, text=True, timeout=120,
        )
        lines = result.stdout.splitlines()
        return sum(1 for l in lines if "::" in l)
    except Exception as e:
        warnings.warn(f"pytest collection failed: {e}")
        return None


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


def update_stats_svg(svg: str, tools: Optional[int], tests: Optional[int]) -> str:
    """Replace <!-- STAT:TOOLS -->N<!-- /STAT --> and <!-- STAT:TESTS -->N<!-- /STAT -->."""
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
    if "server_ver" in stats and stats["server_ver"] is not None:
        v = stats["server_ver"]
        readme = re.sub(r"server-v[\d.]+", f"server-v{v}", readme)
        readme = re.sub(r'(alt="server v)[\d.]+', rf'\g<1>{v}', readme)
    if "plugin_ver" in stats and stats["plugin_ver"] is not None:
        v = stats["plugin_ver"]
        readme = re.sub(r"plugin-v[\d.]+", f"plugin-v{v}", readme)
        readme = re.sub(r'(alt="plugin v)[\d.]+', rf'\g<1>{v}', readme)
    return readme


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
    tests = count_pytest_tests(REPO_ROOT / "server" / "tests")
    server_ver = read_server_version(REPO_ROOT / "server" / "pyproject.toml")
    plugin_ver = read_plugin_version(REPO_ROOT / "unity-plugin" / "package.json")
    commits = git_commit_count()
    changelog_text = (REPO_ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
    cl_ver, cl_title = parse_latest_changelog(changelog_text)

    def _v(val, fallback: str = "?") -> str:
        return str(val) if val is not None else fallback

    print(f"tools={_v(tools)}  tests={_v(tests)}  server={_v(server_ver)}  "
          f"plugin={_v(plugin_ver)}  commits={_v(commits)}  latest={cl_ver}")

    # --- badges JSON ---
    badges_dir = REPO_ROOT / ".github" / "badges"
    badges_dir.mkdir(parents=True, exist_ok=True)

    tests_badge = make_badge_json("tests", f"{_v(tests)} passing", "3ad29f")
    (badges_dir / "tests.json").write_text(json.dumps(tests_badge, indent=2) + "\n")
    print(f"  wrote {badges_dir}/tests.json")

    tools_badge = make_badge_json("tools", f"{_v(tools)} MCP", "e94560")
    (badges_dir / "tools.json").write_text(json.dumps(tools_badge, indent=2) + "\n")
    print(f"  wrote {badges_dir}/tools.json")

    # --- README badges ---
    readme_path = REPO_ROOT / "README.md"
    readme = readme_path.read_text(encoding="utf-8")
    original_readme = readme

    stats: dict = {
        "tests": tests, "tools": tools,
        "server_ver": server_ver, "plugin_ver": plugin_ver,
    }
    readme = update_readme_badges(readme, stats)

    # --- changelog injection ---
    blocks = extract_changelog_blocks(changelog_text, n=2, max_bullets=3)
    if blocks:
        changelog_md = "\n\n".join(blocks)
        readme = inject_changelog_into_readme(readme, changelog_md)

    if readme != original_readme:
        readme_path.write_text(readme, encoding="utf-8")
        print("  updated README.md")
    else:
        print("  README.md unchanged")



if __name__ == "__main__":
    main()
