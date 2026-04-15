# Changelog

All notable changes to Needlr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

#### Copilot Integration

- **`NexusLabs.Needlr.Copilot`** — new integration package providing `CopilotChatClient`
  (an `IChatClient` backed by the GitHub Copilot API) and `CopilotWebSearchFunction` (an
  `AIFunction` wrapping Copilot's MCP `web_search` tool). Supports SSE streaming, automatic
  token exchange from GitHub OAuth, and configurable retry with exponential backoff. Plugs
  into the agent framework via the existing `UsingChatClient()` hook — no AF-specific
  extensions needed. Default model is `claude-sonnet-4.6`.

#### Agent Framework

- **`IIterativeAgentLoop`** — workspace-driven agent execution loop that eliminates O(n²)
  token accumulation from `FunctionInvokingChatClient`. Each iteration constructs a fresh
  prompt from workspace files instead of appending to conversation history. Configurable
  via `IterativeLoopOptions` with three `ToolResultMode`s (`SingleCall`, `OneRoundTrip`,
  `MultiRound`). Returns `IterativeLoopResult` with per-iteration diagnostics including
  tool call records, token counts, and timing. Automatically registered via DI.

- **`IChatClientAccessor`** — accessor for the DI-registered `IChatClient`, used by the
  iterative loop to make LLM calls without depending on `FunctionInvokingChatClient`.

- **`IterativeTripPlannerApp` example** — complex trip planner demonstrating the iterative
  loop with a real LLM (Copilot `claude-sonnet-4.6`) and real web search (Copilot MCP
  `web_search`). Plans multi-stop NY→Tokyo trips with 3+ intermediate stops on a $3,000
  budget with hotel rating constraints, showing research phases, budget failures, route
  pivots, and 84% token savings vs FIC. Uses full DI pattern: `[AgentFunctionGroup]` tools
  resolved via `IAgentFactory.ResolveTools()`, workspace access via
  `IAgentExecutionContextAccessor`, lifecycle hooks for progress output, progress events
  via `IProgressSink`, and diagnostics via `IAgentDiagnosticsAccessor`. No mocks or fake
  data — all flight/hotel research uses live web search.

- **Lifecycle hooks on `IterativeLoopOptions`** — `OnIterationStart`, `OnToolCall`, and
  `OnIterationEnd` async callbacks for real-time progress reporting (e.g., SignalR).
  Hook exceptions propagate to the caller; null hooks are safe.

- **`ToolFilter` on `IterativeLoopOptions`** — per-iteration tool gating callback that
  receives the iteration number, context, and full tool list, returning the filtered
  subset to offer the LLM. Enables phase-gating patterns (e.g., only offer `finalize_trip`
  after validation passes).

- **`IAgentFactory.ResolveTools()`** — public API to resolve DI-wired tool instances
  without creating an `AIAgent`. Supports filtering by `FunctionGroups`, `FunctionTypes`,
  or agent attribute via generic overloads.

- **Diagnostics accessor integration** — `IIterativeAgentLoop` automatically publishes
  `IAgentRunDiagnostics` to `IAgentDiagnosticsAccessor` after each run when the service
  is registered. Consumers call `BeginCapture()` before the run to receive diagnostics.

- **Execution context bridge** — `IIterativeAgentLoop` automatically bridges
  `IterativeContext.Workspace` to `IAgentExecutionContextAccessor` so DI-resolved tools
  can access workspace files via constructor injection.

### Fixed

#### Agent Framework

- **`IIterativeAgentLoop` now emits `ToolCallStartedEvent`/`ToolCallCompletedEvent`/
  `ToolCallFailedEvent` to the progress system** — previously, these event types were
  defined but only emitted by `DiagnosticsFunctionCallingMiddleware` (which the iterative
  loop bypasses). The loop now accepts `IProgressReporterAccessor` and emits progress
  events from `ExecuteToolCallsAsync`, giving consumers real-time tool call visibility
  via `IProgressSink`. Falls back to `NullProgressReporter` (no-op) when no sinks are
  registered.

- **`IIterativeAgentLoop` now records `ChatCompletionDiagnostics`** — previously,
  `Diagnostics.AggregateTokenUsage` was always zero because the loop never called
  `diagnosticsBuilder.AddChatCompletion()`. Now records diagnostics for both success
  and failure paths, and calls `RecordFailure()` for prompt-factory exceptions and
  cancellation.

### Removed

#### Agent Framework

- **`AddProgressSink<T>()` on `AgentFrameworkSyringe`** — removed the syringe-level
  progress sink registration method. This API caused duplicate event delivery when
  combined with Needlr's auto-discovery (which automatically registers any class
  implementing `IProgressSink`). Use auto-discovery for default sinks, or pass sinks
  explicitly via `IProgressReporterFactory.Create(workflowId, sinks)` for
  per-orchestration isolation.

- **`ProgressServiceCollectionExtensions`** — removed `IServiceCollection.AddProgressSink<T>()`
  and `IServiceCollection.AddProgressSink(instance)`. These are redundant with standard DI
  registration (`services.AddSingleton<IProgressSink, T>()`) and auto-discovery.

### Changed

#### Agent Framework

- **`IProgressReporterFactory` default sink resolution** — the `Create(workflowId)`
  overload now resolves default sinks exclusively from DI-registered `IProgressSink`
  instances (including auto-discovered ones). The removed syringe factory merge path
  is no longer involved.

### Added

- **Progress Reporting documentation** — new `docs/progress-reporting.md` page covering
  auto-discovery, per-orchestration sinks, `[DoNotAutoRegister]` opt-out,
  `[ProgressSinks]` attribute, `IProgressReporterAccessor`, and error handling.

## [0.0.2-alpha.34] - 2026-04-13

Group chat ordering and structured tool-call termination.

### Added

#### Agent Framework

- **`AgentGroupChatMemberAttribute.Order`** — controls the round-robin
  turn position in group chat workflows. Lower values run first;
  same-order agents sort alphabetically by type name. Without explicit
  ordering, agent turn position was non-deterministic (depended on
  assembly scanning order), causing scenarios where a reviewer agent
  ran before the writer produced any content.
  ```csharp
  [AgentGroupChatMember("article-writing", Order = 1)]
  public sealed class ArticleWriterAgent;

  [AgentGroupChatMember("article-writing", Order = 2)]
  public sealed class ArticleReviewerAgent;
  ```

- **`ToolCallTerminationCondition`** — terminates a group chat when an
  agent calls a specific tool instead of matching keywords in response
  text. Eliminates false positives where the LLM includes the
  termination keyword in rejection text. Inspects
  `TerminationContext.ToolCallNames` which is populated from
  `FunctionCallContent` entries in the agent's response.
  ```csharp
  [AgentTerminationCondition(typeof(ToolCallTerminationCondition),
      "ApproveArticle", "ReviewerAgent")]
  public sealed class ReviewerAgent;
  ```

- **`TerminationContext.ToolCallNames`** — new property exposing the
  names of tools/functions called by the agent during the current turn.

## [0.0.2-alpha.33] - 2026-04-13

Source-gen registration fixes, Serilog plugin, Avalonia primary
constructor diagnostic, and documentation improvements.

### Added

#### Serilog Integration

- **`SerilogPlugin`** — auto-discovered `IServiceCollectionPlugin` that wires
  `ILogger<T>` from `appsettings.json` with zero ceremony. Reads the
  `"Serilog"` section from `IConfiguration`, registers with `dispose: true`
  for automatic sink flushing on container disposal. Works in both
  source-gen and reflection modes.

- **`GeneratorAssemblyInfo.cs` for `NexusLabs.Needlr.Serilog`** — the Serilog
  package now includes `[GenerateTypeRegistry]` so the source generator emits
  a `[ModuleInitializer]` that registers `SerilogPlugin` into the bootstrap.
  Source-gen consumers discover the plugin automatically without
  `UsingAdditionalAssemblies`.

- **Source-gen Serilog example** (`SerilogSourceGenExample`) demonstrating
  zero-ceremony plugin discovery via `UsingSourceGen()`.

### Fixed

#### Agent Framework

- **`AgentMetrics` and `AgentDiagnosticsAccessor` missing `[DoNotAutoRegister]`.**
  The source generator's `TypeRegistry` was registering both types, clobbering
  the hand-written factory lambdas in `RegisterAgentFrameworkInfrastructure`.
  `AgentMetrics` was registered with `GetRequiredService<AgentFrameworkMetricsOptions>()`
  (which throws because the options are `[DoNotAutoRegister]`).
  `AgentDiagnosticsAccessor` was registered without `ChatCompletionCollectorHolder`
  injection, causing `CompletionCollector` to be null and the group chat token
  collector fallback to silently fail. Both now have `[DoNotAutoRegister]`.

#### Avalonia Integration

- **NDLRAVA004: primary constructors are not supported.** The generator now
  emits an error diagnostic when `[GenerateAvaloniaDesignTimeConstructor]` is
  applied to a class with a primary constructor. Chaining via `: this(default!)`
  would pass null for all captured parameters, causing `NullReferenceException`
  during design-time preview. The diagnostic directs users to use a regular
  constructor with fields instead.

## [0.0.2-alpha.32] - 2026-04-12

Group chat diagnostics accuracy and Avalonia packaging fix.

### Fixed

#### Agent Framework

- **Per-stage duration in group chat workflows.** Stage duration
  previously measured streaming event delivery gap (~0.5ms) instead of
  actual agent execution time. Now uses completion timestamps captured
  at LLM call time by the chat client middleware.

- **Per-stage token attribution in group chat workflows.** All tokens
  were assigned to the first stage because the collector drain grabbed
  every completion before subsequent stages were built. Completions are
  now partitioned across stages using agent name matching or round-robin
  interleaving when agent names are unavailable.

- **`ChatCompletionDiagnostics.AgentName` property.** Each LLM
  completion now carries the agent name that triggered it (when the
  `AgentRunDiagnosticsBuilder` AsyncLocal propagates). Enables
  name-based completion-to-stage attribution.

#### Avalonia Integration

- **Generator DLL missing from NuGet package.** The
  `NexusLabs.Needlr.Avalonia` source generator was marked
  `IsPackable=false` (correct) but was not embedded as an analyzer asset
  in `NexusLabs.Needlr.Avalonia.Attributes`. Consumers got the marker
  attribute but no generator to act on it. The generator is now packed
  into `analyzers/dotnet/cs` in the Attributes package.

## [0.0.2-alpha.31] - 2026-04-12

Source generator correctness fixes, Avalonia design-time constructor
generation, and group chat diagnostics fix.

### Added

#### Avalonia Integration

