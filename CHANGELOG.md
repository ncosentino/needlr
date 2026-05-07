## [Unreleased]

### Added

- **`ToolInvocationRunner`** (`NexusLabs.Needlr.AgentFramework.Testing`) — public, supported test harness for invoking `[AgentFunction]`-decorated tool methods through their source-generated `AIFunction` wrapper. Removes the boilerplate consumers used to face: build a service provider with the right registrations, look up the source-generated `IAIFunctionProvider`, find the right `AIFunction` by name, build `AIFunctionArguments`, establish an ambient `IAgentExecutionContext` so the tool can read `accessor.Current.GetWorkspace()`. Default mode is generated-only; reflection fallback is opt-in via the explicitly-named, AOT-annotated `GetFunctionAllowingReflection*` methods. Per-invocation `IServiceScopeFactory.CreateScope()` so tools with scoped dependencies behave correctly. Immutable `With*` methods return new instances to prevent cross-test leakage. `LimitToTools(...)` wraps `AgentFrameworkGeneratedBootstrap.BeginTestScope` for per-test source-gen scope isolation. 25 unit tests + 60 integration tests (existing `GeneratedWrapper.Tests` migrated as the forcing function).
- **`AgentExecutionContextBuilder`** (`NexusLabs.Needlr.AgentFramework.Testing`) — fluent builder for the `IAgentExecutionContext` the runner establishes during invocation. Top-level `WithWorkspace(seed)` / `WithWorkspace(IWorkspace)` overloads remove the need to learn the longer `WithExecutionContext(c => c.WithWorkspace(...))` chain.
- **`ToolInvocationResult`** (`NexusLabs.Needlr.AgentFramework.Testing`) — record returned from `InvokeAsync`. Exposes `ReturnValue`, `Exception`, `FunctionSource` (Generated vs Reflection — surfaces which path actually ran), `Workspace` (for post-invocation file assertions), and `Duration`. Helpers: `Succeeded`, `GetValue<T>()`, `AssertSuccess()`, `AssertResultContains(...)`.
- **`ToolFunctionSource`** (`NexusLabs.Needlr.AgentFramework.Testing`) — enum (`Generated`, `Reflection`) so tests can assert they exercised the source-generated wrapper rather than silently falling through to reflection-based discovery.
- **`AgentFrameworkAccessorServiceCollectionExtensions.AddAgentFrameworkAccessors()`** (`NexusLabs.Needlr.AgentFramework`) — public extension that registers the small set of Needlr agent accessors (`IAgentExecutionContextAccessor`, `IAgentDiagnosticsAccessor`, `IAgentDiagnosticsWriter`) without dragging in the rest of the Agent Framework wiring. Lets tool tests build a minimal `IServiceProvider` without going through Syringe.
- **`AgentFrameworkGeneratedBootstrap.BeginTestScope`** promoted from `internal` to `public`. Unlocks per-test isolation for consumer test projects with many `[AgentFunction]` types — they can scope the source-gen visibility to a specific subset for the duration of one test without disturbing parallel tests in different async flows.
- **`docs/testing-tools.md`** — paved-path testing reference covering `ToolInvocationRunner`, wrapper edge-case tests, per-test source-gen scoping via `LimitToTools`, the explicit reflection fallback, full-agent scenario tests via `AgentScenarioRunner`, and a clear pointer to the planned `ScriptedChatClient` follow-up. Includes anti-patterns to avoid (calling `tool.DoIt(...)` directly, hand-rolling DI, sharing mutable runners).
- **`docs/adr/adr-0002-build-scriptedchatclient-locally.md`** — ADR documenting the build-vs-buy investigation for a scripted `IChatClient`. Conclusion: `Microsoft.Extensions.AI.Testing` does not exist (HTTP 404, no roadmap entry, dotnet/extensions team's own multi-turn tests use a copy-pasted internal `TestChatClient`). The follow-up will ship a first-party `ScriptedChatClient` in `NexusLabs.Needlr.AgentFramework.Testing`.
- **`WorkspacePath`** (`NexusLabs.Needlr.AgentFramework.Workspace`) — public static class that defines the `IWorkspace` path-canonicalization contract. Three members: `Canonicalize(string)` for file paths (rejects null, empty, whitespace, `..` segments, and inputs that canonicalize to empty); `CanonicalizeDirectory(string)` for directory paths (same rules but root-equivalent inputs return `""` instead of throwing); and `PathComparer` (a `StringComparer.OrdinalIgnoreCase` property that implementations MUST use for path equality). Single source of truth for path identity across all `IWorkspace` implementations — third-party impls and decorators no longer need to re-derive the canonicalization rules.
- **`IWorkspace`** (`NexusLabs.Needlr.AgentFramework.Workspace`) — interface XML docs now document the path-canonicalization contract on the interface itself (rules, `PathComparer` requirement, `ActualPath`-is-canonical guarantee, and the `Argument*Exception`-vs-`WorkspaceResult.Fail` boundary). Previously the only mention of normalization was a one-line remark with no contract.
- **`docs/workspace.md`** — paved-path reference for `IWorkspace` covering the canonicalization contract (with input/output table and rejection list), the `WorkspacePath` helpers, the `InMemoryWorkspace` reference impl, the decorator pattern (with an `AllowedWritePathsWorkspace` example), the exception-vs-result boundary, and a migration note for callers that were unintentionally relying on the previous aliasing behavior.

### Fixed

- **🛠️ `InMemoryWorkspace` path aliasing** — the previous `NormalizePath` was just `path.Replace('\\', '/').TrimStart('/')`, so `kb/foo.md`, `./kb/foo.md`, `kb//foo.md`, `kb/./foo.md`, `/kb/foo.md`, and `kb/foo.md/` all produced **distinct dictionary keys** despite referring to the same logical file. Two agents writing the same logical file under different aliases would silently produce two entries and downstream readers would see "missing file" gaps. Now all six variants collide on the canonical `kb/foo.md` key, matching the (now-explicit) `IWorkspace` contract. Filed against `0.0.2-alpha-0054` and observed as far back as `0.0.2-alpha-0049`. Original report: BrandGhost engineering, brand-KB pipeline build (2026-05-06).

### Changed

- **`AgentFrameworkGeneratedBootstrap.BeginTestScope`** — visibility changed from `internal` to `public`. Existing internal callers (`AgentFramework.Tests`, `AgentFramework.Workflows`) continue to work unchanged.
- **`InMemoryWorkspace` rejects `..` segments and root-equivalent file paths.** Paths that contain a segment exactly equal to `..` (e.g., `kb/../foo.md`) now throw `ArgumentException` instead of being treated as a literal segment that silently produces a distinct key. File-mutating methods (`TryReadFile`, `TryWriteFile`, `FileExists`, `TryCompareExchange`, `ReadFileAsMemory`, `SeedFile`) also now throw `ArgumentException` for empty / whitespace-only / root-equivalent inputs (`""`, `"/"`, `"."`, `"./"`, `"/./"`, etc.). `ListDirectory` continues to accept these as the workspace root (it routes through `WorkspacePath.CanonicalizeDirectory` instead of `Canonicalize`). Migration: pass the resolved workspace-relative path directly; never `..`. Filenames that *contain* `..` as a substring (e.g., `version..draft.md`) remain valid — only segments exactly equal to `..` are rejected.

### Internal

- `NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests/AIFunctionWrapperEndToEndTests.cs` — migrated from a private `ResolveFunction<TTool>(string methodName)` helper to the public `ToolInvocationRunner.GetFunction<TTool>(name)` API. Forcing function: if the public surface couldn't express what the 57 wrapper edge-case tests needed, we missed something in the API. All 57 tests pass.
- `NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests/ToolInvocationRunnerLimitToToolsTests.cs` — new 3-test integration suite covering the source-gen scope wrapper.

## [0.0.2-alpha.54] - 2026-05-06

### Fixed

- **🛠️ Production crash: `[AgentFunction]` tools with optional primitives throw on non-Copilot `IChatClient`** — `AgentFrameworkFunctionRegistryGenerator` captured `HasExplicitDefaultValue` and `IsNullable` for the JSON schema's `required` array (correctly telling the model "these params are optional") but **dropped both flags when emitting `InvokeCoreAsync`**. The generated wrapper called the strict extractor unconditionally, so when the underlying `IChatClient` legitimately omitted an optional argument (`AzureOpenAIClient.AsIChatClient()`, Anthropic, every non-normalizing client) the extractor threw `InvalidOperationException("Cannot extract bool argument from null.")` before user code ran. GitHub Copilot's normalizing client masked the bug by materializing typed defaults before delivery. **Now the wrapper honors C# default values, nullability annotations, and required-param semantics end-to-end** — swapping `IChatClient` providers is finally transparent for `[AgentFunction]` tool authors.
- **Required `[AgentFunction]` parameter missing key now throws a useful exception** — was `InvalidOperationException("Cannot extract bool argument from null.")` deep in the extractor stack with no operator clue which arg or which tool. Now `ArgumentException("Required argument 'flag' was not supplied to AIFunction 'MyTool.M'.")` thrown at the wrapper boundary with named arg/method context.
- **Enum-typed `[AgentFunction]` parameters did not compile** — schema mapped enums to `"string"`, but the generated code assigned `string` directly to an enum-typed variable. Now routes through `Enum.Parse<TEnum>(GetStringArgument(raw), ignoreCase: true)`. Defaulted enums (`Mode mode = Mode.Append`) emit the typed enum-member literal as the fallback (Roslyn surfaces the underlying integer; the generator now resolves the matching field by constant value).
- **Defaulted `Guid` / `DateTime` / `DateTimeOffset` / `TimeSpan` parameters generated `var = null;`** — Roslyn returns `null` from `ExplicitDefaultValue` for non-primitive value-type defaults; the previous emission tried to assign `null` to a non-nullable value type and would not have compiled if anyone hit the path. Now emits `default(global::System.Guid)` (etc.) for these.
- **DTO `{"prop": null}` payload threw on value-type properties** — the now-strict extractor rejects `JsonValueKind.Null`, so a DTO property assignment of the form `dto.Count = GetInt32Argument(_p_count);` blew up when the model sent an explicit `null`. The generator now gates each per-property assignment on `JsonValueKind.Null` / `JsonValueKind.Undefined` and silently leaves the property at whatever the parent `new T()` established (so `public int Count { get; set; } = 5;` keeps `5` for `{"count": null}`).
- **Flaky `Meter_EmitsTokensUsedCounter`** test (`AgentFramework.Tests`) — three Meter tests filtered `MeterListener` events by the global default meter name `"NexusLabs.Needlr.AgentFramework"`, so parallel test fixtures across the suite that constructed `AgentMetrics()` (same default name) bled measurements into the wrong test's listener. Each test now scopes its filter to a per-test `Guid`-suffixed meter name. Verified across 3 consecutive full-suite runs after the fix.

### Added

- **`AgentFrameworkArgumentExtractor.IsArgumentSupplied(object?)`** — public static helper that returns `false` for `null`, `JsonValueKind.Null`, and `JsonValueKind.Undefined`; `true` for any other value. The documented contract mate to the existing kind-tolerant extractor methods, which assume the value is present and non-null. Generated wrappers call this once per parameter site to gate the strict extractor call. Available for hand-written `AIFunction` wrappers that need the same gating.
- **End-to-end wrapper tests for parameter optionality and default-value resolution** — 35 new tests in `NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests` covering bug report §9 matrix rows 1–18 plus DTO null-property tests and defaulted `Guid` / `DateTime` / `DateTimeOffset` / `TimeSpan` / `decimal` / enum value-type fallbacks. 9 new fixture tools.
- **`docs/agent-functions.md`** — comprehensive reference for parameter optionality semantics: required vs `HasDefault` vs `IsNullable` resolution priority for top-level params, DTO property null-payload behavior, the shared `IsArgumentSupplied` gate, the strict extractor contract, and the schema-vs-extraction invariant.

### Changed

- **Default Copilot model bumped from `claude-sonnet-4` to `claude-sonnet-4.5`** — GitHub Copilot deprecated `claude-sonnet-4`; every request against it now returns HTTP 400 `model_not_supported`. `CopilotChatClientOptions.DefaultModel`, every example app's `appsettings.json` fallback, and the `docs/copilot.md` examples updated. `claude-sonnet-4.5` and `claude-sonnet-4.6` are the live Sonnet variants; chose `4.5` as the conservative forward bump.
- **`HasDefault` wins over `IsNullable` for resolution priority** — when both flags are set on a parameter (e.g., `string? p = "x"`, `int? n = 5`), the wrapper falls back to the C# default literal on missing/null, not to `null`. The reasoning: the `?` annotation tells the framework "null is a legal value the model can send"; the `= "x"` clause tells it "but if the model omits it, use this fallback". Both flags coexist.
- **Required-param missing key now throws `ArgumentException`** at the wrapper boundary instead of `InvalidOperationException` deep in the extractor stack — strictly an improvement (named arg, named tool/method) but the exception type changed.
- **DTO `{"prop": null}` payloads now respect each property's C# init default** instead of silently coercing strings to `""` via the previous `GetStringArgument(null) → ""` quirk. Aligns the null-payload behavior with the missing-key behavior. Covered by 3 explicit tests so future regressions cannot slip through silently.
- **`GetStringArgument(null)` and `GetStringArgument(JsonValueKind.Null|Undefined)` now throw `InvalidOperationException`** — previously returned `string.Empty`. Restores symmetry with every other extractor method on the class. Direct callers should gate with `IsArgumentSupplied` first.

### Removed

- **`RfcPipelineApp.Evaluation.XUnit`** and **`RfcPipelineApp.Evaluation.TUnit`** example projects — both were smoke tests masquerading as evaluation harnesses. Their 6 assertions only proved "the pipeline ran end-to-end without throwing" (no quality, prompt-efficiency, or correctness measurement). Each suite fired ~15–25 live Copilot completions per run, routinely tripping HTTP timeouts and rate-limit throttling — making the suites indeterminate without providing meaningful failure signal. End-to-end Copilot coverage is provided by the dedicated live smoke tests (`IntegrationSmokeTests`, `CopilotSmokeTests`); pipeline orchestration is covered deterministically in `AgentFramework.Tests` and `Evaluation.Tests`.

### Internal

- `AgentFunctionParameterInfo` gained `DefaultLiteral` (the C# literal expression for the parameter's default value, when `HasDefault` is true) and `IsEnum` (drives enum-aware extraction emission).
- `ObjectPropertyInfo` gained `InitDefaultLiteral` (captures simple-literal property initializers from DTO declarations; currently informational, gate-and-skip emission relies on the parent `new T()` to populate init defaults).
- `AgentDiscoveryHelper.ConvertToCSharpLiteral(value, ITypeSymbol)` — converts Roslyn-supplied `ExplicitDefaultValue` into emittable C# literals. Handles three cases that produce non-trivial output: `null` on a non-nullable value type → `default(T)`; enum literals → resolved member name (`global::MyApp.Mode.Append` instead of `2`); standard primitives.
- `AgentDiscoveryHelper.TryGetPropertyInitializerLiteral(IPropertySymbol)` — extracts simple literal initializers from DTO property declarations.
- `AppendEnumExtraction` — new emission path for enum-typed `[AgentFunction]` parameters; routes through `Enum.Parse<T>(GetStringArgument(raw), ignoreCase: true)` with required/defaulted/nullable branching matching `AppendPrimitiveExtraction`.

## [0.0.2-alpha.52] - 2026-05-05

### Fixed

- **🛠️ Production crash: `JsonElement.GetString()` throws on non-string kinds** — `AIFunctionProviderCodeGenerator` emitted unsafe direct `JsonElement.GetXxx()` calls into every generated tool wrapper. When the underlying `IChatClient` was anything other than the GitHub Copilot client (e.g. `AzureOpenAIClient.AsIChatClient()`), tool arguments arrived as the raw `JsonValueKind` the model produced — `Array`, `Object`, `Number`, etc. — and the call threw `InvalidOperationException` before user code could run. The same bug class affected `GetInt32`, `GetBoolean`, `GetDouble`, and the per-element extractors inside arrays and object items. Generated wrappers now route every extraction through the new kind-tolerant `AgentFrameworkArgumentExtractor` helper.
- **`Guid`/`DateTime`/`DateTimeOffset`/`TimeSpan` parameter binding** — these types fell through to a cast-only branch (`raw is Guid g ? g : default(Guid)!`) that silently substituted `Guid.Empty` / `DateTime.MinValue` / `TimeSpan.Zero` whenever the actual delivery shape was a `JsonElement` of `String` kind (which is always, since JSON has no native kind for any of these). Their JSON schema also emitted as `{"type":"object"}` — meaningless to the LLM. Now correctly emits `{"type":"string","format":"uuid"|"date-time"|"duration"}` and routes extraction through typed helpers. `TimeSpan` accepts both .NET round-trip `"01:30:00"` and ISO 8601 `"PT1H30M"` formats.
- **Top-level DTO / record parameter binding** — `void Tool(MyDto dto)` parameters got a meaningless `{"type":"object"}` schema (no `properties` field — the LLM had no idea what shape to send) and a silent-default extraction (`raw is MyDto _c ? _c : default(MyDto)!`) that returned `null` whenever the chat client delivered a `JsonElement` of `Object` kind. Now emits a full property-level schema (matching the array-of-objects path) and per-property `TryGetProperty` + helper-call extraction. Tool authors can use natural typed DTO shapes instead of `JsonElement` workarounds. Limitation preserved (also affects existing array-of-objects path): the DTO must have a parameterless constructor and mutable `{ get; set; }` properties — positional records and init-only properties don't bind.
- **`OperationCanceledException` swallowed by `ToolResultFunctionMiddleware`** — `catch (Exception ex)` at line 56 caught cancellation tokens and converted them to `{ error: "An unexpected error occurred…" }`. Cooperative cancellation signals (parent timeouts, user cancels, structured-concurrency aborts) were silently lost. Now rethrows OCE before the general catch.
- **Object-property extraction used wrong typed helper for sub-types** — `long`/`Int16`/`Decimal`/`Single` properties inside complex array items were extracted via `GetInt32()` / `GetDouble()`, with overflow / precision-loss potential. Now picks the right typed helper (`GetInt64Argument`, `GetDecimalArgument`, etc.) per the property's declared C# type.

### Added

- **`AgentFrameworkArgumentExtractor` (`NexusLabs.Needlr.AgentFramework`)** — public static class with kind-tolerant extractors for `string`, `bool`, the 8 integer types (`byte`/`sbyte`/`short`/`ushort`/`int`/`uint`/`long`/`ulong`), `float`/`double`/`decimal`, `Guid`, `DateTime`, `DateTimeOffset`, and `TimeSpan`. Strict bool semantics (no `0`/`1` numeric coercion), `TryGetDecimal`-first / `TryGetDouble`-first precision, `NaN`/`Infinity` rejection, and dual-format `TimeSpan` parsing (.NET + ISO 8601). 63 unit tests.
- **`ToolResultFunctionMiddleware.HandleInvocationAsync`** — internal static method exposing the translation logic (exception capture, `IToolResult` unwrap, plain pass-through) for direct unit testing without standing up a full agent pipeline. Same logic as before; refactored for testability.
- **`ToolResult.UnhandledFailure(Exception, Func<Exception, ToolError>? formatter = null)`** — overload that lets consumers customize the LLM-facing failure message. Default formatter now includes the exception type name so the LLM can distinguish `ArgumentException` (fix shape) from `TimeoutException` (retry later). The `Exception.Message` body is intentionally still excluded to avoid PII leaks.
- **NDLRMAF030 analyzer** (Info severity) — hints when an `[AgentFunction]` `string` parameter is being used to carry JSON. Fires when the parameter name ends with `Json` / `_json` OR its `[Description]` text mentions `"JSON array"` / `"JSON object"`. Suggests typing the parameter as `JsonElement` for direct, typed access. Informational only — the new generator coercion makes the string-typed shape work via `GetRawText()`.
- **End-to-end wrapper tests** — new `NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests` project that exercises generated `AIFunction` wrappers via the same path `FunctionInvokingChatClient` uses (resolve `IAIFunctionProvider` → invoke with literal `JsonElement` args). Closes the test-ergonomics gap that hid the production crash. 22 tests covering string/int/bool/array/object-array branches, all four temporal/Guid types, a nested-temporal-property fixture, and a top-level DTO with full / partial / kind-coerced JSON inputs.
- **`docs/tool-result-middleware.md`** — comprehensive reference for `ToolResultFunctionMiddleware`: its three behaviors (exception capture, `IToolResult` unwrap, plain pass-through), trade-offs of enabling it (agent-turn survives tool exceptions; LLM gets sanitized error; diagnostics counts as success), the plugin-ordering pitfall vs `UsingResilience()`, and a clear recommendation to enable for production agent setups.
- **`docs/analyzers/NDLRMAF030.md`** — analyzer reference page in the standard cause / rule / how-to-fix / when-to-suppress format.
- **Example app: typed DTO + `JsonElement` patterns** in `SimpleAgentFrameworkApp.Agents.BatchFunctions` — `AttachTopicMetadata` demonstrates the `JsonElement` parameter pattern (analyzer-suggested for arbitrary JSON); new `RecordTopicFeedback` demonstrates the typed-DTO pattern (now-supported natural shape).

### Changed

- **`UnhandledFailure` default LLM-facing message** changed from `"An unexpected error occurred. Please try again."` to `"An unexpected error occurred ({ExceptionTypeName}). Please try again."` — type names only (no message body or stack). Pass `formatter:` to opt out.
- **`string` parameter null/undefined handling** — was throw (`JsonElement.GetString()` throws on `Undefined`), now returns `""`. Code expecting null-tolerance can rely on this.
- **`string` parameter receiving non-string kinds** — was throw, now returns `GetRawText()` (canonical JSON of the literal). Existing tool bodies that call `JsonSerializer.Deserialize<T[]>(arg)` keep working transparently because `GetRawText()` of an array is valid JSON.
- **`bool`/numeric parameter receiving wrong kind or `null`** — still throws, but with a useful kind-detail message; the previous `Convert.ToInt32(null) → 0` silent path is gone.
- **`int[]` and `string[]` element extraction** — previously crashed when an element was the wrong `JsonValueKind`; now routes through the kind-tolerant helper per element.
- Behavior change for nested-object Guid/DateTime/TimeSpan properties: the JSON schema previously emitted `{"type":"object"}` for these (broken — model didn't know what to send). Now correctly emits `{"type":"string","format":"..."}`. Tools that rely on the old broken behavior will see different LLM input shapes — the new shape is what was intended.
- Top-level DTO parameters previously got `default(T)` (silent null) when the chat client delivered a `JsonElement` of `Object` kind; now populate from properties. Code that worked around this by typing the parameter as `JsonElement` and parsing manually can migrate to the natural DTO shape.

### Internal

- `AgentFunctionParameterInfo` gained `JsonSchemaFormat`, `ObjectSchemaJson`, and `ObjectProperties` fields; `ObjectPropertyInfo` gained `SchemaFormat` and `CSharpTypeFullName` so nested-object temporal properties dispatch to the right typed helper.
- `AppendIntegerExtraction` / `AppendNumberExtraction` no longer take a `jVar` parameter (unused after the helper-call refactor).
- New shared `AppendObjectPropertyAssignments` helper in `AIFunctionProviderCodeGenerator` is called from both the array-of-objects branch and the new top-level-DTO branch.

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