# Evaluation

Needlr's agent framework plugs directly into [`Microsoft.Extensions.AI.Evaluation`](https://learn.microsoft.com/en-us/dotnet/ai/evaluation/libraries) without adapters or flattening.

## Overview

`Microsoft.Extensions.AI.Evaluation` (MEAI.Evaluation) evaluates LLM interactions by consuming native MEAI primitives: `ChatMessage`, `ChatResponse`, and `UsageDetails`. Needlr exposes these same shapes on its result surfaces, so evaluators slot in directly — no string flattening, no re-hydration step.

## Live-path result types

The surfaces an agent run returns expose full MEAI types rather than flattened strings, so the output of a run is feedable to an evaluator as-is.

| Surface | Type |
|---|---|
| `IterativeLoopResult.FinalResponse` | `ChatResponse?` |
| `IterationRecord.FinalResponse` | `ChatResponse?` |
| `TerminationContext.LastMessage` | `ChatMessage?` |
| `TerminationContext.Usage` | `UsageDetails?` |
| `IAgentStageResult.FinalResponse` | `ChatResponse?` |
| `AgentStageResult` | positional `ChatResponse?` |
| `IPipelineRunResult.FinalResponses` | `IReadOnlyDictionary<string, ChatResponse?>` |

Use `response.Text` when a string projection is needed.

## Wiring an evaluator

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;

// loopResult is an IterativeLoopResult returned by IterativeAgentLoop.
ChatResponse? finalResponse = loopResult.FinalResponse;

var chatConfiguration = new ChatConfiguration(judgeChatClient);
var evaluator = new RelevanceEvaluator();
var userPrompt = new ChatMessage(ChatRole.User, "Original user question here.");

EvaluationResult result = await evaluator.EvaluateAsync(
    [userPrompt],
    finalResponse!,
    chatConfiguration);
```

## Trajectory adapter

Tool-call trajectories are extracted from `IterationRecord.ToolCalls` via an extension method:

```csharp
using NexusLabs.Needlr.AgentFramework.Iterative;