- **`NexusLabs.Needlr.Avalonia` package** — source generator that emits
  parameterless design-time constructors for Avalonia controls using
  Needlr's constructor-based DI. Opt in per class with
  `[GenerateAvaloniaDesignTimeConstructor]`. The generated constructor
  includes a `Design.IsDesignMode` guard (throws at runtime),
  `InitializeComponent()` call, and `#pragma warning disable CS8618`.
  Diagnostics: NDLRAVA001 (not partial), NDLRAVA002 (already has
  parameterless ctor), NDLRAVA003 (no parameterized ctor).

- **`NexusLabs.Needlr.Avalonia.Attributes` package** — contains the
  `[GenerateAvaloniaDesignTimeConstructor]` marker attribute
  (netstandard2.0).

### Fixed

#### Source Generation

- **Array-of-objects tool parameter schema.** The agent framework
  generator now emits full JSON schema (`items` with `properties` and
  `required`) for `T[]` / `IReadOnlyList<T>` parameters where `T` is a
  complex object. Deserialization uses AOT-safe `JsonElement.TryGetProperty`
  extraction instead of `JsonSerializer.Deserialize<T>`.

- **Richest satisfiable constructor selection.** The type registry
  generator now picks the constructor with the most resolvable
  parameters instead of the first match. Fixes cases where a type with
  both a parameterless and a parameterized constructor would get
  registered with the parameterless one.

#### Agent Framework

- **Group chat workflows reporting 0 tokens in `RunWithDiagnosticsAsync`.**
  MAF's `InProcessExecution.RunStreamingAsync()` runs the workflow on a
  separate async context where the `AsyncLocal`-based diagnostics holder
  doesn't propagate. The `DiagnosticsChatClientMiddleware` always captures
  completions into a `ConcurrentQueue` regardless of async context, but
  the simple `RunWithDiagnosticsAsync` overloads defaulted to
  `NullChatCompletionCollector`. Fix: `IAgentDiagnosticsAccessor` now
  exposes `CompletionCollector` (wired automatically by `UsingDiagnostics()`)
  so the pipeline extensions resolve the real collector without requiring
  consumers to pass it explicitly.

## [0.0.2-alpha.30] - 2026-04-12

Agent framework usability, source generator hardening for UI frameworks
(Avalonia, MAUI), and hosting/bootstrapping conveniences.

### Added

#### Source Generation

- **`ExcludeNamespacePrefixes` on `[GenerateTypeRegistry]`.** Blacklist
  namespace prefixes to prevent the source generator from scanning
  framework types (Avalonia, MAUI, etc.) that Needlr would otherwise
  try to register. Applied after inclusion — if a type matches both an
  include and exclude prefix, it is excluded. Configure via attribute:
  ```csharp
  [assembly: GenerateTypeRegistry(
      ExcludeNamespacePrefixes = new[] { "Avalonia" })]
  ```
  Or via MSBuild property in `Directory.Build.props`:
  ```xml
  <NeedlrExcludeNamespacePrefix>Avalonia;Microsoft.Maui</NeedlrExcludeNamespacePrefix>
  ```

- **Avalonia example app** (`src/Examples/SourceGen/AvaloniaDemoApp`).
  Minimal Avalonia 11.x desktop app demonstrating Needlr source
  generation with `ExcludeNamespacePrefixes`, constructor-injected
  views (no service locator), and AOT/trimming readiness. `MainWindow`
  takes `GreetingService` in its constructor, resolved from DI.

#### Agent Framework

- **Per-agent instruction overrides on `CreateAgent` and
  `CreateGroupChatWorkflow`.** New overloads on `IAgentFactory` and
  `IWorkflowFactory` that accept an `Action<AgentFactoryOptions>`
  (or `Action<Type, AgentFactoryOptions>` for workflows) callback.
  The options are pre-populated from the `[NeedlrAiAgent]` attribute;
  the callback overrides what it needs — most commonly `Instructions`
  for per-run dynamic prompts. Enables patterns like BrandGhost's
  article production workflow where the writer agent needs per-article
  funnel-stage rules injected at runtime while base writing rules
  stay static on the attribute.

- **`AgentResponse.GetText()` extension method.** Concatenates
  non-empty `ChatMessage.Text` properties from `AgentResponse.Messages`
  with newline separators. Skips whitespace-only messages. Returns
  `null` when no text content is present. Standard replacement for the
  text-extraction helper every consumer ends up writing.

#### Hosting & Bootstrapping

- **`NeedlrBootstrapper` lifecycle wrapper** in
  `NexusLabs.Needlr.Hosting`. Wraps the `IHost` build-and-run
  lifecycle with structured error handling, logging, and graceful
  shutdown.

- **`NexusLabs.Needlr.Serilog` package.** Provides
  `NeedlrSerilogBootstrapper` for Serilog-based applications with
  integrated bootstrap logging.

- **`UsingLogger` extension** on `CreateWebApplicationOptions` and
  `CreateHostOptions` for injecting a logger into the build pipeline.

- **`UsingOptions(ILogger)` convenience overload** on
  `WebApplicationSyringe` and `HostSyringe`.

- **`UsingCurrentProcessCliArgs` and `UsingCurrentProcessArgs`**
  convenience methods for passing command-line arguments without
  explicit `string[] args` plumbing.

### Fixed

- **Dot-boundary namespace prefix matching.** `"Avalonia"` now matches
  `"Avalonia"` and `"Avalonia.Controls"` but NOT `"AvaloniaDemoApp"`.
  Previously, `StartsWith` without a boundary check caused false
  matches across namespace boundaries. Both `IncludeNamespacePrefixes`
  and `ExcludeNamespacePrefixes` use the new boundary-aware matcher.

- **Inaccessible interfaces skipped in generated code.** When a type
  extends a framework base class (e.g., `MainWindow : Window`), the
  generator no longer emits `typeof()` for `internal` interfaces
  inherited from that base class. Previously this produced CS0122
  errors. Accessible interfaces are still registered normally.

- **Obsolete types no longer silently dropped from DI.** The generator
  now emits `#pragma warning disable CS0618, CS0619` in all generated
  files instead of skipping `[Obsolete]` types/interfaces. This
  ensures obsolete-but-still-needed services remain in the container
  while suppressing the warning only in generated code — consumers
  still see `[Obsolete]` warnings in their own code.

- **Release script push race.** `release.ps1` now pushes tags first
  (fires `release.yml` immediately) then rebases + pushes `HEAD`
  separately, handling the coverage-badge bot race gracefully instead
  of failing.

### Changed

- **Getting Started and README recommend `NexusLabs.Needlr.Build`** as
  the paved path for source generation instead of manual
  `NexusLabs.Needlr.Generators` + `Generators.Attributes` references.

- **Release script pack step parallelized.** Replaced per-project
  sequential `dotnet pack` loop (~50 projects × ~10s) with
  solution-level `dotnet pack` that lets MSBuild parallelize
  internally. Reduces local release time by ~6-8 minutes.

## [0.0.2-alpha.29] - 2026-04-12

Consumer extensibility release driven by BrandGhost migration feedback.
Addresses five gaps (G1–G5) that blocked full adoption of the agent
framework's diagnostics, workspace, and provider-selector systems.

### Added

- **Configurable meter and ActivitySource names
  (`AgentFrameworkMetricsOptions`).** Consumers with existing
  Prometheus/Grafana dashboards keyed to a specific meter name can set
  `MeterName` (and optionally `ActivitySourceName`) via the syringe:
  ```csharp
  .UsingAgentFramework(af => af
      .ConfigureMetrics(o => o.MeterName = "BrandGhost.Agents"))
  ```
  Defaults to `"NexusLabs.Needlr.AgentFramework"`. `AgentMetrics` now
  creates its `Meter` and `ActivitySource` from these options instead
  of hardcoded constants.

- **`IWorkspace.ReadFileAsMemory(string path)`** — returns
  `ReadOnlyMemory<char>` for zero-copy line enumeration via
  `MemoryExtensions.EnumerateLines()`. `InMemoryWorkspace` returns
  `string.AsMemory()` over its internal storage.

- **`IWorkspace.ListDirectory(string directory, int maxDepth = 2)`** —
  produces a tree-formatted directory listing with Unicode tree
  characters. `InMemoryWorkspace` builds the tree from its
  `ConcurrentDictionary` keys with depth-limited recursion.

- **`AgentExecutionContext.Workspace` parameter.** The default
  `AgentExecutionContext` record now accepts an optional `IWorkspace?`
  constructor parameter. When provided, the workspace is automatically
  injected into the `Properties` bag under the `IWorkspace` type key.

- **`GetWorkspace()` / `GetRequiredWorkspace()` extensions on
  `IAgentExecutionContext`.** Typed convenience methods for resolving
  the workspace from context without manual casting. `GetRequired`
  throws `InvalidOperationException` if no workspace is available.

- **Per-partition quota on `IQuotaGate`.** `TryReserveAsync` and
  `ReleaseAsync` now accept an optional `string? quotaPartition`
  parameter. When provided, the quota gate can scope tracking to that
  partition (user, tenant, API key — the string is opaque to the
  framework). When `null`, quota is global (backward-compatible).

- **`QuotaPartitionSelector` delegate.** `TieredProviderSelector` now
  reads the partition from ambient `IAgentExecutionContextAccessor`
  via a configurable delegate. Default: `context?.UserId`. Consumers
  that need a different partitioning strategy (e.g., tenant ID) provide
  a custom delegate at construction time.

### Changed

- **Activity span tags aligned to OpenTelemetry GenAI semantic
  conventions.** Renamed tags across all three diagnostics middleware
  classes:
  - `agent.name` → `gen_ai.agent.name`
  - `llm.tokens.input` → `gen_ai.usage.input_tokens`
  - `llm.tokens.output` → `gen_ai.usage.output_tokens`
  - `llm.model` → `gen_ai.response.model`
  - `tool.name` → `agent.tool.name`

  Added `agent.tool.sequence`, `agent.chat.sequence` tags for call
  ordering. Added `tool.custom.{key}` tag emission for custom tool
  metrics on Activity spans.

  Activity operation names now include the subject for trace
  readability: `"agent.run {agentName}"`, `"agent.tool {toolName}"`,
  `"agent.chat {model}"`.

- **`TieredProviderSelector` constructor** now requires
  `IAgentExecutionContextAccessor` (previously only took providers +
  quota gate). This is a **breaking change** for consumers constructing
  the selector directly. The accessor is registered automatically by
  `UsingAgentFramework()`.

### Breaking Changes

- **`IQuotaGate` interface** — `TryReserveAsync` and `ReleaseAsync`
  signatures gained a `string? quotaPartition` parameter. All
  implementations must update. `AlwaysGrantQuotaGate` is already
  updated.

- **`IWorkspace` interface** — gained `ReadFileAsMemory` and
  `ListDirectory` (no default interface implementations). All
  implementations must add these methods.

- **`TieredProviderSelector` constructor** — added required
  `IAgentExecutionContextAccessor` parameter.

## [0.0.2-alpha.28] - 2026-04-12

