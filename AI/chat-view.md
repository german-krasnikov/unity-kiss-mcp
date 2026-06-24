# Chat View Architecture

In-Unity MCP Chat window: 15-file partial class MCPChatWindow, Markdown→UIElements rendering, chip system for scene references, annotation overlays.

## File Organization

### CLI Layer (Backend Abstraction)
- `Chat/CLI/CliBackendBase.cs` — Interface for Claude/Codex/Kimi/Gemini/OpenCode
- `Chat/CLI/ChipKindRegistry.cs` — Plugin-extensible chip display registry (provider pattern)
- `Chat/CLI/DeferredSchema.cs` — Lazy tool schema loading (token optimization)

### View Layer (UIElements Rendering)
- `Chat/View/MCPChatWindow.cs` — Main window (15 partials: input, transcript, events, reload-survival)
- `Chat/View/ChatTranscript.cs` — Message rendering + reload-survival serialization
- `Chat/View/ChatBlockRendererRegistry.cs` — Block type → renderer dispatcher
- `Chat/View/Markdown/MarkdownParser.cs` — Pure markdown→MdBlock parse (no side effects)
- `Chat/View/Markdown/MarkdownRenderers.cs` — MdBlock → UIElements (heading, code, table, etc.)
- `Chat/View/Mermaid/MermaidRenderer.cs` — Diagram rendering via SVG texture
- `Chat/View/Chips/ChipPillFactory.cs` — Unified chip rendering (scene refs, toggles, buttons)
- `Chat/View/Chips/ChipTextInterleaver.cs` — Interleave chip displays with plain text

### Data Layer
- `Chat/Data/ChatEvent.cs` — Event log entry (message, tool call, error)
- `Chat/Data/UserMessage.cs` — User input with interleaved chips (segments)
- `Chat/Data/ToolCallRecord.cs` — MCP tool invocation + result

## MCPChatWindow (Main Window) — 15 Partials

| Partial | Responsibility |
|---------|-----------------|
| MCPChatWindow.cs | Field declarations, lifecycle (OnEnable/OnDisable) |
| _Build.cs | UIBuilder: construct layout (input, transcript, buttons) |
| _Events.cs | Event handlers: OnSubmit, OnChatEvent, OnReload |
| _Backend.cs | CLI backend selection + configuration |
| _Input.cs | TextField management, auto-complete, @ mentions |
| _Transcript.cs | ChatTranscript delegation: AppendMessage, Finalize |
| _Permission.cs | Dialog for MCP tool permissions |
| _Reload.cs | Domain reload survival: SerializeForReload, RestoreFromReload |
| _Agent.cs | Agent mode toggle + agentic turn handling |
| _Token.cs | Token budget display + Haiku cost tracking |
| _Undo.cs | Turn undo (TurnUndoTracker) |
| _Tests.cs | NUnit helpers (test-scoped hooks) |

**Key Fields:**
- `_backend`: Current CLI backend (Claude/Codex/Kimi/Gemini)
- `_transcript`: ChatTranscript renderer
- `_agentMode`: Boolean for agent vs. one-shot mode
- `_sentLlmCache`: Full-path payload cache (reload-survival)
- `_permConfig`: Permission UI state
- `_resumeRetryCount`: Bounded retry for compile-clean gate (max 30)

**Reload-Survival (F21):**
- `SerializeForReload()` → JSON snapshot of _entries + _sentLlmCache
- `RestoreFromReload()` → Re-render from snapshot + re-send pending turns if compile clean
- Circuit breaker: _resumeRetryCount prevents infinite retry loop

## ChatTranscript

**Responsibility:** Render turn messages (user + assistant) to UIElements.

**Architecture:**
- `_entries`: List<TranscriptEntry> (F21 reload-survival)
- `_registry`: ChatBlockRendererRegistry (dispatch MdBlock type → renderer)
- `_container`: VisualElement parent
- `_assistantBubble`: Live tail for streaming

**Key Methods:**
- `AppendUserBubble(UserMessage, llmPayload)` — Render user input with chips
- `AppendBlock(MdBlock)` — Dispatch to renderer based on block.kind
- `FinalizeAssistant()` — Close live tail; commit to transcript
- `SerializeForReload()` / `RestoreFromReload()` — Reload-survival

**Streaming:** Live assistant text appended to _assistantBubble; FinalizeAssistant() commits on LLM done.

**Tool Chips:** ToolChipGrouper batches tool-call UI (prevents duplicate displays).

## Markdown Pipeline

### Parse: MarkdownParser.Parse(text) → List<MdBlock>

**Single-pass, pure (no side effects).**

**Block Types:**
- Heading (H1-H6)
- CodeBlock (fenced or indented)
- Table (GFM pipe syntax)
- List (ordered/unordered)
- Image (standalone)
- Paragraph (default fallback)

