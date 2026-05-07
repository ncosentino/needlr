---
title: "ADR-0002: Build ScriptedChatClient in Needlr.AgentFramework.Testing rather than depend on a Microsoft package"
status: "Accepted"
date: "2026-05-06"
authors: "Nick Cosentino"
tags: ["architecture", "decision", "agent-framework", "testing", "scripted-chat-client"]
supersedes: ""
superseded_by: ""
---

## Status

Accepted — gates the V1.5 follow-up to ship a paved path for end-to-end agent-loop tool tests.

## Context

The `NexusLabs.Needlr.AgentFramework.Testing` package ships `AgentScenarioRunner` and `PipelineScenarioRunner`, both of which expect callers to wire a fake `IChatClient` through `AgentFrameworkSyringeExtensions.UsingChatClient(...)` to deterministically test agent behavior without a real LLM. The package itself does not provide an `IChatClient` fake, so the Needlr codebase contains 8 ad-hoc, copy-pasted implementations:

- `Examples/AgentFramework/IterativeLoopDiagnosticsApp/Program.cs:200` — `ToolCallingMockChatClient`
- `Examples/AgentFramework/DiagnosticAttributionApp/Program.cs:197` — `MockChatClient`
- `Examples/AgentFramework/AotAgentFrameworkApp/Program.cs:68` — `NoOpChatClient`
- `NexusLabs.Needlr.AgentFramework.Evaluation.Tests/RecordingChatClient.cs`
- `NexusLabs.Needlr.AgentFramework.Evaluation.Tests/ThrowingChatClient.cs`
- `NexusLabs.Needlr.AgentFramework.Tests/DiagnosticsRecordingChatClientTestsHelpers.cs:5` — `FakeInnerChatClient`
- `NexusLabs.Needlr.AgentFramework.Tests/DiagnosticsFunctionInvokingChatClientTestsHelpers.cs:9` — `TestChatClient`
- `NexusLabs.Needlr.AgentFramework.Tests/GraphWorkflowRuntimeTests.cs:1399` — `FailingChatClient`

Downstream consumers writing `[AgentFunction]`-decorated tools hit the same gap and write their own.

The candidate alternative was to depend on a Microsoft testing package and wrap it with a Needlr extension method. This ADR records the investigation outcome and the build-vs-buy decision.

## Decision

Build a first-party `ScriptedChatClient` (and supporting `ChatScriptBuilder`) inside `NexusLabs.Needlr.AgentFramework.Testing`. Do not take a dependency on a Microsoft testing package — none exists, none is on the public roadmap, and the dotnet/extensions team's own tests use an `internal` copy-paste pattern that consumers cannot reuse.

## Investigation

Verified via direct nuget.org and dotnet/extensions repository inspection (May 6, 2026):

| Search | Result |
|---|---|
| `https://www.nuget.org/packages/Microsoft.Extensions.AI.Testing` | HTTP 404 |
| NuGet search `Microsoft.Extensions.AI.Testing` (incl. prerelease) | 0 results |
| NuGet search `Microsoft.Extensions.AI.Fakes` / `Microsoft.Extensions.AI.TestUtilities` | 0 results |
| dotnet/extensions GitHub issues + discussions for testing package | 0 results — no public proposal or roadmap entry |
| dotnet/extensions `src/` libraries scanned for any public scripted/fake `IChatClient` | None found |

What does exist:

1. **`TestChatClient`** at `dotnet/extensions:test/Libraries/Microsoft.Extensions.AI.Abstractions.Tests/TestChatClient.cs` (SHA `95f89a79141e34f8f8defddb60821800cc3bffad`) — an internal callback wrapper used only inside the dotnet/extensions test suite. Not referenced from any `src/` library, not packaged into any NuGet output. Public API surface is two settable `Func<>` callbacks; no turn scheduling or call recording.
2. **`AnonymousDelegatingChatClient`** at `dotnet/extensions:src/Libraries/Microsoft.Extensions.AI/ChatCompletion/AnonymousDelegatingChatClient.cs:18` is `internal sealed`, so consumers cannot instantiate it.
3. **The dotnet team's own multi-turn tests** at `dotnet/extensions:test/Libraries/Microsoft.Extensions.AI.Tests/ChatCompletion/FunctionInvokingChatClientTests.cs:2198-2252` use the `TestChatClient` callback with `messages.Count()` as the turn index — every test re-implements the turn counter via closure. There is no reusable type.
4. **Shipped MEAI packages** (Abstractions / Core / OpenAI / Evaluation, all 10.5.2 as of May 5, 2026, MIT-licensed) provide no `IChatClient` test fakes.

## Consequences

### Positive

- Removes 8 copy-paste fakes once V1.5 ships.
- The `AgentScenarioRunner` doc comment at `src/NexusLabs.Needlr.AgentFramework.Testing/AgentScenarioRunner.cs:24-37` already directs consumers to wire a fake `IChatClient` — `ScriptedChatClient` becomes the canonical answer.
- ~60 lines of code with zero dependencies beyond `Microsoft.Extensions.AI.Abstractions` (already in the chain).
- `ScriptedChatClient` lives next to `AgentScenarioRunner` in the same package — discoverable in IDE.
- Streaming support is free via `ChatResponse.ToChatResponseUpdates()` (stable public MEAI API).

### Negative

- One more shipped public type to maintain forever in Needlr's public API surface.
- If Microsoft ever ships an official testing package, we'd have a competing primitive. Mitigation: design `ScriptedChatClient` as a thin sealed type so it can be marked `[Obsolete]` and forwarded later without breaking consumers.

### Neutral

- The decision does not affect V1 — the `ToolInvocationRunner` does not need a chat client because it invokes the source-generated `AIFunction` wrapper directly, bypassing the LLM.

## Implementation outline (V1.5)

Target file: `src/NexusLabs.Needlr.AgentFramework.Testing/ScriptedChatClient.cs`. One type per file. File-scoped namespace. Full XML docs. Per `AGENTS.md`.

Required surface:
- Sealed class implementing `IChatClient`.
- Constructor accepting an ordered list of `ChatResponse` (the script).
- `ReceivedMessages` — `IReadOnlyList<IReadOnlyList<ChatMessage>>` for assertions.
- `CallCount` — `int`.
- `GetResponseAsync` returns the next scripted response; throws clear `InvalidOperationException` on overflow ("script exhausted").
- `GetStreamingResponseAsync` shells through `GetResponseAsync` + `ToChatResponseUpdates()`.
- A separate `ChatScriptBuilder` (also one type per file) for fluent script assembly with `OnTurn(n, t => t.RequestTool(...))` / `OnTurn(n, t => t.RespondText(...))` shorthand.

Replaces (post-ship migration):
- All 8 ad-hoc fakes listed above. Forcing-function migration done as part of V1.5 to keep the abstraction honest.

## References

- Spike research: `meai-testing-spike` background agent, May 6, 2026.
- Needlr issue: build-vs-buy gate for V1.5 follow-up todo `meai-testing-spike` (was satisfied by this ADR; new V1.5 todo `scripted-chat-client-build` will track implementation).
