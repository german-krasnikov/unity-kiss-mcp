"""Guard tests: _UnstructuredMCP must never emit output_schema for any tool."""
import pytest
from unity_mcp.server import mcp, _UnstructuredMCP


def test_tool_count_not_zero():
    assert len(mcp._tool_manager._tools) >= 90


def test_no_tool_has_output_schema():
    schemas = {
        name: tool.output_schema
        for name, tool in mcp._tool_manager._tools.items()
        if tool.output_schema is not None
    }
    assert schemas == {}, f"Tools with output_schema: {list(schemas)}"


def test_structured_output_true_override_produces_no_schema():
    from mcp.server.fastmcp import FastMCP as _BaseFastMCP
    base = _BaseFastMCP("base")

    @base.tool(structured_output=True)
    def base_tool() -> str:
        return "x"

    assert base._tool_manager._tools["base_tool"].output_schema is not None

    # Even when caller passes structured_output=True, subclass forces False.
    instance = _UnstructuredMCP("test")

    @instance.tool(structured_output=True)
    def my_tool() -> str:
        return "hello"

    assert instance._tool_manager._tools["my_tool"].output_schema is None


async def test_list_tools_response_has_no_output_schema():
    tools = await mcp.list_tools()
    assert tools, "list_tools returned empty"
    bad = [t.name for t in tools if t.outputSchema is not None]
    assert bad == [], f"Tools with outputSchema in list_tools: {bad}"
