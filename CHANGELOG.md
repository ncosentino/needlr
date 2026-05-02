## [0.0.2-alpha.49] - 2026-05-02

### Fixed
- **Same-assembly internal interface registration** — the source generator was blanket-skipping all internal interfaces (including same-assembly ones), breaking the standard `internal class Foo : IFoo` DI pattern. Replaced with `IsAccessibleFromGeneratedCode` that allows same-assembly internal/protected-internal interfaces while still skipping cross-assembly internals (e.g., Avalonia's `IContentPresenterHost`) that would produce CS0122
- **`WouldBePluginIgnoringAccessibility` same-assembly awareness** — now passes `compilationAssembly` through to `GetPluginInterfaces` so plugin diagnostic reporting correctly recognizes same-assembly internal interfaces
- **Release workflow test step** — `release.yml` was still running `dotnet test` on the entire solution (unlike `ci.yml` which was already fixed), hitting the MTP/VSTest incompatibility on .NET 10 SDK for evaluation example projects

## [0.0.2-alpha.48] - 2026-04-30

### Added
- **Sequential pipeline runner** — `SequentialPipelineRunner` for orchestrating `IIterativeAgentLoop` runs as pipeline stages with retry, validation, skip, and criticality support
- **Pipeline evaluation bridge** — `PipelineEvaluationContext` bridging `IPipelineRunResult` diagnostics to MEAI evaluators; pipeline-specific evaluators for stage-level scoring
- **Executor decorators** — composable middleware for pipeline stage execution (retry, timeout, fallback, logging)
- **Transcript logging middleware** — captures full agent conversation transcripts during pipeline runs
- **Failure disposition and hooks** — pipeline stages can declare failure disposition (fail-fast, continue, skip) and pre/post execution hooks with typed state
- **FallbackExecutor optional predicate** — fallback execution can be gated on a caller-supplied condition
- **AsyncLocalScoped property proxies** — `[AsyncLocalScoped]` source generator now emits proxy properties for all public properties on the scoped type
- **PromptAssert testing utility** — assertion helpers for validating LLM prompt content in unit tests
- **Pipeline scenario runner** — test infrastructure for running deterministic pipeline scenarios with structured assertions
- **RFC pipeline example** — full end-to-end example app exercising all pipeline features (decorators, hooks, typed state, eval tests)

### Fixed
- **`IToolMetricsAccessor.AttachMetric()` thread-safety** — replaced `Dictionary` with `ConcurrentDictionary` in the AsyncLocal backing store; concurrent `Task.WhenAll` branches inside `[AgentFunction]` methods no longer corrupt the metrics collection
- Stage failure recording and cancellation heuristics in `SequentialPipelineRunner`
- HTTP timeout handling now correctly recorded as stage failures
- Planned stage count tracking, xUnit test discovery, and evaluation logging

## [0.0.2-alpha.47] - 2026-04-25

### ⚠️ Breaking Changes
- `IWorkflowFactory.CreateGraphWorkflow(string)` now requires `[RequiresUnreferencedCode]` annotation for AOT compatibility (IL2046)
- `DiagnosticsChatClientMiddleware.StartChatActivity` return type changed from `Activity?` to `(Activity? Created, Activity? Target)` tuple — callers writing tags must use the `Target` activity

### Added
- **DAG graph workflows** — `[AgentGraphNode]`, `[AgentGraphEdge]`, `[AgentGraphEntry]`, `[AgentGraphReducer]` attributes for declaring directed acyclic graph workflows with conditions, reducers, LlmChoice routing, and WaitAny/WaitAll join modes
- **DAG source generator** — emits `AgentGraphTopologyRegistry`, Mermaid diagrams, graph run helper extensions, and `GraphNames` constants at compile time
- **DAG analyzers** — NDLRMAF016–029 covering orphan graph attributes, cyclic edges, unreachable nodes, missing entry points, condition/reducer validation, WaitAny+CreateGraph incompatibility
- **DAG runtime** — `IGraphWorkflowRunner` with BSP-based WaitAll execution and Needlr-native WaitAny executor with fan-out/fan-in, condition evaluation, reducer invocation
- **DevUI integration** — new `NexusLabs.Needlr.AgentFramework.DevUI` package bridging `[NeedlrAiAgent]` agents into MAF DevUI's `/devui` web interface and `/v1/entities` discovery
- **DevUI example app** — `DevUIApp` with Copilot LLM backend, tool calling (`LookupCapital`, `GetCurrentTime`), and live interactive chat
- **Generator coexistence example** — `GeneratorCoexistenceApp` proving Needlr and MAF `Workflows.Generators` compile in the same project without conflict
- **ReturnJsonSchema on source-generated AIFunction** — emits `ReturnJsonSchema` override with property-level schemas for complex return types, enabling structured output
- **IChatReducer convenience** — `IterativeLoopOptions.ChatReducer` property auto-composes `ReducingChatClient` into the per-loop pipeline
- **New MEAI content type handling** — `DiagnosticsCharCounter` counts `TextReasoningContent`; `EvaluationCaptureChatClient` hashes `TextReasoningContent` and `WebSearchToolCallContent` in cache keys
- **EnrichParent span enrichment** — `DiagnosticsChatClientMiddleware` now writes OTel tags to the parent `gen_ai.*` span when suppressing its own activity
- **Evaluation infrastructure** — `TaskCompletionEvaluator` (LLM-judged), `EfficiencyEvaluator` (token/cost), `EvaluationQualityGate` for CI regression detection
- **Expert agents** — `microsoft-agent-framework.agent.md`, `meai.agent.md`, `csharp-source-generator.agent.md`, `csharp-roslyn-analyzer.agent.md`, `ai-evaluation.agent.md`
- **DagRoutingApp** and enhanced **GraphWorkflowApp** examples demonstrating 4 DAG scenarios and routing mode comparison

### Changed
- **MAF upgraded from 1.0.0-rc1 → 1.3.0** (stable release)
- **MEAI upgraded from 10.3.0 → 10.5.0**
- Added `Microsoft.Agents.AI.Workflows.Generators` 1.3.0 to `Directory.Packages.props`
- Added DevUI/Hosting preview packages (`1.3.0-preview`) to `Directory.Packages.props` (isolated in DevUI package)
- Suppressed `GHSA-g94r-2vxg-569j` (OpenTelemetry.Api 1.15.0 transitive vulnerability from MAF) in `Directory.Build.props`
- Agent identity uses `Type.FullName` across the codebase instead of `Type.Name`

### Fixed
- **CopilotChatClient streaming tool call accumulation** — incremental SSE chunks for tool calls with arguments were creating ghost `FunctionCallContent` entries with empty names; now properly accumulated by index and emitted as complete function calls on `finish_reason=tool_calls`
- **CopilotChatClient empty-name function calls** — filters empty-name `FunctionCallContent` and empty-callId `FunctionResultContent` from outgoing messages to prevent Copilot API `BadRequest` errors
- **EnrichParent mode dropped OTel tags** — `StartChatActivity` returned `null` when suppressing, causing all `activity?.SetTag()` calls to be no-ops; now returns a `(Created, Target)` tuple so tags flow to the parent span
- **HostedService tests port conflict** — tests were hardcoded to port 5244 (conflicting with Narnia); now use `UseUrls("http://127.0.0.1:0")` for random port binding
- **Activity test isolation** — each `ChatCompletionActivityModeTests` test uses GUID-suffixed `ActivitySourceName` preventing cross-test `ActivityListener` interference
- **AOT trim analysis IL2046** — `IWorkflowFactory.CreateGraphWorkflow` missing `[RequiresUnreferencedCode]` annotation matching the implementation
- BSP routing, LlmChoice mode, reducer closure capture, and DI resolution in DAG runtime
- Semantic condition matching and structured classifier prompts in routing examples