"""Tests for Persistent Skill Library (Feature 3)."""
import json
import os
import pytest
from unittest.mock import AsyncMock, patch
from unity_mcp.tools.skills import save_skill, use_skill, list_skills


@pytest.fixture
def skills_dir(tmp_path, monkeypatch):
    """Use tmp dir for skills to avoid polluting real .claude/skills/learned/."""
    d = tmp_path / "learned"
    monkeypatch.setattr("unity_mcp.tools.skills._skills_dir", lambda: str(d))
    return d


@pytest.mark.asyncio
async def test_save_skill_creates_file(skills_dir):
    result = await save_skill("test_skill", "Does something", "var go = new GameObject();")
    assert result == "Skill saved: test_skill — Does something"
    path = skills_dir / "test_skill.json"
    assert path.exists()
    data = json.loads(path.read_text())
    assert data["name"] == "test_skill"
    assert data["description"] == "Does something"
    assert data["code"] == "var go = new GameObject();"
    assert data["used_count"] == 0


@pytest.mark.asyncio
async def test_use_skill_executes_code(skills_dir, mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "executed"}
    await save_skill("my_skill", "Creates obj", "var go = new GameObject();")
    result = await use_skill("my_skill")
    # C# code → execute_code
    mock_bridge.send.assert_called_once()
    call_args = mock_bridge.send.call_args[0]
    assert call_args[0] == "execute_code"
    assert "var go = new GameObject();" in call_args[1]["code"]


@pytest.mark.asyncio
async def test_use_skill_batch_detection(skills_dir, mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "batch done"}
    await save_skill("batch_skill", "Moves obj", "set_property path=Cube pos=1,2,3")
    result = await use_skill("batch_skill")
    mock_bridge.send.assert_called_once()
    call_args = mock_bridge.send.call_args[0]
    assert call_args[0] == "batch"


@pytest.mark.asyncio
async def test_use_skill_not_found_lists_available(skills_dir):
    result = await use_skill("nonexistent")
    # use_skill delegates to list_skills when name not found — always returns "No skills..."
    assert "No skills" in result, result


@pytest.mark.asyncio
async def test_list_skills_empty(skills_dir):
    result = await list_skills()
    assert result == "No skills saved yet. Use save_skill to create one."


@pytest.mark.asyncio
async def test_list_skills_with_saved(skills_dir):
    await save_skill("alpha", "Alpha skill", "var x = 1;")
    await save_skill("beta", "Beta skill", "create_object name=Test")
    result = await list_skills()
    assert "alpha" in result
    assert "Alpha skill" in result
    assert "beta" in result
    assert "0x" in result  # used_count


@pytest.mark.asyncio
async def test_use_skill_increments_count(skills_dir, mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await save_skill("countable", "Test count", "var x = 1;")
    await use_skill("countable")
    await use_skill("countable")
    path = skills_dir / "countable.json"
    data = json.loads(path.read_text())
    assert data["used_count"] == 2


@pytest.mark.asyncio
async def test_use_skill_param_substitution(skills_dir, mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await save_skill("spawn", "Spawns obj", "var go = new GameObject(\"${name}\");")
    await use_skill("spawn", params="name=Player")
    call_args = mock_bridge.send.call_args[0]
    assert "Player" in call_args[1]["code"]
    assert "${name}" not in call_args[1]["code"]