Diagnostics extensibility release. Makes the agent-framework diagnostics
write-side public so consumers can build their own middleware, adds
distributed tracing spans via `ActivitySource`, and introduces typed
context property extensions. Also includes docs-pipeline improvements
(sitemap index, version switcher, articles fix, footer) and CI
hardening.

### Added

#### Diagnostics extensibility

- **`IAgentDiagnosticsWriter` is now public.** Consumers can resolve it
  from DI and call `Set(diagnostics)` from their own middleware after
  capturing an agent run. Previously internal — blocked consumers from
  writing custom diagnostics middleware that integrated with Needlr's
  accessor.

- **`AgentRunDiagnosticsBuilder` is now public.** All methods (`StartNew`,
  `GetCurrent`, `AddToolCall`, `AddChatCompletion`, `RecordInputMessageCount`,
  `RecordOutputMessageCount`, `RecordFailure`, `Build`, `ClearCurrent`)
  are public. Consumers use the builder in their own middleware to
  accumulate per-run diagnostics the same way Needlr's built-in
  middleware does. Thread-safe via `ConcurrentQueue` and
  `Interlocked.Increment` for sequence numbers.

- **`ActivitySource` distributed tracing spans on all three diagnostics
  middleware layers.** Needlr's diagnostics middleware now creates
  `System.Diagnostics.Activity` spans for:
  - `agent.run` (ActivityKind.Internal) — tags: `agent.name`,
    `agent.status`, `agent.tokens.total`, `agent.duration_ms`
  - `agent.tool.call` (ActivityKind.Internal) — tags: `tool.name`,
    `tool.status`, `tool.duration_ms`
  - `agent.chat.completion` (ActivityKind.Client) — tags: `llm.model`,
    `llm.status`, `llm.duration_ms`, `llm.tokens.input`,
    `llm.tokens.output`, `llm.tokens.total`

  Spans are exported automatically when an OpenTelemetry listener is
  registered (e.g., Jaeger, Zipkin, OTLP). Zero overhead when no
  listener is active (`StartActivity()` returns null, all tag calls
  are null-conditional no-ops).

- **`IAgentMetrics.ActivitySource` property.** Exposes the framework's
  `ActivitySource` (named `NexusLabs.Needlr.AgentFramework`) so
  consumers and middleware can create spans from the same source. The
  `Meter` for counters/histograms remains alongside it.

- **`AgentExecutionContextExtensions.GetProperty<T>()`** — typed
  read-only extensions on `IAgentExecutionContext` for retrieving
  properties from the `Properties` bag without manual casting. Keyed
  by `typeof(T).FullName` by default, or by explicit string key.
  No `SetProperty` — the `Properties` bag is `IReadOnlyDictionary`
  by design; consumers that need mutable state implement their own
  `IAgentExecutionContext` with typed properties and populate the
  bag at construction time.

#### Documentation site

- **API version switcher.** Runtime-rendered `<select>` dropdown on
  every `/api/` page (and only those pages) for jumping between stable,
  dev, and preserved `v0.0.x-alpha.N` snapshots. Data source:
  `/api/versions.json` generated at release time by
  `scripts/generate-versioned-api-docs.sh`. Includes a back-link
  ("← All packages in {version}") for navigating back to the version's
  catalog from deep pages.

- **Sitemap index with per-slice sub-sitemaps.** Replaces mkdocs's
  default flat `sitemap.xml` (which only contained ~488 URLs from the
  current build) with a sitemaps.org-compliant index referencing 11
  sub-sitemaps: `sitemap-main.xml`, `api/dev/sitemap.xml`,
  `api/stable/sitemap.xml`, and one per preserved version. Total
  indexed URLs went from ~488 to ~3,586 — full coverage of all API
  reference versions for search engine crawlers.

- **Articles page populated.** Fixed the articles generator
  (`scripts/generate-articles.py`) which was silently producing an
  empty page because devleader.ca's `/tags/Needlr` page was redesigned
  and no longer uses `<article>` elements. Switched to `/search/needlr`
  with a post-parse filter requiring `Needlr` in the article's category
  list. 12 articles now appear.

- **SEO footer.** Replaced the default "Made with Material for MkDocs"
  attribution with three keyword-rich backlinks to Dev Leader (blog),
  Dev Leader links (channels hub), and BrandGhost (business). Each
  link uses descriptive anchor text for SEO value. Stacked on separate
  lines for readability.

### Fixed

- **Coverage report no longer includes example apps.** Added
  `[assembly: ExcludeFromCodeCoverage]` to all projects under
  `src/Examples/` via `Directory.Build.props` `AssemblyAttribute`
  item. Coverlet skips these assemblies during instrumentation.
  Library coverage (`NexusLabs.Needlr.*`) is unaffected — example
  tests still run and exercise library code paths as before.

- **mkdocs template override no longer leaks publicly.** Relocated
  `docs/overrides/` to `overrides/` at repo root (outside `docs_dir`)
  so mkdocs no longer copies the Jinja template to `site/overrides/`.

- **NBGV `$GITHUB_ENV` race condition handled.** Nerdbank.GitVersioning's
  `SetCloudBuildVariables` MSBuild task writes version metadata to
  `$GITHUB_ENV` during every project build. In per-project loops
  (`test-packages.ps1`, release.yml pack step), concurrent writes
  produce torn lines the runner rejects. Fixed by redirecting
  `GITHUB_ENV`/`GITHUB_OUTPUT`/`GITHUB_STEP_SUMMARY` to throwaway
  temp files in every per-project loop step.

- **`/api/` version catalog filtered to live content only.** The catalog
  now intersects git tags with actual directories on gh-pages (fetched
  via GitHub API), so historical tags without deployed content don't
  produce broken links.

### Changed

- **Removed unused `mike` from CI/CD toolchain.** Both `ci.yml` and
  `release.yml` installed `mike` via pip but never invoked it. Removed.

### Removed

- **Legacy `/dev/` site on gh-pages.** Deleted ~60 HTML files left
  behind by an abandoned `mike deploy dev` experiment. Not referenced
  by the current site.

## [0.0.2-alpha.27] - 2026-04-11

Documentation pipeline infrastructure release. No library code changes. This
release exists to validate the new ci.yml/release.yml disjoint-ownership
model for the versioned API docs site end-to-end.

### Fixed

- **Docs pipeline: ci.yml no longer clobbers stable API docs.** Previously
  every push to `main` ran an "Ensure Stable API Docs Are Available" step
  that wrote a placeholder to `docs/api/stable/index.md`, which mkdocs then
  built into `./site/api/stable/` and `peaceiris/actions-gh-pages@v4`
  deployed to `gh-pages` — silently overwriting the real stable API
  reference that `release.yml` had published at release time. That step is
  deleted. `ci.yml` now has a symmetric `rm -rf ./site/api/stable
  ./site/api/v*` pre-deploy step so `keep_files: true` preserves whatever
  `release.yml` last wrote to those paths.

- **Docs pipeline: release.yml no longer fights `.gitignore` to commit
  generated docs back to main.** The "Commit Stable API Docs to Main" step
  tried to `git add docs/api/v<version>/` and `docs/api/stable/` but those
  paths are in `.gitignore`, so every release required three separate
  "root-cause fix" commits to work around the repo's own rules. That step
  is deleted. Generated docs now live exclusively on `gh-pages`;
  per-version `/api/v<N>/` directories are preserved forever because
  neither workflow ever writes to paths outside its owned slice.

- **Docs pipeline: `/api/` version catalog now enumerates from git tags
  instead of walking the working tree.** `scripts/generate-versioned-api-docs.sh`
  previously listed "All Versions" by scanning `docs/api/v*/` on disk,
  which silently lost history on every release because `.gitignore` blocks
  per-version directories from ever being committed to main — the live
  catalog therefore only ever listed the current version plus nothing
  else. Catalog now uses `git tag --list 'v*' --sort=-version:refname`
  and emits trailing-slash links that resolve at runtime against the
  preserved `gh-pages` content.

### Changed

- **`docs/api/stable/index.md` is now a tracked static placeholder.** The
  file exists solely so `mkdocs build --strict` has a valid source for
  `/api/stable/`. Its only job is to point readers at the live site; it is
  never overwritten by generated content on `gh-pages`. File header
  explains this so future maintainers don't assume it's stale.

- **Docs pipeline ownership documented in the Copilot instruction file.**
  `.github/instructions/docs.instructions.md` now contains an ownership
  table showing which workflow owns which `/api/*` slice, the invariant
  that neither workflow writes generated docs back to main, and the
  git-tag-driven catalog rule.

## [0.0.2-alpha.26] - 2026-04-10

This release ships the Agent Framework progress reporting framework, the
`[HttpClientOptions]` source generator, 27 new analyzer diagnostics, and
a critical fix for the ASP.NET Core web application path where
source-generated `[Options]` classes were silently not binding. It also
hardens the release process itself with a new pre-flight guardrail and
a full maintainer guide so analyzer shipping is no longer tribal
knowledge.

### Added

#### Agent Framework — progress reporting framework

- **Progress reporting core types**: `IProgressReporter`,
  `IProgressReporterFactory`, `IProgressReporterAccessor`,
  `IProgressSink`, `IProgressEvent`, and the concrete event hierarchy
  (`WorkflowStartedEvent`, `WorkflowCompletedEvent`, `AgentInvokedEvent`,
  `AgentCompletedEvent`, `AgentFailedEvent`, `AgentHandoffEvent`,
  `LlmCallStartedEvent`, `LlmCallCompletedEvent`, `LlmCallFailedEvent`,
  `ToolCallStartedEvent`, `ToolCallCompletedEvent`, `ToolCallFailedEvent`,
  `BudgetUpdatedEvent`, `BudgetExceededEvent`,
  `SuperStepStartedProgressEvent`, `SuperStepCompletedProgressEvent`).
  Events carry full correlation context (`WorkflowId`, `AgentId`,
  `ParentAgentId`, `Depth`, `SequenceNumber`) for downstream SSE
  streams, dashboards, and trace diagrams.

- **`ChannelProgressReporter`**: Non-blocking reporter backed by a
  bounded `Channel<IProgressEvent>` with a single background consumer
  so slow sinks (database, network) don't block the agent pipeline.
  Child reporters share the parent's channel instead of spawning new
  background tasks.

- **`IProgressReporterErrorHandler`**: Optional DI-registrable handler
  that receives sink exceptions. Without it, sink errors were silently
  swallowed; with it, failures surface through a pluggable callback
  (default: `NullProgressReporterErrorHandler` no-op).

- **`[ProgressSinks]` source generator**: Apply `[ProgressSinks(typeof(...))]`
  to an agent class and the generator emits
  `GetXxxAgentProgressSinkTypes()` (metadata) and
  `BeginXxxAgentProgressScope()` (auto-wiring — returns a composite
  `IDisposable` that tears down both the reporter scope and any sink
  instances implementing `IDisposable`, preventing leaks). Also emits
  `AddGeneratedProgressSinks()` for DI registration of all discovered
  sink types across the assembly.