**Key Regex:**
- Fenced code: `^```(language)?` → `^```$`
- Image: `^!\[...\]\(...\)$`
- Table: `^|...|...|$` (GFM 3-row header-sep-body)

**Fence Priority:** Checked first (code blocks take precedence over other syntax).

### Render: MarkdownRenderers.Render(block) → VisualElement

**Dispatcher pattern:** SwitchOnKind → Create block renderer (Label, Markdown UI, RichText, etc.).

**Mermaid Diagrams:**
- Detected: ````mermaid` fence
- Rendered: MermaidRenderer.CreateDiagram() → SVG texture → Image
- Fallback: Plain text if Mermaid unavailable

**Code Highlighting:** Syntax coloring per language (C#, Python, GLSL).

**Tables:** Rendered as VisualElement grid (no native UIElements Table; built from Rows/Columns).

## Chip System

### ChipPillFactory (Unified Rendering)

**Purpose:** Single source for chip display (scene refs, toggles, buttons, hyperlinks).

**Chip Types:**
- Scene object: /Path → clickable frame-to-object
- Asset ref: Guid → clickable open-in-inspector
- Toggle: checkbox state
- Button: action trigger
- Hyperlink: external URL

**Provider Model:**
```csharp
public interface IChipKindProvider
{
    string Key { get; }  // "scene", "asset", "toggle"
    VisualElement CreateChip(ChipData data);
    bool Navigate(string payload);
}
```

**Registration:**
```csharp
ChipKindRegistry.Register(new SceneChipProvider());  // 3rd party
```

**Markup Syntax:** `[kind:payload]text[/kind]` in LLM response (symmetric in/out).

### ChipTextInterleaver

**Purpose:** Interleave chip pill displays with plain text (no double-rendering).

**Input:** UserMessage with Segments (text + chip data)
**Output:** VisualElement with chips positioned inline

**Segments:** Each segment = (Text? or ChipData?) — builder pattern ensures no duplicates.

## Annotation System

### ChatAnnotation (F11)

**Purpose:** Persistent metadata on turns (edited, tool-calls, compile state).

**Fields:**
- `TurnEditedCode`: User hand-edited response (bypass LLM)
- `TurnHasToolCalls`: MCP tools were invoked
- `NeedsRefresh`: Transcript dirty (re-render on next frame)

**Persistence:** Stored in turn entry; survives reload via _entries list.

## Viewers & Overlays

### Viewer Pattern

**Purpose:** Specialized renderers for complex types (VFX previews, scene graph, 3D visualizations).

**Built-in Viewers:**
- Scene Graph viewer (hierarchy tree)
- Inspector viewer (component details)
- Diff viewer (code changes)
- Blueprint viewer (Mermaid diagrams)

**3rd-party Extension:** Plugins register viewers via Viewer registry (similar to ChipKind registry).

### SceneMcpOverlay

**Purpose:** In-scene visualization of MCP operations (regions, selections, annotations).

**Elements:**
- Region polygons (Lasso/Rectangle draw modes)
- Selected object highlights
- Gizmo handles for manipulation

**Integration:** Chat window can trigger overlay updates via static actions.

## Common Patterns

| Pattern | File | Why |
|---------|------|-----|
| Add new block type | Markdown/MarkdownParser.cs + MarkdownRenderers.cs | Single-pass parse + dispatch render |
| Add 3rd-party chip | ChipKindRegistry.Register(new MyProvider()) | No core edits; extensible |
| Persist chat state | ChatTranscript._entries + SerializeForReload() | Reload-survival; survives domain reload |
| Stream response | ChatTranscript._assistantBubble | Live tail; append + FinalizeAssistant() |
| Handle tool results | ChatTranscript.AppendBlock(ToolBlock) | ToolChipGrouper batches display |

## Reload-Survival (F21 Innovation)

**Problem:** Domain reload clears memory; chat window loses transcript.

**Solution:** SerializeForReload → JSON snapshot → RestoreFromReload + re-render.

**Circuit Breaker:** _resumeRetryCount prevents infinite loop (max 30 retries before giving up).

**Payload Cache:** _sentLlmCache stores full-path LLM input (not short display text) so re-send is identical.

## Error Handling

| Error | File | Fix |
|-------|------|-----|
| Markdown parse fails | MarkdownParser.Parse() | Null/empty → empty list (graceful) |
| Chip provider missing | ChipKindRegistry | Fallback to plain text |
| Reload serialization corrupt | ChatTranscript._entries | Discard; start fresh transcript |
| Mermaid render timeout | MermaidRenderer | Show SVG placeholder + text |

---

**Related:** `.claude/skills/encoding.md` (UTF-8 safety in markup), `AI/batch.md` (tool result formatting), `CLAUDE.md` § chat-features research.
