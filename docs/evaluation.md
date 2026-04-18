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

## Further phases (planned)

- **Phase 2** — Add lossless transcript capture to `ChatCompletionDiagnostics` and tool argument/result capture to `ToolCallDiagnostics` so a serialized `AgentRunDiagnostics` is sufficient for offline replay + evaluation.
- **Phase 3** — Ship a dedicated `NexusLabs.Needlr.AgentFramework.Evaluation` assembly with composite evaluators (`IterativeLoopEvaluator`, `WorkflowEvaluator`, `PipelineEvaluator`) and an opt-in `EvaluationCaptureChatClient` middleware.
- **Phase 4** — xUnit harness (`NeedlrEvaluationFixture`, `[NeedlrEvaluationFact]`) + Needlr-native evaluators (`ToolCallTrajectoryEvaluator`, `IterationCoherenceEvaluator`, `TerminationAppropriatenessEvaluator`).