- **Syringe-level `AddProgressSink<T>()`**: Fluent sink registration
  on `AgentFrameworkSyringe` that honors AOT trim analysis by capturing
  each closed generic at the call site. Sinks flow through DI to every
  reporter created by `IProgressReporterFactory`.

- **`SpectreProgressApp` example**: Real-time Spectre Console dashboard
  with animated spinner, live-updating agent rows, LLM call tracking,
  tool call status, budget display, SuperStep step counter, and
  `AgentFailedEvent` handling. Demonstrates the full progress pipeline
  against `SimpleAgentFrameworkApp.Agents`.

- **`WorkflowRunOptions` record**: Consolidates the seven-parameter
  `RunWithDiagnosticsAsync` overload into a single options object with
  `DiagnosticsAccessor`, `ProgressReporter`, `CompletionCollector`,
  `ProgressReporterAccessor`, and `CancellationToken` properties.
  Backward-compatible overloads remain.

#### Agent Framework — diagnostics & telemetry

- **`IAgentMetrics` with OpenTelemetry Meter**: Emits `needlr.agents.tokens.used`,
  `needlr.agents.llm.duration`, and `needlr.agents.tool.calls` counters
  via `System.Diagnostics.Metrics.Meter` for OpenTelemetry export.

- **Agent run diagnostics**: `AgentRunDiagnosticsBuilder`,
  `IAgentDiagnosticsAccessor`, `IAgentDiagnosticsWriter`,
  `AgentRunDiagnostics`, `AgentStageResult`, and `IPipelineRunResult`
  for per-call telemetry aggregation. Captures per-LLM-call timing,
  token usage, tool call metadata, and stage breakdown across sequential
  and handoff workflows.

- **`IAgentExecutionContextAccessor`**: Ambient identity accessor for
  threading user/orchestration identity through agent execution via
  `AsyncLocal`, scoped via `BeginScope`.

- **`IPipelineRunResult`**: Aggregated stage-level diagnostics result
  with per-stage `ResponseText`, `Duration`, `Diagnostics`, and
  `AggregateTokenUsage`. Supports handoff, sequential, and group-chat
  topologies.

- **`IAgentOutputCollector<T>`**: Structured typed output capture for
  agent runs. `AddAgentOutputCollector<T>()` DI registration extension
  wires it into the factory pipeline.

- **`IWorkspace` opt-in abstraction**: `InMemoryWorkspace` default
  implementation provides a simple key/value scratchpad for agents to
  share state across turns without leaking into the DI container.

- **`TieredProviderSelector`**: Generic quota-gated fallback selector
  for primary/secondary/tertiary provider hierarchies. Used by the
  example app to demonstrate automatic failover.

- **`NexusLabs.Needlr.AgentFramework.Testing` package**: Test-only
  helpers for dogfooding `IAgentScenario`, mock chat clients, and
  scenario runner fakes.

- **`[AsyncLocalScoped]` source generator**: Emits a generated
  `IDisposable` scope class for any type marked with the attribute,
  replacing hand-rolled `AsyncLocal<T>` + `BeginScope` boilerplate.

#### Agent Framework — middleware

- **`UsingToolResultMiddleware()`**: Tool result wrapping middleware
  that surfaces tool failures as structured `ToolResult<T>` records
  instead of raw exceptions bubbling through the agent response.

- **`UsingResilience()`**: Polly-backed retry policy for agent runs
  with `[AgentResilience]` attribute for per-agent override. Resilience
  plugin is AOT-compatible.

- **`UsingTokenBudget()`**: Input/output/total token budget enforcement
  with granular `ITokenBudgetTracker.BeginScope(maxTokens)` and
  `BudgetExceededEvent` emission. Cancels in-flight runs via
  `CancelRunAsync` when exceeded.

- **`UsingDiagnostics()`**: Chat and function call middleware that
  emits per-call diagnostics into the `IAgentDiagnosticsAccessor` for
  downstream `IPipelineRunResult` aggregation.

#### HttpClient options source generator

- **`[HttpClientOptions]` attribute**: Decorate a class implementing
  `INamedHttpClientOptions` and the generator emits the full
  `services.AddHttpClient(name).ConfigureHttpClient(...)` wiring plus
  `services.AddOptions<T>().BindConfiguration(...)` for the matching
  configuration section. Supports v1 capability interfaces
  (`IHttpClientTimeout`, `IHttpClientUserAgent`, `IHttpClientBaseAddress`)
  with capability-conditional emission.

- **Analyzer diagnostics NDLRHTTP001-006**: Compile-time validation for
  `[HttpClientOptions]` misuse — missing `INamedHttpClientOptions`
  implementation, conflicting name sources, non-literal `ClientName`
  property body, empty resolved name, duplicate names, invalid
  `ClientName` property shape.

- **`HttpClientExample` console project**: End-to-end example
  demonstrating typed HTTP client registration + configuration binding.

- **`docs/http-clients.md`**: Full feature page with `Quick Start`,
  capability matrix, `Attribute Reference`, and `Analyzers` table.

#### Analyzers — 27 new diagnostics shipped

