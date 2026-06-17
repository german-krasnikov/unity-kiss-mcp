"""permission_prompt — --permission-prompt-tool MCP handler for Claude CLI."""
import json
from ._annotations import RO as _RO

_send = None


async def permission_prompt(tool_name: str, input: dict, tool_use_id: str):
    """Handle Claude permission prompts via MCP.

    Registered as --permission-prompt-tool so Claude routes all permission
    checks here instead of blocking on stdin.
    """
    if tool_name == "AskUserQuestion":
        try:
            questions = input.get("questions", [])
            answers_raw = await _send("ask_user", {"questions": json.dumps(questions)}, timeout=300.0)
            answers = json.loads(answers_raw)
            return json.dumps({
                "behavior": "allow",
                "updatedInput": {"questions": questions, "answers": answers},
            })
        except Exception:
            return json.dumps({"behavior": "deny", "message": "Unity not connected or user dismissed"})
    return json.dumps({"behavior": "allow", "updatedInput": input})


def register(mcp, send, args):
    global _send
    _send = send
    mcp.tool(annotations=_RO)(permission_prompt)