IEnumerable<AIContent> trajectory = iterationRecord.ToToolCallTrajectory();
```

The returned sequence alternates `FunctionCallContent` (the call) and `FunctionResultContent` (the result) in call order — the exact shape MEAI.Evaluation tool-call evaluators consume.

## Example

See [`src/Examples/AgentFramework/EvaluationExampleApp`](https://github.com/ncosentino/needlr/tree/main/src/Examples/AgentFramework/EvaluationExampleApp) for a self-contained example. It builds a sample `IterativeLoopResult`, feeds it to `RelevanceEvaluator`, and prints the trajectory shape — no API keys required (uses a local mock judge `IChatClient`).

Run it:

```bash
dotnet run --project src/Examples/AgentFramework/EvaluationExampleApp
```

## Post-hoc replay from diagnostics

A serialized `AgentRunDiagnostics` is sufficient for offline replay and evaluation — no need to re-invoke the agent or the underlying model.

| Diagnostic | Property | Contents |
|---|---|---|
| `ChatCompletionDiagnostics` | `RequestMessages : IReadOnlyList<ChatMessage>?` | The exact messages sent to the chat client on that call. |
| `ChatCompletionDiagnostics` | `Response : ChatResponse?` | The full response returned by the chat client (null on failure). |
| `ToolCallDiagnostics` | `Arguments : IReadOnlyDictionary<string, object?>?` | Snapshot of the arguments the tool was invoked with. |
| `ToolCallDiagnostics` | `Result : object?` | The value returned by the tool invocation (null on failure). |

All four properties are init-only, default to `null`, and are populated automatically by `DiagnosticsChatClientMiddleware` and `DiagnosticsFunctionCallingMiddleware`. Capture is always on.

With these in hand you can rehydrate a MEAI `ChatResponse` + trajectory from a persisted diagnostics document and feed it into any `IEvaluator` offline.

## Full-fidelity transcripts

Evaluation and agent-assisted debugging both depend on **replay-grade** transcripts — every chat exchange, not just totals.

### Streaming capture

`DiagnosticsChatClientMiddleware` instruments both paths:

- `GetResponseAsync` — captured on completion.
- `GetStreamingResponseAsync` — streaming updates are teed through to the caller in real time, then buffered via `ToChatResponse()` at stream completion. The synthesized `ChatResponse` is written to `ChatCompletionDiagnostics.Response` with identical shape to the non-streaming path.

Errors mid-stream still populate `ChatCompletionDiagnostics.{Success=false, ErrorMessage, Response}` with the partial response built from updates observed before the failure. No data is silently dropped.

### Streaming agent runs

`DiagnosticsAgentRunMiddleware` instruments both agent-run paths:

- `HandleAsync` — captured on completion.
- `HandleStreamingAsync` — `AgentResponseUpdate`s are teed through to the caller in real time while distinct non-null `MessageId`s accumulate into `AgentRunDiagnostics.TotalOutputMessages`. On stream completion the builder is finalized and written to the configured `IAgentDiagnosticsWriter` with identical shape to the non-streaming path.

Mid-stream failures record the partial output-message count observed so far and call `AgentRunDiagnosticsBuilder.RecordFailure(...)` before rethrowing, so streaming agent runs surface in diagnostics the same way non-streaming runs do.

### Character counts

Tokens are an LLM-reported abstraction; character counts are a direct measure of the payload Needlr actually shipped and received. Both are captured on every completion.

- **`ChatCompletionDiagnostics.RequestCharCount`** — sum of `TextContent.Text?.Length` across all `RequestMessages`.
- **`ChatCompletionDiagnostics.ResponseCharCount`** — sum of text length across the aggregated `Response`.
- **`ToolCallDiagnostics.ArgumentsCharCount`** — length of the `System.Text.Json` serialization of the captured `Arguments` dictionary.
- **`ToolCallDiagnostics.ResultCharCount`** — length of the `System.Text.Json` serialization of the captured `Result`.

Populated automatically by `DiagnosticsChatClientMiddleware` and `DiagnosticsFunctionCallingMiddleware` on both success and failure paths. `DiagnosticsCharCounter` (in `NexusLabs.Needlr.AgentFramework.Diagnostics`) exposes the same helpers for callers who want to compute counts outside the middlewares. All helpers are null-safe and exception-tolerant — a counter failure never destabilizes the live path; it just yields `0`.

### Ordered timeline

`IAgentRunDiagnostics` exposes `ChatCompletions` and `ToolCalls` as separate collections, each with its own `Sequence`. When you need to see what actually happened in execution order, call the `GetOrderedTimeline()` extension method:

```csharp
using NexusLabs.Needlr.AgentFramework.Diagnostics;

