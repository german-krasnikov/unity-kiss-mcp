from mcp.types import ToolAnnotations

RO = ToolAnnotations(readOnlyHint=True)
RW = ToolAnnotations(readOnlyHint=False)
RW_IDEM = ToolAnnotations(readOnlyHint=False, idempotentHint=True)
DEL = ToolAnnotations(readOnlyHint=False, destructiveHint=True)