See the [Shipped analyzers](#shipped-analyzers-02-alpha26) section
at the bottom of this entry for the full list.

- **NDLRCOR012**: Disposable captive dependency — longer-lived service
  holds `IDisposable` with shorter lifetime.
- **NDLRCOR016**: `[DoNotAutoRegister]` applied directly to a plugin
  class is redundant (and was the silent-plugin-suppression bug fixed
  in alpha.20).
- **NDLRGEN022**: Disposable captive dependency detected by source gen.
- **NDLRGEN031-034**: Provider attribute analyzer diagnostics —
  `[Provider]` on class requires partial modifier, interface has
  invalid member, property uses concrete type, circular provider
  dependency.
- **NDLRHTTP001-006**: `[HttpClientOptions]` diagnostics (see above).
- **NDLRMAF001-014**: Full Microsoft Agent Framework analyzer suite —
  topology validation, cyclic handoff detection, function group
  reference checking, sequence order validation, termination condition
  diagnostics, `[AgentFunction]` description checks. **First-ever ship
  for `NexusLabs.Needlr.AgentFramework.Analyzers`.**

#### Release process hardening

- **`RELEASING.md` at the repo root**: Short fast-lookup maintainer
  checklist. GitHub auto-links it at the top of the repo page.

- **`docs/releasing.md`**: Full in-depth maintainer guide covering
  prerequisites, version numbering, every pre-release gate with
  rationale, the mechanical procedure for moving analyzer rules
  between `AnalyzerReleases.Unshipped.md` and `Shipped.md` (including
  the non-obvious RS2007 "base version only" rule), CHANGELOG
  conventions, post-release verification, rollback procedure, and
  a historical gotchas section pointing at past fix commits for the
  most common mistakes.

- **Analyzer release tracking pre-flight gate in `scripts/release.ps1`**:
  Scans every `AnalyzerReleases.Unshipped.md` under `src/` and blocks
  the release if any file contains unshipped rule rows. Prints every
  pending rule ID, the file it lives in, the exact Shipped.md header
  to use, and the canonical `chore: ship analyzers for <version>`
  commit message. This closes a repeatable gap where past alpha
  releases required retroactive "fix analyzer release format" commits
  (see `83ef38ab`, `6b7e1166`, `22bd5b64`).

#### Examples

- **`SpectreProgressApp` example** (see progress reporting section above).
- **`MinimalWebApiSourceGen` + `[Options]` demonstration**: Added
  `WeatherOptions` decorated with `[Options(ValidateOnStart = true)]`
  and `WeatherProvider` consuming `IOptions<WeatherOptions>` to prove
  the web path `[Options]` fix end-to-end against a real
  `WebApplicationBuilder`.
- **`HttpClientExample`** (see HttpClient section above).
- **Spectre budget events**: Dashboard shows current/max token budget
  with live updates on `BudgetUpdatedEvent`.
- **SuperStep and AgentFailed rendering**: Dashboard handles
  `SuperStepStartedProgressEvent`, `SuperStepCompletedProgressEvent`,
  and `AgentFailedEvent`.
- **`TieredProviderSelector` fallback** demonstrated in the example app.
- **`IToolMetricsAccessor.AttachMetric`** demonstrated in a tool.

### Fixed

- **[Critical] ASP.NET Core web path silently skipped source-generated
  `[Options]` binding**:
  `WebApplicationSyringe.BuildWebApplication()` invoked
  `BaseSyringe.GetPostPluginRegistrationCallbacks()` which only returned
  user-registered callbacks, **never** invoking
  `SourceGenRegistry.TryGetOptionsRegistrar` the way the console path
  (`ConfiguredSyringe.BuildServiceProvider(IConfiguration)`) does. As a
  result, every `[Options]`-decorated class in a web application
  received a default-constructed `IOptions<T>.Value` with empty/default
  properties. `ValidateOnStart` never fired because the validator was
  also registered via the same dropped callback.

  The fix routes `TryGetOptionsRegistrar` and `TryGetExtensionRegistrar`
  through the web path's post-plugin callback list, resolving
  `IConfiguration` from `WebApplicationBuilder.Services` at callback
  invocation time (the host registers it as an `ImplementationInstance`
  before our callbacks run, with a fallback to a minimal temporary
  provider for exotic factory registrations).

  15 integration tests in
  `OptionsWebApplicationSourceGenTests` lock this behavior in, covering
  `IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`, named
  options, `[Required]` validation pass/fail via `ValidateOnStart`,
  external validators, nested configuration, array and dictionary
  binding, immutable records, and explicit console-vs-web parity
  verification against the same configuration.

  **Consumer action**: projects on alpha.25 or earlier that use
  `[Options]` on the ASP.NET Core web path and rely on manual
  `AddOptions<T>().BindConfiguration(...)` workarounds can now remove
  those workarounds after upgrading. Projects that did not have a
  workaround and silently shipped with empty `IOptions<T>.Value` should
  audit their options consumers.

- **AOT trim analysis IL2072 in `AddProgressSink<T>()`**: The
  `AgentFrameworkSyringe.ProgressSinkTypes` field stored raw `List<Type>`
  entries, and the `IProgressReporterFactory` factory lambda iterated
  them calling the non-generic
  `ActivatorUtilities.CreateInstance(sp, Type)` overload. The AOT trim
  analyzer rejected this because `List<Type>.Current` has no
  `[DynamicallyAccessedMembers(PublicConstructors)]` annotation. Fix:
  replace `ProgressSinkTypes` with
  `List<Func<IServiceProvider, IProgressSink>>`, where each factory is
  constructed at the `AddProgressSink<TSink>()` call site via the
  generic `ActivatorUtilities.CreateInstance<TSink>(sp)` overload with
  `[DynamicallyAccessedMembers(PublicConstructors)]` annotated on
  `TSink`. The constructor requirements now flow through the generic
  type parameter without needing to annotate a shared collection.

- **Source generator closure race in `SyringeExtensionsForAgentFramework.cs`**:
  `syringeSinkTypes` was a closure-captured `List<Type>?` mutated by
  the `IAgentFactory` factory lambda and read by the
  `IProgressReporterFactory` factory lambda. If DI resolved the
  progress reporter factory before the agent factory, the list was
  still `null` and syringe-added sinks vanished silently. Fix:
  introduce `BuiltAgentFrameworkSyringe` DI singleton — both factories
  resolve the same singleton, so the configure delegate runs exactly
  once and both consumers see identical state regardless of resolution
  order.

- **`AgentFailedEvent` was declared but never emitted**: the type
  existed in the `IProgressEvent` hierarchy but no code path fired it.
  Fix: `PipelineRunExtensions` now handles MAF `ExecutorFailedEvent`
  and `WorkflowErrorEvent` from the stream, emits `AgentFailedEvent`
  with the failing executor ID, and also emits it in the outer catch
  block if an exception escapes the stream with a known
  `currentExecutorId`.

- **`ProgressReporter` and `ChannelProgressReporter` silently swallowed
  sink exceptions**: the error paths in both reporters had bare
  `catch { }` blocks. Fix: both now invoke a registered
  `IProgressReporterErrorHandler` instead. Default behavior
  (`NullProgressReporterErrorHandler`) is a no-op so existing consumers
  see no change; projects that need visibility can register their own
  handler that forwards to `ILogger<T>` or an in-process diagnostic bus.

- **Generated `BeginXxxAgentProgressScope` leaked `IDisposable` sinks**:
  the generator emitted a scope method that returned only the reporter
  accessor scope handle, so any sink instances created via
  `ActivatorUtilities.CreateInstance` that implemented `IDisposable`
  were orphaned when the scope ended. Fix: generator now emits a
  `CompositeDisposable` that ties both the reporter scope and the
  sinks' disposal to the returned handle.

- **First-wins `TryAddSingleton<IProgressSink, TSink>()` in generator**:
  when multiple agents each declared a different `[ProgressSinks]` type,
  the generated `AddGeneratedProgressSinks()` used `TryAddSingleton`
  against the `IProgressSink` interface, causing only the first sink
  to register. Fix: generator now emits `TryAddSingleton<TSink>()`
  (on the concrete type) plus
  `AddSingleton<IProgressSink>(sp => sp.GetRequiredService<TSink>())`
  so each sink stacks correctly in `GetServices<IProgressSink>()`.

- **`SuperStepStartedProgressEvent` / `SuperStepCompletedProgressEvent`
  were emitted but duplicate `WorkflowStartedEvent` were firing
  alongside**: the workflow event loop incorrectly treated super-step
  boundaries as workflow starts. Fix: dedicated SuperStep event paths
  with proper `StepNumber` tracking.

- **Tool call progress events not propagating through
  `IProgressReporterAccessor`**: the tool-call middleware was writing
  to the wrong reporter instance. Fix: resolve `Current` from the
  accessor on each call and forward events there.

- **Per-LLM-call timing was occasionally missing when `AsyncLocal`
  diagnostics didn't propagate across runtime context boundaries**:
  added a `ConcurrentQueue`-based bypass and a fallback event-stream
  timing path so per-call duration is always captured.

- **`BeginCapture` was called after `RunStreamingAsync` in pipeline
  runner**: diagnostics scope was opened too late to capture the first
  stage. Fix: move `BeginCapture` before `RunStreamingAsync` so every
  stage is captured.

- **Token budget cancellation was inconsistent**: budget exceeded
  sometimes threw `OperationCanceledException` from a retry handler
  instead of propagating out to the caller. Fix: dedicated
  `BudgetCancellationToken` + explicit `CancelRunAsync` +
  `ThrowIfCancellationRequested` call pattern. Retry's `ShouldHandle`
  now excludes `OperationCanceledException` so budget cancels don't
  retry.

- **Missing DI registrations in `Func<AgentFrameworkSyringe>` overload
  of `UsingAgentFramework()`**: the parameterless factory overload
  didn't register the same infrastructure services as the
  `Func<AgentFrameworkSyringe, AgentFrameworkSyringe>` overload. Fix:
  route the second overload through the first so both paths share the
  same `RegisterAgentFrameworkInfrastructure` helper.

- **Duplicate agent names in `PipelineRunResult.Responses`**: when the
  same agent appeared in multiple stages, responses were overwritten
  in a dictionary keyed by agent name. Fix: key by stage index instead.

- **Agent name hardcoded to `"Agent"` in some paths**: now resolved
  at runtime from the agent factory.

- **`AIAgentBuilder.UseResilience()` did not handle
  `OperationCanceledException`**: caused budget cancellations to leak
  through as uncaught exceptions. Fix: add to `ShouldHandle` list.

- **`ActivitySource` field was declared but never used** in
  `IAgentMetrics`: removed the dead field and added a TODO documenting
  that distributed tracing is deferred to a future release.

- **Meter disposal missing in test cleanup**: `AgentMetrics` was not
  disposing its `Meter` in test tear-down, causing test pollution.
  Fix: implement `IDisposable` and dispose in test fixtures.

- **Missing `IProgressEvent.ParentAgentId` propagation via
  `CreateChild()`**: child reporters inherited the parent's `AgentId`
  instead of setting it as `ParentAgentId`. Fix: `CreateChild()` now
  correctly sets `ParentAgentId = parent.AgentId` and increments
  `Depth`.

- **`NullProgressReporter.NextSequence()` always returned 0**: masked
  bugs because all events got sequence 0 if the null reporter was
  accidentally in use. Fix: `Interlocked.Increment` on a static counter
  so sequences are always real values.

### Changed

- **Static `ChatMiddlewareHolder` replaced with DI-managed accessors**:
  removed a static singleton that held middleware state; middleware now
  resolves from DI per-invocation. Closes a test isolation issue where
  state leaked across tests.

- **Static `ProgressSequence` replaced with DI-managed
  `IProgressSequence`**: removed another static holder in favor of a
  singleton service.

- **`TypeRegistryGenerator` refactored**: extracted `Options` discovery
  into `OptionsDiscoveryHelper`, extracted `Options` emission into
  `OptionsCodeGenerator`, extracted injectable types + plugins emission
  into `CodeGen/`, split `DiscoveryModels.cs` into one-type-per-file,
  and split the final `TypeRegistryGenerator.cs` down to 842 lines.
  No user-visible behavior change; future generator work becomes
  easier to reason about.

- **`AgentFrameworkFunctionRegistryGenerator` split into 16 files**:
  same motivation as the `TypeRegistryGenerator` refactor above.

- **`IAgentDiagnosticsWriter` extracted from `AgentDiagnosticsAccessor`**:
  eliminates a concrete-type cast in the middleware registration path.

- **`AgentRunDiagnosticsBuilder` is now `IDisposable`**: ensures the
  diagnostics capture scope is torn down deterministically.

- **`docs/options.md` has a new "ASP.NET Core Web Application Path"
  section** documenting that `[Options]` classes bind on both console
  and web paths via `RegisterOptions` without manual `AddOptions`
  calls, with a runnable example matching `MinimalWebApiSourceGen`.

- **`ConfigurationChangeTokenSource` in `AgentFrameworkSyringe` is now
  registered under the correct name**: was previously `null!` for the
  options name.

### Removed

- **Dead `ProgressReportingExtensions.cs`**: file was orphaned after a
  refactor.

- **`GenerateTypeRegistry` skill** migrated to
  `ncosentino/agentic-plugins` — no runtime behavior change; this was
  developer tooling only.

### Shipped analyzers (0.0.2-alpha.26)

The following diagnostic rules have been moved from
`AnalyzerReleases.Unshipped.md` to `AnalyzerReleases.Shipped.md` under
the `## Release 0.0.2` section in each analyzer project. All are now
officially released — consumers on alpha.26+ can rely on their
stability.

**`NexusLabs.Needlr.Analyzers`** (2 rules):

- `NDLRCOR012` — Error — Disposable captive dependency
- `NDLRCOR016` — Warning — `[DoNotAutoRegister]` on plugin class is redundant

**`NexusLabs.Needlr.AgentFramework.Analyzers`** (14 rules, first-ever ship):

- `NDLRMAF001` — Error — `AgentHandoffsTo` target type is not decorated with `[NeedlrAiAgent]`
- `NDLRMAF002` — Error — `AgentGroupChatMember` group has fewer than two members
- `NDLRMAF003` — Warning — Class with `[AgentHandoffsTo]` is not itself decorated with `[NeedlrAiAgent]`
- `NDLRMAF004` — Warning — Cyclic handoff chain detected
- `NDLRMAF005` — Warning — `FunctionGroups` references a group name with no matching `[AgentFunctionGroup]` class
- `NDLRMAF006` — Error — Duplicate `Order` value in `[AgentSequenceMember]` pipeline
- `NDLRMAF007` — Warning — Gap in `[AgentSequenceMember]` `Order` sequence
- `NDLRMAF008` — Info — Agent participates in no topology declaration
- `NDLRMAF009` — Warning — `[WorkflowRunTerminationCondition]` declared on a non-agent class
- `NDLRMAF010` — Error — Termination condition type does not implement `IWorkflowTerminationCondition`
- `NDLRMAF011` — Info — Prefer `[AgentTerminationCondition]` over `[WorkflowRunTerminationCondition]` for group chat members
- `NDLRMAF012` — Warning — `[AgentFunction]` method is missing a `[Description]` attribute
- `NDLRMAF013` — Warning — `[AgentFunction]` method parameter is missing a `[Description]` attribute
- `NDLRMAF014` — Warning — `FunctionTypes` entry has no `[AgentFunction]` methods

**`NexusLabs.Needlr.Generators`** (11 rules):

- `NDLRGEN022` — Error — Disposable captive dependency detected
- `NDLRGEN031` — Error — `ProviderAttributeAnalyzer`: `[Provider]` on class requires partial modifier
- `NDLRGEN032` — Error — `ProviderAttributeAnalyzer`: `[Provider]` interface has invalid member
- `NDLRGEN033` — Warning — `ProviderAttributeAnalyzer`: Provider property uses concrete type
- `NDLRGEN034` — Error — `ProviderAttributeAnalyzer`: Circular provider dependency detected
- `NDLRHTTP001` — Error — `HttpClientOptionsAnalyzer`: `[HttpClientOptions]` target must implement `INamedHttpClientOptions`
- `NDLRHTTP002` — Error — `HttpClientOptionsAnalyzer`: HttpClient name sources conflict
- `NDLRHTTP003` — Error — `HttpClientOptionsAnalyzer`: `ClientName` property body is not a literal expression
- `NDLRHTTP004` — Error — `HttpClientOptionsAnalyzer`: Resolved HttpClient name is empty
- `NDLRHTTP005` — Error — `HttpClientOptionsAnalyzer`: Duplicate HttpClient name
- `NDLRHTTP006` — Error — `HttpClientOptionsAnalyzer`: `ClientName` property has wrong shape

## [0.0.2-alpha.25] - 2026-03-04

### Added

- **`NeedlrSourceGenBootstrap.RegisterPlugins()`**: New public API for cross-generator plugin registration. When a second source generator in the same assembly emits plugin types, `TypeRegistryGenerator` cannot see those types — it receives the original compilation snapshot before any other generator runs. `RegisterPlugins()` lets a generator emit a `[ModuleInitializer]` to contribute those types to the Needlr registry at runtime, before any application code executes. See [Cross-Generator Plugins](https://ncosentino.github.io/needlr/cross-generator-plugins/) for the full pattern.

- **Auto-suppress generation for Roslyn component and Aspire host projects**: `NeedlrAutoGenerate` is now automatically forced to `false` for projects where `IsRoslynComponent=true` or `IsAspireHost=true`. Previously, consumers had to add explicit opt-out conditions in `Directory.Build.props` for generator/analyzer projects and Aspire host projects. These conditions are now unnecessary — though leaving them in place is harmless.

### Fixed

- **NDLRGEN001 spuriously emitted for assemblies that have `[GenerateTypeRegistry]`**: The NDLRGEN001 diagnostic ("type registry attribute not found") was incorrectly reported for assemblies that are valid Needlr generation targets. It is now suppressed for all assemblies carrying `[assembly: GenerateTypeRegistry(…)]`.

### Changed

- **`MultiProjectApp` example: `AssemblyLoader` removed, generator delivered via `Directory.Build.targets`**: The `AssemblyLoader` helper class (which loaded plugin assemblies at runtime by file path) has been removed from the example. The Needlr generator is now delivered to all example projects automatically through a shared `Directory.Build.targets` file, eliminating the need for consumers to load assemblies manually.

- **`test-packages.ps1` discovers example projects dynamically**: The packaging validation script no longer has a hard-coded list of example `.csproj` files. It now discovers all packable project files under `src/Examples/` at runtime, so new examples are validated automatically without requiring a script update.

## [0.0.2-alpha.24] - 2026-07-02

### Added

- **`NexusLabs.Needlr.Build` auto-generates `[assembly: GenerateTypeRegistry]`**: When `NeedlrAutoGenerate=true`, the `NeedlrWriteTypeRegistryAttributeFile` MSBuild target now writes a `NeedlrGeneratedTypeRegistry.g.cs` file to the intermediate output directory automatically. This eliminates the MSBuild boilerplate consumers previously needed in their own `Directory.Build.targets` to write the attribute file.

- **`NeedlrNamespacePrefix` MSBuild property**: Controls the `IncludeNamespacePrefixes` argument on the generated `[assembly: GenerateTypeRegistry]` attribute. Accepts a semicolon-delimited list of namespace prefixes (e.g., `<NeedlrNamespacePrefix>MyApp.Features;MyApp.Core</NeedlrNamespacePrefix>`). When empty (the default), the no-arg form `[assembly: GenerateTypeRegistry()]` is emitted, meaning all types in the assembly are included.

- **`NeedlrAutoGenerateAttribute` escape hatch MSBuild property**: Set to `false` to suppress the auto-generated attribute file while keeping `NeedlrAutoGenerate=true`. Use this for projects that declare `[assembly: GenerateTypeRegistry]` manually, or for projects in a solution-wide opt-in setup that intentionally have no types to register.

### Changed

- **Example projects use `NeedlrNamespacePrefix` instead of manual attribute files**: All `src/Examples/` source-gen projects now dog-food the new feature. The `GeneratorAssemblyInfo.cs` / `AssemblyInfo.cs` files containing the assembly attribute have been removed; namespace scoping is declared via `<NeedlrNamespacePrefix>` in each project's `.csproj`.

### Breaking Change

- **CS0579 if you already declare `[assembly: GenerateTypeRegistry]` and have `NeedlrAutoGenerate=true`**: The new auto-generated attribute file and any existing manual declaration cannot coexist (`AllowMultiple = false`). If you have both, you will get a compile error. **Fix**: either delete your manual attribute declaration (let `NeedlrAutoGenerate` handle it), or add `<NeedlrAutoGenerateAttribute>false</NeedlrAutoGenerateAttribute>` to your project to suppress the new target.

## [0.0.2-alpha.23] - 2026-05-16

### Fixed

- **`NexusLabs.Needlr.Build` still did not deliver `NexusLabs.Needlr.Generators` to consuming projects (alpha.22 regression)**: The alpha.22 fix only removed `OutputItemType="Analyzer"` but left `ReferenceOutputAssembly="false"` on the `ProjectReference`. `ReferenceOutputAssembly="false"` alone causes NuGet restore to exclude the referenced project from `project.assets.json` entirely, so Pack (which reads the assets file via `GetProjectReferencesFromAssetsFileTask`) never saw Generators and never emitted it as a nuspec dependency. Fixed by removing `ReferenceOutputAssembly="false"` and replacing it with `ExcludeAssets="compile;runtime;native;contentFiles" PrivateAssets="none"` — this preserves the original intent (don't compile Build against the generator DLL) while correctly registering Generators as a public transitive NuGet dependency in the assets file and nuspec.

## [0.0.2-alpha.22] - 2026-03-02

### Fixed

- **`NexusLabs.Needlr.Build` did not deliver `NexusLabs.Needlr.Generators` to consuming projects**: The `ProjectReference` to `NexusLabs.Needlr.Generators` in `NexusLabs.Needlr.Build.csproj` used `OutputItemType="Analyzer"`, which causes NuGet Pack to omit the reference from the nuspec dependency list. As a result, projects that referenced only `NexusLabs.Needlr.Build` never received the generator DLL and no `TypeRegistry` was produced. Removed `OutputItemType="Analyzer"` from the reference; since `NexusLabs.Needlr.Generators` already places its DLL in `analyzers/dotnet/cs/` within its own package, NuGet now correctly delivers and wires it up as an analyzer for all consuming projects.

## [0.0.2-alpha.21] - 2026-03-02

### Added

- **`MultiProjectApp.Integration.Tests` example**: New test project in the MultiProjectApp example demonstrating source generation enabled inside a test project. Shows how `ITestNotificationCapture` (a test-only interface) and its implementation are discovered via a generated TypeRegistry, illustrating the pattern for test infrastructure that uses Needlr plugin discovery without interfering with production plugins.

### Fixed

- **`.snupkg` 400 error / NU5128 warning for `NexusLabs.Needlr.Build`**: Build-only packages that contain only `.props`/`.targets` files must not generate symbol packages (`.snupkg`). Added `<IncludeSymbols>false</IncludeSymbols>` and `<NoWarn>NU5128</NoWarn>` to `NexusLabs.Needlr.Build.csproj`.

### Changed

- **`MultiProjectApp` example simplified**: Removed the now-unnecessary `BootstrapPlugin` class. The Bootstrap project no longer needs a concrete `IServiceCollectionPlugin` implementation to anchor type discovery; the assembly reference chain is sufficient.

### Documentation

- Updated `MultiProjectApp/README.md`: added `Integration.Tests` to project structure, added "Keeping Source Gen Enabled in Test Projects" section, removed stale `BootstrapPlugin` content.
- Updated `docs/solution-wide-source-generation.md`: split opt-out/opt-in sections, added critical caveat that `OutputItemType="Analyzer"` project references are **not** transitively propagated, fixed example showing test projects with source gen kept enabled.

## [0.0.2-alpha.20] - 2026-03-01

### Added

- **`NexusLabs.Needlr.Build` MSBuild package**: New build-only package for solution-wide source generation setup. Add to `Directory.Build.props` with `<NeedlrAutoGenerate>true</NeedlrAutoGenerate>` to enable source generation across all projects; individual projects opt out with `<NeedlrAutoGenerate>false</NeedlrAutoGenerate>`.

- **NDLRCOR016 Analyzer**: New warning fires when `[DoNotAutoRegister]` is placed directly on a class that implements a plugin interface. The attribute is redundant there (the interface already carries it) and was the root cause of the silent plugin suppression bug (see Fixed below).

- **`MultiProjectApp` reference example**: New example in `src/Examples/MultiProjectApp/` demonstrating canonical multi-project solution setup — Bootstrap anchor project, feature plugin projects, multiple entry points, and test projects with generation opt-out.

### Fixed

- **`[DoNotAutoRegister]` on plugin class silently prevented plugin discovery**: Placing `[DoNotAutoRegister]` directly on a class implementing `IServiceCollectionPlugin` (or any plugin interface) caused the plugin to be silently excluded from both the source generator's TypeRegistry and the runtime `ReflectionPluginFactory`. The attribute is documented as "don't register as a DI service" — it should not affect plugin discovery. Both code paths have been corrected. The new NDLRCOR016 analyzer warns when this pattern is used.

- **Generator bundling in integration packages caused duplicate TypeRegistry compilation**: `NexusLabs.Needlr.AgentFramework`, `NexusLabs.Needlr.SemanticKernel`, and `NexusLabs.Needlr.SignalR` bundled `NexusLabs.Needlr.Generators.dll` in their NuGet `analyzers/` folder with `PrivateAssets="all"`. When a project also referenced `NexusLabs.Needlr.Generators` directly, Roslyn ran the generator twice, producing duplicate type registration code that failed to compile. The bundled DLL is removed; each package now declares the generator as a proper transitive NuGet dependency. A `DeduplicateNeedlrGeneratorAnalyzers` MSBuild target is included in each package as defense-in-depth.

## [0.0.2-alpha.19] - 2026-03-01

### Added

- **Microsoft Agent Framework (MAF) Integration**: First-class support for building multi-agent orchestrations with `NexusLabs.Needlr.AgentFramework`
  - `[NeedlrAiAgent]` attribute for named agent declarations with source-generated bootstrap
  - `IAgentFactory.CreateAgent<T>()` / `CreateAgent(string name)` for DI-wired agent construction
  - `IWorkflowFactory` with `CreateHandoffWorkflow<T>()`, `CreateSequentialWorkflow(name)`, `CreateGroupChatWorkflow(name)`
  - `UsingAgentFramework()` / `UsingChatClient()` Syringe extensions for fluent MAF wiring

- **MAF Topology Declarations**: Compile-time workflow structure via attributes
  - `[AgentHandoffsTo(typeof(T))]` — declares agent handoff edges
  - `[AgentSequenceMember(pipeline, order)]` — declares sequential pipeline membership
  - `[AgentGroupChatMember(groupName)]` — declares group chat membership
  - `[WorkflowRunTerminationCondition]` — marks methods as early-exit predicates for pipeline workflows
  - `[AgentFunctionGroup]` — scopes AI functions to a named group for multi-agent isolation

- **MAF Workflow Execution Helpers** (`NexusLabs.Needlr.AgentFramework.Workflows`)
  - `StreamingRunWorkflowExtensions.RunAsync()` — executes a workflow and collects all agent responses as `IReadOnlyDictionary<string, string>`
  - Source-generated `WorkflowFactoryExtensions` with typed `Create*Workflow()` and `Run*WorkflowAsync()` methods per declared workflow

- **MAF Source Generator** (`NexusLabs.Needlr.AgentFramework.Generators`)
  - `AgentFrameworkBootstrapGenerator`: emits `AgentFrameworkGeneratedBootstrap` with handoff, sequential, and group-chat topology tables registered via `[ModuleInitializer]`
  - `AgentFrameworkFunctionRegistryGenerator`: emits `GeneratedAIFunctionProvider` — AOT-safe, source-generated `IAIFunctionProvider` using `IServiceProvider.GetRequiredService<T>()` for instance creation
  - `WorkflowExtensionsGenerator`: emits typed `WorkflowFactoryExtensions` per workflow with full XML doc comments per agent
  - `AgentTopologyExportGenerator`: emits `AgentTopologyGraph.md` when `NeedlrDiagnostics=true`

- **MAF Analyzers** (12 new diagnostics)
  - `NDLRMAF001`: Missing `[AgentHandoffsTo]` target registration
  - `NDLRMAF002`: Circular handoff chain detected
  - `NDLRMAF003`: Unreachable agent (no inbound handoff edges)
  - `NDLRMAF004`: `[AgentSequenceMember]` order gap or duplicate
  - `NDLRMAF005`: `[AgentFunctionGroup]` declared on non-injectable type
  - `NDLRMAF006`: Agent function group not registered in any agent
  - `NDLRMAF007`: `[NeedlrAiAgent]` missing required `IChatClient` dependency
  - `NDLRMAF008`: Duplicate agent name
  - `NDLRMAF009`: `[WorkflowRunTerminationCondition]` on wrong return type
  - `NDLRMAF010`: `[WorkflowRunTerminationCondition]` on non-sequential-member class
  - `NDLRMAF011`: Multiple termination conditions on same sequential pipeline
  - `NDLRMAF012`–`NDLRMAF014`: Function provider, group membership, and agent registration cross-checks
  - Code fix providers for `NDLRMAF001` and `NDLRMAF003` (add missing attribute scaffolding)

- **AOT-Safe MAF** (`AotAgentFrameworkApp` example)
  - NativeAOT proof with `IlcDisableReflection=true`, full `IL2026`/`IL3050` `WarningsAsErrors`, `TrimMode=full`
  - `[DoNotAutoRegister]` on reflection-based scanners prevents ILC tracing their `[RequiresDynamicCode]` constructors
  - `[UnconditionalSuppressMessage]` on `AgentFactory.BuildFunctionsForType` and `WorkflowFactory.Resolve*` fallback callers — reflection branches are unreachable when the source-gen bootstrap is registered
  - Demonstrates handoff workflow, sequential pipeline, and termination condition under NativeAOT
  - CI job `aot-agent-app` publishes for `linux-x64` in parallel with existing AOT jobs

- **Plugin assembly example**: `[NeedlrAiAgent]` source gen across assembly boundaries via separate `.Agents` library pattern

- **SemanticKernel / SignalR XML documentation enrichment**: All public types have comprehensive XML doc comments

### Changed

- **`IAIFunctionProvider.TryGetFunctions` signature**: Parameter changed from `object? instance` to `IServiceProvider serviceProvider`
  - Generated provider now calls `serviceProvider.GetRequiredService<T>()` (AOT-safe) instead of casting the passed instance
  - `AgentFactory.BuildFunctionsForType` checks the generated provider *before* calling `ActivatorUtilities.CreateInstance`

### Fixed

- MAF diagnostics now output to entry-point exe bin directory (not the `.Agents` library bin)
- `NeedlrExportGraph` defaults to `false` to match props file expectation

### Documentation

- AI integrations page covering the two-layer discovery model
- MAF feature page with topology declarations, workflow execution, and source generation guide
- NDLRMAF001–014 analyzer reference pages
- `llms.txt` for AI tool accessibility
- Open Graph / Twitter Card meta tags, BreadcrumbList and SoftwareSourceCode structured data
- Per-page descriptions and cross-references to published articles

_(366 files changed, +39873/-3108 lines)_

## [0.0.2-alpha.18] - 2026-01-30

### Added
- **AOT-Compatible Options - Full Parity**: Complete feature parity with non-AOT configuration binding
  - Primitive types, nullable primitives, and enums using `Enum.TryParse<T>()`
  - Init-only properties via factory pattern with object initializers
  - Positional records via factory pattern with constructor invocation
  - Nested objects with recursive property binding
  - Arrays, Lists, and Dictionaries (`Dictionary<string, T>`)
  - Circular reference protection with visited type tracking
  - Named options with `[Options("Section", Name = "Named")]` pattern

- **DataAnnotations Source Generation**: Validation attributes source-generated as `IValidateOptions<T>` implementations
  - Supported: `[Required]`, `[Range]`, `[StringLength]`, `[MinLength]`, `[MaxLength]`, `[RegularExpression]`, `[EmailAddress]`, `[Phone]`, `[Url]`
  - Non-AOT keeps `.ValidateDataAnnotations()` fallback for unsupported attributes

- **NDLRGEN030: Unsupported DataAnnotation Warning**: Analyzer warns when [Options] classes use DataAnnotation attributes that cannot be source-generated
  - Warns about: `[CustomValidation]`, `[CreditCard]`, `[FileExtensions]`, and custom `ValidationAttribute` subclasses
  - Guides users to use custom `Validate()` method instead

- **[RegisterAs<T>] Attribute**: Explicit interface registration for DI
  - Apply `[RegisterAs<IService>]` to control which interface a class is registered as
  - NDLRCOR015 analyzer validates the type argument is implemented by the class

- **FluentValidation Integration**: New `NexusLabs.Needlr.FluentValidation` package with source generator extension
  - Auto-discovers and registers FluentValidation validators for `[Options]` classes
  - Extension framework for custom validator integrations

- **Enhanced Diagnostic Output**
  - `OptionsSummary.md` shows all discovered options with their configuration
  - Consolidated Factory Services section in diagnostics

- **Enhanced Dependency Graph Visualization**: The `DependencyGraph.md` diagnostic output now includes additional sections
  - Decorator Chains, Keyed Service Clusters, Plugin Assembly Boundaries
  - Factory Services, Interface Mapping, Complexity Metrics

- **Open Generic Decorators**: New `[OpenDecoratorFor(typeof(IInterface<>))]` attribute for source-gen only
  - Automatically decorates all closed implementations of an open generic interface
  - Compile-time validation with NDLRGEN006, NDLRGEN007, NDLRGEN008 analyzers

- **Positional Record Support for [Options]**: Positional records now work with `[Options]` when declared as `partial`
  - The generator emits a parameterless constructor that chains to the primary constructor
  - NDLRGEN021 warning emitted for non-partial positional records

### Changed
- **BREAKING: `[GenerateFactory]` namespace change**: Moved from `NexusLabs.Needlr` to `NexusLabs.Needlr.Generators`
  - Update imports: `using NexusLabs.Needlr;` → `using NexusLabs.Needlr.Generators;`

- **BREAKING: Factory analyzer diagnostic IDs renamed**: Factory-related analyzers moved to generators project with new IDs
  - `NDLRCOR012` → `NDLRGEN003`, `NDLRCOR013` → `NDLRGEN004`, `NDLRCOR014` → `NDLRGEN005`

- **BREAKING: ConfiguredSyringe API**: `Syringe` no longer has `BuildServiceProvider()`. You must call a strategy method first:
  - `new Syringe().UsingReflection()` / `.UsingSourceGen()` / `.UsingAutoConfiguration()` → returns `ConfiguredSyringe`
  - This prevents runtime crashes from misconfigured syringes by making incorrect usage a compile-time error

- **Refactored TypeRegistryGenerator**: Decomposed into smaller focused generators
  - `OptionsCodeGenerator`, `FactoryCodeGenerator`, `InterceptorCodeGenerator`
  - `DiagnosticsGenerator`, `SignalRCodeGenerator`, `SemanticKernelCodeGenerator`

- **SignalR and SemanticKernel code generation moved to dedicated packages**

### Fixed
- `GetShortTypeName` correctly handles generic types
- Attribute detection in DataAnnotations uses namespace/typeName matching (more reliable than ToDisplayString)

_(176 files changed, +23041/-2816 lines)_

## [0.0.2-alpha.16] - 2026-01-24

### Added
- **Source-gen assembly ordering**: `OrderAssemblies(order => order.By().ThenBy())` now works for source-gen path
  - Same fluent API as reflection path for full parity
  - `SyringeSourceGenExtensions.OrderAssemblies()` extension method
  - Assemblies sorted by tiered predicates using `AssemblyOrderBuilder`
- **6 parity tests**: Verifying reflection and source-gen assembly ordering produce identical behavior

### Fixed
- **TypeRegistry generator not detecting `[GenerateTypeRegistry]` attribute**: `ForAttributeWithMetadataName` doesn't work for assembly-level attributes. Changed to use `compilation.Assembly.GetAttributes()` directly, which correctly detects `[assembly: GenerateTypeRegistry(...)]` attributes.
- **`AssemblyOrderBuilderTests`**: Moved from incorrectly named "parity" tests to unit test location

## [0.0.2-alpha.15] - 2026-01-23

### Fixed
- **Plugin discovery**: Records with parameterless constructors ARE now discoverable via `IPluginFactory.CreatePluginsFromAssemblies<T>()` (fixes CacheConfiguration pattern)
- **Auto-registration**: Records are still correctly excluded from `IsInjectableType` (not auto-registered into DI container)

## [0.0.2-alpha.14] - 2026-01-23

### Added
- **`[DecoratorFor<TService>]` attribute**: Automatic decorator wiring without manual plugin registration
  - Apply to a class to automatically configure it as a decorator for the specified service
  - `Order` property controls decoration sequence (lower = closer to original service)
  - Works with both source generation and reflection
  - Multiple decorators per service supported with ordering
  - A class can decorate multiple services using multiple attributes
- **Non-generic `AddDecorator(Type, Type)` overload**: For runtime decorator application

### Fixed
- **Plugin discovery**: Records with parameterless constructors ARE now discoverable via `IPluginFactory.CreatePluginsFromAssemblies<T>()` (fixes CacheConfiguration pattern)
- **Auto-registration**: Records are still correctly excluded from `IsInjectableType` (not auto-registered into DI container)
- **Source generation**: Types with `required` members are excluded from automatic registration (cannot be instantiated by DI container without setter access)
- **Source generation**: Types with `[SetsRequiredMembers]` constructor are still included (constructor satisfies all required members)

### Changed
- **CI/CD**: Added GitHub-native code coverage reporting with badges and GitHub Pages reports

## [0.0.2-alpha.13] - 2026-01-23

### Added
- **Expression-based Assembly Ordering API**: New unified API for ordering assemblies that works for both reflection and source-gen
  - `OrderAssemblies(order => order.By(...).ThenBy(...))` fluent builder
  - `AssemblyOrderBuilder` with tiered matching - assemblies go in first matching tier, unmatched always last
  - `AssemblyInfo` abstraction for expressions to work with both runtime assemblies and compile-time strings
  - Presets: `AssemblyOrder.LibTestEntry()`, `AssemblyOrder.TestsLast()`, `AssemblyOrder.Alphabetical()`
- **`UseLibTestEntryOrdering()` extension method**: Replaces `UseLibTestEntrySorting()` with consistent naming

### Changed
- **BREAKING**: `UseSorter()` removed from `IAssemblyProviderBuilder` - use `OrderAssemblies()` instead
- **BREAKING**: `UseAlphabeticalSorting()` removed - use `OrderAssemblies(order => order.By(a => true))` or `AssemblyOrder.Alphabetical()`
- **BREAKING**: `UseLibTestEntrySorting()` renamed to `UseLibTestEntryOrdering()`

### Removed
- **BREAKING**: `NeedlrAssemblyOrderAttribute` removed - assembly ordering is now configured at Syringe level
  - Was: `[assembly: NeedlrAssemblyOrder(First = new[] { "..." })]`
  - Now: `.OrderAssemblies(order => order.By(a => a.Name.StartsWith("...")))`
- **BREAKING**: `IAssemblySorter` interface and implementations removed
  - `AlphabeticalAssemblySorter`, `LibTestEntryAssemblySorter`, `ReflectionAssemblySorter` all removed

### Fixed
- **`UsingOnlyAsTransient<T>()` not working with source-gen**: `GeneratedTypeRegistrar` now respects lifetime overrides from `ITypeFilterer`
  - Added `GetEffectiveLifetime()` to `ITypeFilterer` interface
  - Source-gen registrar now checks filterer before using pre-computed lifetime

## [0.0.2-alpha.12] - 2026-01-23

### Fixed
- **Base class plugin discovery**: Source generator now discovers plugins that inherit from abstract base classes (not just interfaces)
  - `GetPluginInterfaces()` now walks the base class hierarchy in addition to interfaces
  - Fixes issue where `CacheConfiguration` and similar abstract record-based plugins were not discovered

## [0.0.2-alpha.11] - 2026-01-22

### Fixed  
- **`Except<T>()` not working with source-gen**: `GeneratedTypeRegistrar` now checks `IsTypeExcluded()` on the type filterer
  - Added `IsTypeExcluded()` method to `ITypeFilterer` interface
  - All registrars (Generated, Reflection, Scrutor) now respect type exclusions
  - Fixes duplicate route registration when using `Except<ICarterModule>()`

## [0.0.2-alpha.10] - 2026-01-22

### Added
- **`IPluginFactory` in plugin options**: All plugin option types now include access to `IPluginFactory`
  - `WebApplicationBuilderPluginOptions.PluginFactory`
  - `ServiceCollectionPluginOptions.PluginFactory`
  - Enables plugins to discover and instantiate other plugins at configuration time

## [0.0.2-alpha.9] - 2026-01-22

### Added
- **Duplicate Plugin Prevention**: Framework-level deduplication ensures each plugin type executes only once, even if discovered multiple times
  - Applies to all plugin types: `IWebApplicationPlugin`, `IWebApplicationBuilderPlugin`, `IServiceCollectionPlugin`, `IPostBuildServiceCollectionPlugin`, and `IHostApplicationBuilderPlugin`
  - Prevents duplicate route registration, middleware configuration, and service registration
  - Tests added to verify deduplication behavior

### Fixed
- **Carter routes registered twice**: Removed Carter-specific idempotency guards since framework now handles deduplication
  - `CarterWebApplicationBuilderPlugin` and `CarterWebApplicationPlugin` simplified back to single responsibility

## [0.0.2-alpha.8] - 2026-01-22

### Added
- **Carter end-to-end HTTP tests**: Added `CarterEndToEndTests` for full HTTP request/response testing of Carter modules

## [0.0.2-alpha.7] - 2026-01-22

### Added
- **Global Namespace Support**: Empty string `""` in `IncludeNamespacePrefixes` now explicitly includes types in the global namespace
  - Allows source generation to discover types without a `namespace` declaration
  - Example: `[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "MyCompany", "" })]`
- **NDLRCOR004 Analyzer**: Warns when injectable types in the global namespace may not be discovered
  - Detects types in global namespace that implement interfaces, have DI attributes, or have dependency constructors
  - Reports warning with guidance to either add a namespace or include `""` in `IncludeNamespacePrefixes`
  - Automatically suppressed when type has `[DoNotInject]` or `[DoNotAutoRegister]`

### Fixed
- **Needlr.Carter and Needlr.SignalR plugins not discovered**: Added `[GenerateTypeRegistry]` to Carter and SignalR packages
  - `CarterWebApplicationBuilderPlugin` and `CarterWebApplicationPlugin` now register automatically
  - `SignalRWebApplicationBuilderPlugin` now registers automatically
  - These packages now have their own TypeRegistry and module initializer for source generation compatibility

## [0.0.2-alpha.6] - 2026-01-21

### Added
- **Automatic Assembly Force-Loading**: Source generator now automatically discovers all referenced assemblies with `[GenerateTypeRegistry]` and emits `typeof()` calls to ensure they are loaded at startup
  - Solves the issue where transitive dependencies with plugins were not discovered because their assemblies never loaded
  - Generated `ForceLoadReferencedAssemblies()` method uses `typeof(AssemblyName.Generated.TypeRegistry).Assembly` for AOT-safe assembly loading
  - Assemblies are loaded in alphabetical order by default
- **`[NeedlrAssemblyOrder]` Attribute**: Optional attribute to control the order assemblies are loaded during startup
  - `First` property: Array of assembly names to load first (in order)
  - `Last` property: Array of assembly names to load last (in order)
  - All other assemblies load alphabetically between First and Last
  - Useful when plugins have dependencies on other plugins being registered first
- **NDLRCOR003 Analyzer**: Detects when `[DeferToContainer]` attribute is placed in generated code (which won't work due to source generator isolation)
  - Reports error with guidance to move the attribute to original hand-written source file

### Fixed
- **Plugin Discovery Bug**: Fixed issue where plugins in transitive dependencies were not discovered when using source generation
  - Root cause: Module initializers only run when their assembly is loaded by the CLR
  - If no code directly references types from an assembly, it never loads
  - Solution: Auto-discovery and force-loading ensures all Needlr-enabled assemblies load

## [0.0.2-alpha.5] - 2026-01-21

### Added
- **Generic Host Support**: New `NexusLabs.Needlr.Hosting` package for non-web .NET applications
  - `IHostApplicationBuilderPlugin` for configuring `HostApplicationBuilder`
  - `IHostPlugin` for configuring `IHost` after build
  - `ForHost()` and `ForHostApplicationBuilder()` extension methods on Syringe
  - Works with both source generation and reflection strategies
- **`[DeferToContainer]` Attribute**: Enables source generation for partial classes that receive constructors from other source generators
  - Declare expected constructor parameter types that another generator will add
  - Needlr generates correct factory code based on the declaration
  - Compile-time validation ensures declared types match actual generated constructor
- **Hosting Example Projects**: 
  - `WorkerServiceExample` (reflection) - Background worker service
  - `HostBuilderIntegrationExample` (reflection) - Console app with hosted services
  - `WorkerServiceSourceGen` (source generation) - AOT-compatible worker service
  - `HostBuilderIntegrationSourceGen` (source generation) - AOT-compatible console app
- **Comprehensive XML Documentation**: All public types and methods now have XML doc comments
- **Open Generic Type Tests**: Test coverage for generic type handling parity between reflection and source generation

### Fixed
- **Open Generic Types Bug**: Source generator now correctly excludes open generic type definitions (e.g., `JobScheduler<TJob>`) matching reflection behavior
  - Previously generated invalid `typeof(JobScheduler<TJob>)` syntax
  - Now correctly excluded since open generics cannot be instantiated
  - `GetFullyQualifiedName` outputs valid open generic syntax (`MyClass<>`) if ever needed elsewhere

### Changed
- Example projects reorganized: `Hosting/Reflection/` and `Hosting/SourceGen/` subfolders
- Documentation updated with "Working with Other Source Generators" section

## [0.0.2-alpha.1] - 2026-01-19

### ⚠️ Breaking Changes
- Removed `IAssembyProviderBuilder` interface

### Added
- Source-generation type providers (`GeneratedTypeProvider`, `GeneratedPluginProvider`)
- Module initializer bootstrap for zero-reflection startup
- Source-generation plugin factory (`GeneratedPluginFactory`)
- Reflection-based type providers (`ReflectionTypeProvider`, `ReflectionPluginProvider`)
- Bundle package with auto-configuration (source-gen first, reflection fallback)
- Roslyn analyzers for common Needlr mistakes
- Source-generation support for SignalR hub registration
- Source-generation support for Semantic Kernel plugin discovery
- AOT/Trimming example applications (console and web)
- Bundle auto-configuration example
- Performance benchmarks comparing source-gen vs reflection
- Parallel CI/CD with AOT publish validation
- Changelog generator agent skill

### Changed
- `AssembyProviderBuilder` renamed to `AssemblyProviderBuilder`
- `IAssembyProviderBuilderExtensions` renamed to `IAssemblyProviderBuilderExtensions`
- `PluginFactory` renamed to `ReflectionPluginFactory`
- `DefaultTypeRegistrar` renamed to `ReflectionTypeRegistrar`
- `PluginFactoryTests` renamed to `ReflectionPluginFactoryTests`
- Source generation is now the default pattern (reflection is opt-in)
- SignalR now accepts `IPluginFactory` via dependency injection (no hardcoded reflection)
- Semantic Kernel now accepts `IPluginFactory` via dependency injection
- ASP.NET package decoupled from Bundle (explicit strategy choice required)
- Simplified Syringe API with provider-based architecture
- `UsingScrutorTypeRegistrar()` renamed to `UsingScrutor()`

### Removed
- `DefaultAssemblyLoader` (assembly loading now handled by providers)
- `DefaultAssemblySorter` (assembly sorting no longer needed)
- `SyringeExtensions` (consolidated or moved)

_(249 files changed, +13355/-545 lines)_

## [0.0.1-alpha.19] - 2026-01-18

Initial alpha releases with reflection-based architecture.