var timeline = diag.GetOrderedTimeline();
foreach (var entry in timeline)
{
    Console.WriteLine($"[{entry.StartedAt:HH:mm:ss.fff}] {entry.Kind} #{entry.Sequence}");
}
```

The returned list merges both collections and sorts them by `StartedAt` (wall-clock). When two entries share the same `StartedAt`, `ChatCompletion` entries sort before `ToolCall` entries (a chat completion is what triggers a tool call, not the reverse); further ties resolve by `Sequence` within kind. Each `DiagnosticsTimelineEntry` carries the original `ChatCompletionDiagnostics` or `ToolCallDiagnostics` reference in the property matching its `Kind`, so no information is lost in the merge — the ordered view is purely additive.

### Agent-run boundary capture

Beyond the per-completion and per-tool-call records, `IAgentRunDiagnostics` captures the exact input and output at the run boundary:

- **`InputMessages : IReadOnlyList<ChatMessage>`** — the full input list handed to the middleware at run start. Empty when no input was supplied.
- **`OutputResponse : AgentResponse?`** — the full response assembled at run completion. For non-streaming runs this is the underlying `AgentResponse`; for streaming runs the middleware aggregates `AgentResponseUpdate` fragments by `MessageId` (updates without an id become discrete messages keyed by arrival ordinal).

Partial responses are still captured when a streaming run fails mid-stream — `OutputResponse` carries whatever messages were assembled before the fault, alongside `Succeeded = false` and the `ErrorMessage`. This makes a serialized `AgentRunDiagnostics` replay-complete: an evaluator can consume `InputMessages` + `OutputResponse` directly, without reaching back to the caller for the original prompt or the streamed output.

## Native agent-run evaluators

`NexusLabs.Needlr.AgentFramework.Evaluation` ships three deterministic evaluators that operate directly on `IAgentRunDiagnostics`. They are pure computations over captured diagnostics — no LLM judge is invoked, so they run offline and are cheap enough to assert in unit tests.

All three evaluators consume the same bridge type:

### AgentRunDiagnosticsContext

`AgentRunDiagnosticsContext : EvaluationContext` wraps an `IAgentRunDiagnostics` instance and exposes it through an `EvaluationContext` so native evaluators (and MEAI-provided evaluators that accept supplemental context) can read diagnostics without a custom adapter.

- **`ContextName = "Needlr Agent Run Diagnostics"`** — stable constant used as the context identifier.
- **`Diagnostics`** — the wrapped `IAgentRunDiagnostics`.
- `BuildContents()` emits a single `TextContent` summary of the run (agent name, execution mode, outcome, chat-completion count, tool-call count, duration) so MEAI judge-based evaluators that round-trip context through a prompt still see a readable summary.

Evaluators downcast the context to `AgentRunDiagnosticsContext` to reach the full `Diagnostics` surface.

### ToolCallTrajectoryEvaluator

Reports on the sequence of tool calls across a run.

- **`Tool Calls Total`** — total tool-call records observed.
- **`Tool Calls Failed`** — count of records where `Succeeded` is false.
- **`Tool Call Sequence Gaps`** — number of positions where consecutive tool-call `SequenceNumber` values are not strictly increasing by one.
- **`All Tool Calls Succeeded`** — boolean rollup, true when every tool call succeeded.

When no `AgentRunDiagnosticsContext` is present in the evaluation's additional context, the evaluator returns an empty result ("not applicable"). This lets callers include it unconditionally in a pipeline of evaluators.

### IterationCoherenceEvaluator

Reports on iterative-loop structure.

- **`Iteration Count`** — number of `IterationRecord` entries.
- **`Iteration Empty Outputs`** — count of iterations whose `FinalResponse` has no text content.
- **`Terminated Coherently`** — boolean, true when the run reports a terminated-coherently signal consistent with the captured iterations.

Gated on execution mode: the evaluator only emits metrics when `Diagnostics.ExecutionMode == "IterativeLoop"` (available as the `IterativeLoopExecutionMode` const). Other execution modes produce an empty result.

### TerminationAppropriatenessEvaluator

Reports on whether the run's terminal state is internally consistent.

- **`Run Succeeded`** — boolean mirror of `Diagnostics.Succeeded`.
- **`Termination Consistent`** — boolean, true when `Succeeded` agrees with the presence/absence of `ErrorMessage` (success ⇔ no error message).
- **`Execution Mode`** — string metric carrying the run's execution mode, or the `UnknownExecutionMode = "Unknown"` fallback when null.

### Wiring native evaluators

```csharp
var context = new AgentRunDiagnosticsContext(diagnostics);
var additionalContext = new[] { context };

// ChatConfiguration is required by the MEAI evaluator contract even for
// deterministic evaluators. Pass a judge when you also run judge-based
// evaluators; deterministic evaluators ignore it.
var chatConfiguration = new ChatConfiguration(judgeChatClient);

var trajectory = await new ToolCallTrajectoryEvaluator()
    .EvaluateAsync(
        messages: Array.Empty<ChatMessage>(),
        modelResponse: new ChatResponse(),
        chatConfiguration: null,
        additionalContext: additionalContext);

var coherence = await new IterationCoherenceEvaluator()
    .EvaluateAsync(
        messages: Array.Empty<ChatMessage>(),
        modelResponse: new ChatResponse(),
        chatConfiguration: null,
        additionalContext: additionalContext);

var termination = await new TerminationAppropriatenessEvaluator()
    .EvaluateAsync(
        messages: Array.Empty<ChatMessage>(),
        modelResponse: new ChatResponse(),
        chatConfiguration: null,
        additionalContext: additionalContext);
