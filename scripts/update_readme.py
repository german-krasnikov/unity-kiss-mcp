"""Update README stats: badges, changelog excerpt, stats/hero/architecture SVGs.

CLI:
  --collect   collect_facts + write docs/assets/_meta.json (needs venv/env)
  --render    read _meta.json + regenerate outputs (CI-safe, no pip)
  --all       collect then render (default)
  --check     render in-memory, exit 1 if any file is stale
"""
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))

REPO_ROOT = pathlib.Path(__file__).parent.parent

# ---------------------------------------------------------------------------
# Re-exports from readme_render (pure stdlib — always safe to import)
# ---------------------------------------------------------------------------
from readme_render import (  # noqa: E402
    substitute_svg_markers,
    update_readme_badges,
    inject_changelog_into_readme,
    make_badge_json,
    generate_changelog_details,
    extract_changelog_blocks,
    parse_latest_changelog,
    render,
    _apply_or_check,
    read_meta_json,
)

# backward compat: update_stats_svg was in old update_readme
def update_stats_svg(svg: str, tools, tests) -> str:
    return substitute_svg_markers(svg, {"tools": tools, "tests_total": tests})


def git_commit_count():
    import subprocess
    try:
        out = subprocess.check_output(
            ["git", "rev-list", "--count", "HEAD"],
            text=True, cwd=REPO_ROOT, stderr=subprocess.DEVNULL,
        )
        return int(out.strip())
    except Exception:
        return None


# ---------------------------------------------------------------------------
# C4: readme_facts re-exports via lazy __getattr__ — not imported at module load
# ---------------------------------------------------------------------------
_FACTS_ATTRS = {"count_mcp_tools", "read_server_version", "read_plugin_version",
                "count_pytest_python", "count_pytest_live", "count_unity_tests"}


def __getattr__(name: str):
    if name in _FACTS_ATTRS:
        import readme_facts as _rf
        return getattr(_rf, name)
    raise AttributeError(f"module 'update_readme' has no attribute {name!r}")


# ---------------------------------------------------------------------------
# Main CLI
# ---------------------------------------------------------------------------

def main() -> None:
    import argparse
    ap = argparse.ArgumentParser(description="Update README/SVG stats from single source.")
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--collect", action="store_true", help="Collect live facts + write _meta.json")
    g.add_argument("--render", action="store_true", help="Read _meta.json + regenerate outputs")
    g.add_argument("--check", action="store_true", help="Render in-memory, exit 1 if stale")
    g.add_argument("--all", dest="all_", action="store_true", help="collect then render (default)")
    args = ap.parse_args()

    do_collect = args.collect or args.all_ or not any([args.collect, args.render, args.check, args.all_])
    do_render  = args.render  or args.all_ or not any([args.collect, args.render, args.check, args.all_])
    do_check   = args.check

    if do_collect:
        # C4: lazy import of readme_facts only in --collect/--all branch
        from readme_facts import collect_facts, write_meta_json
        print("Collecting facts...")
        facts = collect_facts(REPO_ROOT)
        meta_path = write_meta_json(REPO_ROOT, facts)
        print(
            f"  tools={facts['tools']}  tests={facts['tests_total']} "
            f"(python={facts['tests_python']} unity={facts['tests_unity']} live={facts['tests_live']})"
            f"  server={facts['server_version']}  plugin={facts['plugin_version']}"
        )
        print(f"  wrote {meta_path}")
    else:
        # C4: --render reads _meta.json via stdlib-only readme_render (no readme_facts)
        facts = read_meta_json(REPO_ROOT)

    if do_render:
        print("Rendering...")
        render(REPO_ROOT, facts)
        print("Done.")
    elif do_check:
        # C6: same code path as --render, just check=True
        render(REPO_ROOT, facts, check=True)


if __name__ == "__main__":
    main()
