# Roslyn Foundation — Phase B (Cycle 5c)

Phase A (Python tool wrappers) shipped in Cycle 5c. This directory is reserved
for Phase B — Unity-side C# Roslyn workspace + 3 command handlers.

## Phase B scope (not yet implemented)

Files to add:
- `RoslynLoader.cs` — DRY helper, extract `EnsureRoslyn` + `LoadAssembly` from
  existing `CodeExecutor.cs` (lines 63-92, 207-238)
- `RoslynWorkspace.cs` — singleton Compilation cache, lazy-init, drops on
  `AssemblyReloadEvents.beforeAssemblyReload`
- `FindReferencesCommand.cs` — `SymbolFinder.FindReferencesAsync` wrapper
- `CompilePreflightCommand.cs` — in-memory replace + `GetDiagnostics()`
- `SemanticAtCommand.cs` — `SemanticModel.GetSymbolInfo` at position
- `RoslynFormat.cs` — text formatters (single source of truth, must match
  `server/tests/fixtures/roslyn_responses.txt`)

## Command names (must match Python wrappers)

Register in `CommandRouter.RegisterAll()`:
- `find_references` (NOT `find_references_to` — that's a different existing tool
  for object-reference traversal)
- `compile_preflight`
- `semantic_at`

## Workspace strategy

Per architect spec: use `AdhocWorkspace` + `CompilationPipeline.GetAssemblies()`,
NOT `MSBuildWorkspace` (Mono incompatibilities under Unity).

## Output format

Strict text format defined in `server/tests/fixtures/roslyn_responses.txt`.
Python tools pass C# text verbatim to agents. Format drift breaks parsing.

## Testing

- C# NUnit: `unity-test-project/Assets/Tests/Editor/RoslynTests.cs` — manual
  Unity Test Runner. NOT runnable in Python-only CI.
- Python live: `server/tests/live/test_roslyn.py` — opt-in via `UNITY_MCP_LIVE=1`,
  smoke tests against actual Unity Editor on :9500.

## Backward compat

- New commands, no breaking changes
- `CodeExecutor.cs` refactor: extract method only, public surface unchanged
- 3 tool names already in `gating.py:TIER1` — Phase A enables agent visibility,
  Phase B enables actual functionality

## Reviewer-noted gotchas

- `compile_preflight` `readOnlyHint=True` may need flip if workspace init has
  side effects
- First call cold-start can take 5-30s — Python wrapper timeouts (10-15s) may
  surface as ToolError on cold workspaces; warm-up before exposing to agents
- `[ROSLYN UNAVAILABLE: ...]` is the graceful fallback when Roslyn DLLs miss
  AFTER handler registration. Pre-Phase-B (no handler) raises `ToolError` —
  acceptable fail-safe.
