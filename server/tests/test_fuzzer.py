from unity_mcp.fuzzer import generate_script, shrink_script


def test_generate_script_has_invariants():
    script = generate_script()
    assert "INVARIANT" in script


def test_generate_script_deterministic_with_seed():
    a = generate_script(num_steps=5, seed=42)
    b = generate_script(num_steps=5, seed=42)
    assert a == b


def test_generate_script_different_seeds_differ():
    a = generate_script(num_steps=5, seed=1)
    b = generate_script(num_steps=5, seed=2)
    assert a != b


def test_generate_script_step_count():
    script = generate_script(num_steps=7, seed=0)
    # Count non-invariant, non-timescale lines
    lines = [l for l in script.split("\n") if not l.startswith("INVARIANT") and not l.startswith("TIMESCALE")]
    assert len(lines) == 8  # 7 actions + 1 ASSERT_CONSOLE_CLEAN at end


def test_shrink_reduces_size():
    script = generate_script(num_steps=20, seed=99)
    shrunk = shrink_script(script, failing_step=5)
    assert len(shrunk.split("\n")) < len(script.split("\n"))


def test_shrink_preserves_invariants():
    script = generate_script(num_steps=10, seed=7)
    shrunk = shrink_script(script, failing_step=3)
    assert "INVARIANT" in shrunk
