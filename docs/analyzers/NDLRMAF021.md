# NDLRMAF021: Graph entry point is not a declared agent

## Cause

A class has `[AgentGraphEntry]` but is not itself decorated with `[NeedlrAiAgent]`.

## Rule Description

The class that carries `[AgentGraphEntry]` is the starting node of the graph workflow and must be registered with Needlr via `[NeedlrAiAgent]`. Without the agent declaration, the entry point class will not be discovered by the source generator and the graph workflow cannot be created.

## How to Fix

Add `[NeedlrAiAgent]` to the class, or remove `[AgentGraphEntry]` if it was added by mistake.

### Before

```csharp
// Missing [NeedlrAiAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }

[NeedlrAiAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class ReviewerAgent { }
```

### After

```csharp
[NeedlrAiAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }

[NeedlrAiAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class ReviewerAgent { }
```

## When to Suppress

Suppress if the `[AgentGraphEntry]` is intentionally on a non-agent class for metadata purposes (uncommon).

```csharp
#pragma warning disable NDLRMAF021
[AgentGraphEntry("pipeline")]
public class PlannerAgent { }
#pragma warning restore NDLRMAF021
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF017](NDLRMAF017.md) — graph has no entry point
- [NDLRMAF020](NDLRMAF020.md) — graph edge source is not a declared agent
