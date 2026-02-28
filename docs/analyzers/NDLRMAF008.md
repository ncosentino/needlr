# NDLRMAF008: Agent participates in no topology declaration

## Cause

A class decorated with `[NeedlrAiAgent]` does not appear in any handoff chain, group chat, or sequential pipeline.

## Rule Description

An agent that is not wired into any topology cannot be reached through any generated workflow factory method. It will be included in the agent registry but will never participate in a workflow unless it is invoked programmatically. This is typically an authoring error — either the topology attribute was forgotten, or the agent declaration is a work-in-progress.

This diagnostic is informational and will not block compilation or execution.

## How to Fix

Add the agent to an appropriate topology, or remove the `[NeedlrAiAgent]` attribute if the agent is no longer needed.

### Before

```csharp
[NeedlrAiAgent(Instructions = "You summarize content.")]
public class SummaryAgent { }   // ← no topology attribute
```

### After

```csharp
[NeedlrAiAgent(Instructions = "You summarize content.")]
[AgentSequenceMember("content-pipeline", Order = 2)]
public class SummaryAgent { }   // ← wired into sequential pipeline
```

## When to Suppress

Suppress when declaring a standalone agent that will be invoked programmatically via `IAgentFactory`, not through a generated workflow.

```csharp
#pragma warning disable NDLRMAF008
[NeedlrAiAgent(Instructions = "Standalone utility agent.")]
public class UtilityAgent { }
#pragma warning restore NDLRMAF008
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF002](NDLRMAF002.md) — group chat has fewer than two members
