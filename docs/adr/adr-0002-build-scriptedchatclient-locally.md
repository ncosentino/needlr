---
title: "ADR-0002: Build ScriptedChatClient in Needlr.AgentFramework.Testing rather than depend on a Microsoft package"
status: "Accepted"
date: "2026-05-06"
authors: "Nick Cosentino"
tags: ["architecture", "decision", "agent-framework", "testing", "scripted-chat-client"]
supersedes: ""
superseded_by: ""
---

## Context and scope

`NexusLabs.Needlr.AgentFramework.Testing` provides runners for deterministic agent and
pipeline scenarios, but callers must supply an `IChatClient`. Needlr's tests and examples
therefore contain multiple one-off chat-client implementations for scripted responses,
multi-turn tool calls, failure injection, and call recording. Consumers face the same
duplication when testing agents without a live model.

At the time of this decision, the shipped Microsoft.Extensions.AI packages did not
include a public testing package or reusable scripted `IChatClient`. The dotnet/extensions
test suite had a callback-based `TestChatClient` in a test assembly, but that type was not
part of a package consumers could reference.

This decision covers a deterministic scripted test client for agent-loop tests. It does
not attempt to emulate a model provider, network transport, token accounting, streaming
timing, or every possible `IChatClient` implementation detail.

## Decision drivers

- Agent-loop tests must run deterministically without credentials or network access.
- Needlr and its consumers should not repeatedly implement the same multi-turn fake.
- The public testing surface should remain small and depend only on existing MEAI
  abstractions.
- The client should support both response scripting and request inspection.
- Needlr should prefer an official Microsoft test primitive if one becomes available and
  satisfies the same contract.

## Decision

Needlr will build a first-party `ScriptedChatClient` in
`NexusLabs.Needlr.AgentFramework.Testing`, with a small `ChatScriptBuilder` for readable
multi-turn scripts.

The client will:

- implement `IChatClient` directly;
- return ordered scripted responses and fail clearly when the script is exhausted;
- record received messages and call count for assertions;
- support streaming by projecting scripted responses through MEAI's response-update
  model;
- remain sealed and intentionally narrow so it can be deprecated or adapted if Microsoft
  later ships an equivalent public testing primitive.

The Testing package will not take a dependency on a speculative Microsoft testing package
or copy an internal test-only type from dotnet/extensions.

## Alternatives considered

### Depend on an official Microsoft.Extensions.AI testing package

An official package would minimize Needlr-owned surface and align consumers with upstream
MEAI conventions. It was not available when this decision was made, so it could not
provide a usable dependency. This remains the preferred future replacement if Microsoft
ships an equivalent public contract.

### Continue using local fakes in each test project

Local fakes avoid adding a public Needlr type and let each test implement only what it
needs. This was rejected because multi-turn sequencing, exhaustion behavior, streaming,
and request recording were already being duplicated across the repository and by
consumers.

### Keep a shared fake internal to Needlr's test assemblies

An internal helper would improve Needlr's own tests without committing to public API. It
was rejected because the same gap exists for every consumer of `AgentScenarioRunner`; an
internal helper would leave the public testing workflow incomplete.

### Use mocks directly for `IChatClient`

Mocking is appropriate for a single boundary call, but multi-turn agent behavior requires
ordered responses and captured conversation state. Rebuilding that state machine in each
mock setup is less readable and no less coupled than a purpose-built scripted client.

## Consequences

### Positive

- Agent-loop tests gain one credential-free, deterministic client for multi-turn scripts.
- Consumers can assert the messages sent to the model without writing another fake.
- The type lives beside `AgentScenarioRunner`, making the supported test path discoverable.
- No new package dependency is required beyond the MEAI abstractions already used by the
  Agent Framework.

### Negative

- Needlr assumes maintenance responsibility for another public testing type.
- The client cannot model every provider-specific behavior without losing its intentionally
  small scope.
- If Microsoft ships an equivalent package, Needlr will temporarily have a competing
  abstraction and will need a deliberate migration.

### Neutral

- Tests that need provider-specific transport or streaming behavior may continue to use
  specialized boundary fakes.
- This decision does not change production chat-client composition.

## Confirmation

This accepted decision is not yet fully implemented. The current Testing package still
exposes scenario runners without a public `ScriptedChatClient`, and
`docs/testing-tools.md` identifies the scripted client as planned work.

Implementation will confirm the decision when:

- the Testing package exposes the scripted client and builder;
- tests verify ordered responses, exhaustion, request recording, cancellation, and
  streaming projection;
- at least one end-to-end agent scenario uses the public client; and
- representative one-off chat-client fakes can be removed without losing coverage.

If Microsoft publishes a supported equivalent before those conditions are met, this ADR
must be reconsidered rather than implemented by default.

## References

- [`docs/testing-tools.md`](../testing-tools.md) explains why full agent scenarios require
  a deterministic `IChatClient` and records the current absence of the shared client.
- [`AgentScenarioRunner` at the confirmation snapshot](https://github.com/ncosentino/needlr/blob/78b27a1c5eddd5b8fa3e0c07dce629159b39e436/src/NexusLabs.Needlr.AgentFramework.Testing/AgentScenarioRunner.cs)
  is the public test harness that needs the scripted boundary.
- [dotnet/extensions `TestChatClient` at an immutable commit](https://github.com/dotnet/extensions/blob/c221abef4b4f1bf3fcf0bda27490e8b26bb479f4/test/Libraries/Microsoft.Extensions.AI.Abstractions.Tests/TestChatClient.cs)
  demonstrates that Microsoft used a test-assembly helper but did not expose it as a
  consumer package.
