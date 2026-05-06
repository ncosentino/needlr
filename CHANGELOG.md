## [0.0.2-alpha.52] - 2026-05-05

### Fixed

- **🛠️ Production crash: `JsonElement.GetString()` throws on non-string kinds** — `AIFunctionProviderCodeGenerator` emitted unsafe direct `JsonElement.GetXxx()` calls into every generated tool wrapper. When the underlying `IChatClient` was anything other than the GitHub Copilot client (e.g. `AzureOpenAIClient.AsIChatClient()`), tool arguments arrived as the raw `JsonValueKind` the model produced — `Array`, `Object`, `Number`, etc. — and the call threw `InvalidOperationException` before user code could run. The same bug class affected `GetInt32`, `GetBoolean`, `GetDouble`, and the per-element extractors inside arrays and object items. Generated wrappers now route every extraction through the new kind-tolerant `AgentFrameworkArgumentExtractor` helper.
- **`Guid`/`DateTime`/`DateTimeOffset`/`TimeSpan` parameter binding** — these types fell through to a cast-only branch (`raw is Guid g ? g : default(Guid)!`) that silently substituted `Guid.Empty` / `DateTime.MinValue` / `TimeSpan.Zero` whenever the actual delivery shape was a `JsonElement` of `String` kind (which is always, since JSON has no native kind for any of these). Their JSON schema also emitted as `{"type":"object"}` — meaningless to the LLM. Now correctly emits `{"type":"string","format":"uuid"|"date-time"|"duration"}` and routes extraction through typed helpers. `TimeSpan` accepts both .NET round-trip `"01:30:00"` and ISO 8601 `"PT1H30M"` formats.
- **`OperationCanceledException` swallowed by `ToolResultFunctionMiddleware`** — `catch (Exception ex)` at line 56 caught cancellation tokens and converted them to `{ error: "An unexpected error occurred…" }`. Cooperative cancellation signals (parent timeouts, user cancels, structured-concurrency aborts) were silently lost. Now rethrows OCE before the general catch.
- **Object-property extraction used wrong typed helper for sub-types** — `long`/`Int16`/`Decimal`/`Single` properties inside complex array items were extracted via `GetInt32()` / `GetDouble()`, with overflow / precision-loss potential. Now picks the right typed helper (`GetInt64Argument`, `GetDecimalArgument`, etc.) per the property's declared C# type.

### Added

- **`AgentFrameworkArgumentExtractor` (`NexusLabs.Needlr.AgentFramework`)** — public static class with kind-tolerant extractors for `string`, `bool`, the 8 integer types (`byte`/`sbyte`/`short`/`ushort`/`int`/`uint`/`long`/`ulong`), `float`/`double`/`decimal`, `Guid`, `DateTime`, `DateTimeOffset`, and `TimeSpan`. Strict bool semantics (no `0`/`1` numeric coercion), `TryGetDecimal`-first / `TryGetDouble`-first precision, `NaN`/`Infinity` rejection, and dual-format `TimeSpan` parsing (.NET + ISO 8601). 63 unit tests.
- **`ToolResultFunctionMiddleware.HandleInvocationAsync`** — internal static method exposing the translation logic (exception capture, `IToolResult` unwrap, plain pass-through) for direct unit testing without standing up a full agent pipeline. Same logic as before; refactored for testability.
- **`ToolResult.UnhandledFailure(Exception, Func<Exception, ToolError>? formatter = null)`** — overload that lets consumers customize the LLM-facing failure message. Default formatter now includes the exception type name so the LLM can distinguish `ArgumentException` (fix shape) from `TimeoutException` (retry later). The `Exception.Message` body is intentionally still excluded to avoid PII leaks.
- **NDLRMAF030 analyzer** (Info severity) — hints when an `[AgentFunction]` `string` parameter is being used to carry JSON. Fires when the parameter name ends with `Json` / `_json` OR its `[Description]` text mentions `"JSON array"` / `"JSON object"`. Suggests typing the parameter as `JsonElement` for direct, typed access. Informational only — the new generator coercion makes the string-typed shape work via `GetRawText()`.
- **End-to-end wrapper tests** — new `NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests` project that exercises generated `AIFunction` wrappers via the same path `FunctionInvokingChatClient` uses (resolve `IAIFunctionProvider` → invoke with literal `JsonElement` args). Closes the test-ergonomics gap that hid the production crash. 19 tests covering string/int/bool/array/object-array branches plus all four temporal/Guid types plus a nested-temporal-property fixture.
- **`docs/tool-result-middleware.md`** — comprehensive reference for `ToolResultFunctionMiddleware`: its three behaviors (exception capture, `IToolResult` unwrap, plain pass-through), trade-offs of enabling it (agent-turn survives tool exceptions; LLM gets sanitized error; diagnostics counts as success), the plugin-ordering pitfall vs `UsingResilience()`, and a clear recommendation to enable for production agent setups.
- **`docs/analyzers/NDLRMAF030.md`** — analyzer reference page in the standard cause / rule / how-to-fix / when-to-suppress format.
- **`BatchFunctions.AttachTopicMetadata`** in `SimpleAgentFrameworkApp.Agents` — example tool demonstrating the `JsonElement` parameter pattern (the analyzer-suggested alternative for arbitrary JSON metadata).

