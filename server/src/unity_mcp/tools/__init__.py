from . import scene, objects, asset, animation, connection, runtime, autobatch
from . import batch, codegen, skills, spatial, ui
from . import do_tool, ask_tool, ask_user_tool, permission_prompt_tool
from . import animator_intent_tool, vfx_intent_tool, ui_intent_tool
from . import budget_tool, code_intel, sync, diagnose, watch, debug_tool, diagnostics
from .metrics_tool import register as register_metrics
from ..debug import snapshots as snapshot_tool


def register_all(mcp, send, args, *, get_slot, get_middleware=None):
    for mod in [scene, objects, asset, animation, runtime, watch, code_intel,
                batch, codegen, skills, spatial, ui, sync, diagnose,
                debug_tool, diagnostics, snapshot_tool]:
        mod.register(mcp, send, args)
    connection.register(mcp, send, args, get_slot=get_slot,
                        get_middleware=get_middleware)
    autobatch.register(mcp, send, args)
    do_tool.register(mcp, send, args)
    ask_tool.register(mcp, send, args)
    ask_user_tool.register(mcp, send, args)
    permission_prompt_tool.register(mcp, send, args)
    for mod in [animator_intent_tool, vfx_intent_tool, ui_intent_tool]:
        mod.register(mcp, send, args)
    register_metrics(mcp, send, args)
    budget_tool.register(mcp, send, args)