```

Each `EvaluationResult` exposes metrics by name — use the `*MetricName` constants on each evaluator type to look them up without string literals at the call site.

## Testing with Copilot as the judge

`NexusLabs.Needlr.AgentFramework.Evaluation.Testing` provides a reusable xUnit fixture and skip attribute for judge-based evaluator tests.

- **`NeedlrEvaluationFixture`** — discovers a judge chat client from the environment (Copilot CLI by default) and exposes it as `Judge : IChatClient?`. When no judge provider is configured the property is `null` and judge-based tests are skipped.
- **`RequiresEvaluationJudgeFactAttribute`** / **`RequiresEvaluationJudgeTheoryAttribute`** — xUnit attributes that skip the test at discovery time when no judge is available, so unconfigured machines see explicit skips rather than hard failures.

Azure OpenAI is deliberately excluded from automatic discovery. Tests that hit a live model run only through Copilot CLI, which is rate-limited by the user's subscription rather than a metered API.

### Transcript markdown

For snapshot tests, review artifacts, and CI log attachments, render an entire agent run as deterministic Markdown with `ToTranscriptMarkdown()`:

```csharp
using NexusLabs.Needlr.AgentFramework.Diagnostics;

string transcript = diag.ToTranscriptMarkdown();
File.WriteAllText("run.md", transcript);
```

The output is byte-stable across locales — it uses `CultureInfo.InvariantCulture` for numeric formatting and `System.Text.Json` with `WriteIndented = true` for embedded tool arguments and results. Structure:

- H1 header — agent name, execution mode, success/failure, total duration (ms), aggregate token usage.
- `## Input messages` — only emitted when `InputMessages` is non-empty.
- `## Timeline` — the ordered view from `GetOrderedTimeline()`, with each entry prefixed by its offset from `StartedAt` in milliseconds. Tool-call entries embed `Arguments` and `Result` as pretty-printed JSON blocks.
- `## Output response` — only emitted when `OutputResponse` is non-null and carries at least one message.
- `## Error` — only emitted when `Succeeded` is false.

The renderer is a read-side projection over `IAgentRunDiagnostics` — calling it has no effect on the live path.

## Capture-chat-client middleware

Evaluation suites and CI harnesses benefit from deterministic, repeatable chat responses: the first run against a real model captures the response; subsequent runs replay it without hitting the network. `EvaluationCaptureChatClient` is a transparent `IChatClient` decorator that implements this pattern.

```csharp
using NexusLabs.Needlr.AgentFramework.Evaluation;

IChatClient cached = realChatClient.WithEvaluationCapture("./cache/evaluation");

// First call: delegates to realChatClient and persists the response.
// Second call with the same request: served from the store, real client untouched.
ChatResponse response = await cached.GetResponseAsync(messages, options, ct);
```

### Cache key

The key is a SHA-256 lowercase hex digest (64 chars) computed over:

- Each `ChatMessage` formatted as `"{role}:{text}\n"` in order.
- A `---\n` separator.
- The tuple `model`, `temperature`, `top_p`, `max_tokens` from the supplied `ChatOptions` (missing values emit empty strings, floats are formatted with `"R"` + `InvariantCulture`).

The key intentionally excludes tool definitions, response format, and custom options. Two requests that differ only in those fields collide on the same cache entry — if your suite needs them to vary, route them to separate stores.

### Store contract

`IEvaluationCaptureStore` has two methods:

| Method | Semantics |
|---|---|
| `TryGetAsync(key, ct)` | Returns the captured `ChatResponse?` for the key, or `null` on miss. |
| `SaveAsync(key, response, ct)` | Persists the response under the key, overwriting any existing entry. |

Two implementations ship in-box:

- **`FileEvaluationCaptureStore`** — one JSON file per key under a caller-supplied directory. Writes are atomic via write-then-rename. Directory is created on first save.
- Custom stores — implement `IEvaluationCaptureStore` for Redis, blob storage, in-memory dictionaries, or any other backing.

Use the fluent extension to wrap a client with a file store:

```csharp
IChatClient cached = realChatClient.WithEvaluationCapture("./cache/evaluation");
```

Or inject any `IEvaluationCaptureStore`:

```csharp
IChatClient cached = realChatClient.WithEvaluationCapture(myStore);
```

### Streaming

`GetStreamingResponseAsync` aggregates updates via `ToChatResponse()`, saves the aggregated response under the same key used by the non-streaming path, then re-emits the captured response as a single `ChatResponseUpdate`. A cache hit yields one update; the caller sees the same `await foreach` surface either way. Mid-stream failures are not cached — only complete responses reach the store.

### When not to use it

Capture-replay is unsuitable for suites that rely on response variance (temperature sampling across runs), suites that depend on live model behavior changes, or suites where the cache key's intentional omissions (tools, response format) would cause false hits. For those, bypass the decorator and use the underlying client directly.