### Changed

- **`UnhandledFailure` default LLM-facing message** changed from `"An unexpected error occurred. Please try again."` to `"An unexpected error occurred ({ExceptionTypeName}). Please try again."` — type names only (no message body or stack). Pass `formatter:` to opt out.
- **`string` parameter null/undefined handling** — was throw (`JsonElement.GetString()` throws on `Undefined`), now returns `""`. Code expecting null-tolerance can rely on this.
- **`string` parameter receiving non-string kinds** — was throw, now returns `GetRawText()` (canonical JSON of the literal). Existing tool bodies that call `JsonSerializer.Deserialize<T[]>(arg)` keep working transparently because `GetRawText()` of an array is valid JSON.
- **`bool`/numeric parameter receiving wrong kind or `null`** — still throws, but with a useful kind-detail message; the previous `Convert.ToInt32(null) → 0` silent path is gone.
- **`int[]` and `string[]` element extraction** — previously crashed when an element was the wrong `JsonValueKind`; now routes through the kind-tolerant helper per element.
- Behavior change for nested-object Guid/DateTime/TimeSpan properties: the JSON schema previously emitted `{"type":"object"}` for these (broken — model didn't know what to send). Now correctly emits `{"type":"string","format":"..."}`. Tools that rely on the old broken behavior will see different LLM input shapes — the new shape is what was intended.

### Internal

- `AgentFunctionParameterInfo` gained a `JsonSchemaFormat` field; `ObjectPropertyInfo` gained `SchemaFormat` and `CSharpTypeFullName` so nested-object temporal properties dispatch to the right typed helper.
- `AppendIntegerExtraction` / `AppendNumberExtraction` no longer take a `jVar` parameter (unused after the helper-call refactor).

## [0.0.2-alpha.51] - 2026-05-03

### Added
- **`IterativeLoopStageExecutor`** — new `IStageExecutor` that bridges `IIterativeAgentLoop` into the sequential pipeline ecosystem. Supports options factory, context factory, `onLoopCompleted` callback for termination metadata capture, `shouldTreatAsSuccess` predicate for acceptable non-success terminations, and configurable `FailureDisposition`. Result-based failure mapping (no exceptions for control flow). 56 tests.
- **Pipeline phases** — `PipelinePhase` groups sequential stages with lifecycle hooks and phase-level token budgets. `RunPhasedAsync` / `RunPhasedAsync<TState>` on `SequentialPipelineRunner`. `PipelinePhasePolicy` provides async `OnEnterAsync` / `OnExitAsync` hooks (exit runs in `finally`) and `TokenBudget` that scopes the entire phase (composes with per-stage budgets). Phase metadata on `StageExecutionContext` (`PhaseName`, `PhaseIndex`, `StageIndexInPhase`, `TotalStagesInPhase`) and `IAgentStageResult.PhaseName` (default interface member, non-breaking). `PhaseStartedEvent` / `PhaseCompletedEvent` progress events. 29 tests.
- **Customer Support Triage example** — 3-phase agent pipeline demonstrating `OnEnterAsync`, typed pipeline state via `RunPhasedAsync<TriageState>`, `ShouldSkip` driven by typed state, `PostValidation` with retry on urgency parsing, `CompletionGate`, and workspace-as-memory between phases. Real LLM agents via Copilot.
- **Code Review Pipeline example** — 3-phase agent pipeline demonstrating phase + stage token budget composition (`StageExecutionPolicy.TokenBudget` nested within `PipelinePhasePolicy.TokenBudget`). `OnExitAsync` reports actual phase consumption. Real LLM agents via Copilot.

### Fixed
- **HTTP timeout no longer kills pipeline** — `IterativeAgentLoop` now distinguishes HTTP timeouts (`TaskCanceledException` with `TimeoutException` inner) from genuine user cancellation (`CancellationToken.IsCancellationRequested`). Timeouts report `TerminationReason.Error` with descriptive message instead of `TerminationReason.Cancelled` / "Loop was cancelled." Pipeline retry logic for Critical stages now works correctly for transient API slowdowns. 7 tests.
- **Partial iteration recording** — when an iterative loop is interrupted mid-iteration (timeout or cancellation), completed tool calls and timing data are preserved in a partial `IterationRecord`. Previously reported "0 iterations, 0 tool calls" despite work being completed.
- **`OnExitAsync` budget ordering** — phase `OnExitAsync` hooks now run before the phase budget scope is disposed, so exit hooks can read `ITokenBudgetTracker.CurrentTokens` and `MaxTokens` correctly.
- **`CopilotChatClient` timeout documentation** — XML docs on constructors now call out `HttpClient.Timeout` configuration with examples for long-running pipeline workloads.

## [0.0.2-alpha.50] - 2026-05-02

### Added
- **`IServiceCollection.AddNeedlrAgentFramework()`** — new extension method enabling `IServiceCollectionPlugin` implementations to self-register the agent framework without requiring the composition root to call `UsingAgentFramework()` on the syringe builder. Both entry points share the same `RegisterAgentFrameworkCore()` implementation — zero duplication, zero drift.

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