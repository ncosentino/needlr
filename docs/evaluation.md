# Evaluation

Needlr's agent framework is designed to plug directly into [Microsoft.Extensions.AI.Evaluation](https://learn.microsoft.com/en-us/dotnet/ai/evaluation/libraries) without adapters or flattening.

## Overview

`Microsoft.Extensions.AI.Evaluation` (MEAI.Evaluation) evaluates LLM interactions by consuming native MEAI primitives: `ChatMessage`, `ChatResponse`, and `UsageDetails`. To feed a Needlr-powered agent run into an evaluator, the same shapes must be present on Needlr's result surfaces.

Phase 1 of Needlr's evaluation support retypes the live-path surfaces so evaluators slot in directly — no string flattening, no re-hydration step.

## Phase 1 live-path retypes

| Surface | Before | After |
|---|---|---|
| `IterativeLoopResult.FinalResponse` | `string?` | `ChatResponse?` |
| `IterationRecord.ResponseText` | `string?` | `FinalResponse : ChatResponse?` (renamed + retyped) |
| `TerminationContext.ResponseText` | `string` (required) | Removed; `LastMessage : ChatMessage?` added; `Usage` populated |
| `IAgentStageResult.ResponseText` | `string` | `FinalResponse : ChatResponse?` |
| `AgentStageResult` | positional `string` | positional `ChatResponse?` |
| `IPipelineRunResult.Responses` | `IReadOnlyDictionary<string, string>` | `FinalResponses : IReadOnlyDictionary<string, ChatResponse?>` |

These are **hard breaks** on the alpha channel. Callers that previously read flattened strings now receive full `ChatResponse` / `ChatMessage` objects. Use `response.Text` to recover the string projection when needed.

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

The returned sequence alternates `FunctionCallContent` (the call) and `FunctionResultContent` (the result) in call order — the exact shape MEAI.Evaluation evaluators consume when they land in a future `Microsoft.Extensions.AI.Evaluation.Quality` release that ships a tool-call evaluator.

## Example

See [`src/Examples/AgentFramework/EvaluationExampleApp`](https://github.com/ncosentino/needlr/tree/main/src/Examples/AgentFramework/EvaluationExampleApp) for a self-contained example. It builds a sample `IterativeLoopResult`, feeds it to `RelevanceEvaluator`, and prints the trajectory shape — no API keys required (uses a local mock judge `IChatClient`).

Run it:

```bash
dotnet run --project src/Examples/AgentFramework/EvaluationExampleApp
```

## Post-hoc replay from diagnostics

As of Phase 2, `ChatCompletionDiagnostics` and `ToolCallDiagnostics` capture enough information that a serialized `AgentRunDiagnostics` is sufficient for offline replay and evaluation — no need to re-invoke the agent or the underlying model.

| Diagnostic | New property | Contents |
|---|---|---|
| `ChatCompletionDiagnostics` | `RequestMessages : IReadOnlyList<ChatMessage>?` | The exact messages sent to the chat client on that call. |
| `ChatCompletionDiagnostics` | `Response : ChatResponse?` | The full response returned by the chat client (null on failure). |
| `ToolCallDiagnostics` | `Arguments : IReadOnlyDictionary<string, object?>?` | Snapshot of the arguments the tool was invoked with. |
| `ToolCallDiagnostics` | `Result : object?` | The value returned by the tool invocation (null on failure). |

All four properties are init-only, default to `null`, and are populated automatically by `DiagnosticsChatClientMiddleware` and `DiagnosticsFunctionCallingMiddleware`. On the alpha channel, capture is always on — there is no opt-out flag yet.

With these in hand you can rehydrate a MEAI `ChatResponse` + trajectory from a persisted diagnostics document and feed it into any `IEvaluator` offline.

## Full-fidelity transcripts

Evaluation and agent-assisted debugging both depend on **replay-grade** transcripts — every chat exchange, not just totals.

### Streaming capture

`DiagnosticsChatClientMiddleware` instruments both paths:

- `GetResponseAsync` — captured on completion (existed prior).
- `GetStreamingResponseAsync` — streaming updates are teed through to the caller in real time, then buffered via `ToChatResponse()` at stream completion. The synthesized `ChatResponse` is written to `ChatCompletionDiagnostics.Response` with identical shape to the non-streaming path.

Errors mid-stream still populate `ChatCompletionDiagnostics.{Success=false, ErrorMessage, Response}` with the partial response built from updates observed before the failure. No data is silently dropped.

### Streaming agent runs

`DiagnosticsAgentRunMiddleware` instruments both agent-run paths:

- `HandleAsync` — captured on completion (existed prior).
- `HandleStreamingAsync` — `AgentResponseUpdate`s are teed through to the caller in real time while distinct non-null `MessageId`s accumulate into `AgentRunDiagnostics.TotalOutputMessages`. On stream completion the builder is finalized and written to the configured `IAgentDiagnosticsWriter` with identical shape to the non-streaming path.

Mid-stream failures record the partial output-message count observed so far and call `AgentRunDiagnosticsBuilder.RecordFailure(...)` before rethrowing, so streaming agent runs surface in diagnostics the same way non-streaming runs do.

### Character counts

Tokens are an LLM-reported abstraction; character counts are a direct measure of the payload Needlr actually shipped and received. Both are captured on every completion.

- **`ChatCompletionDiagnostics.RequestCharCount`** — sum of `TextContent.Text?.Length` across all `RequestMessages`.
- **`ChatCompletionDiagnostics.ResponseCharCount`** — sum of text length across the aggregated `Response`.
- **`ToolCallDiagnostics.ArgumentsCharCount`** — length of the `System.Text.Json` serialization of the captured `Arguments` dictionary.
- **`ToolCallDiagnostics.ResultCharCount`** — length of the `System.Text.Json` serialization of the captured `Result`.

Populated automatically by `DiagnosticsChatClientMiddleware` and `DiagnosticsFunctionCallingMiddleware` on both success and failure paths. `DiagnosticsCharCounter` (in `NexusLabs.Needlr.AgentFramework.Diagnostics`) exposes the same helpers for callers who want to compute counts outside the middlewares. All helpers are null-safe and exception-tolerant — a counter failure never destabilizes the live path; it just yields `0`.

## Further phases (planned)

- **Phase 3**— Ship a dedicated `NexusLabs.Needlr.AgentFramework.Evaluation` assembly with composite evaluators (`IterativeLoopEvaluator`, `WorkflowEvaluator`, `PipelineEvaluator`) and an opt-in `EvaluationCaptureChatClient` middleware.
- **Phase 4** — xUnit harness (`NeedlrEvaluationFixture`, `[NeedlrEvaluationFact]`) + Needlr-native evaluators (`ToolCallTrajectoryEvaluator`, `IterationCoherenceEvaluator`, `TerminationAppropriatenessEvaluator`).
