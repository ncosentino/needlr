# NDLRMAF023: MaxSupersteps value is invalid

## Cause

The `MaxSupersteps` property on `[AgentGraphEntry]` is set to zero or a negative number.

## Rule Description

The `MaxSupersteps` property on `[AgentGraphEntry]` controls the maximum number of BSP supersteps the graph executor will run. A value of zero or negative means the graph can never make progress — the executor will not execute any agents. `MaxSupersteps` must be a positive integer.

## How to Fix

Set `MaxSupersteps` to a positive integer that represents the maximum depth of execution you want to allow.

### Before

```csharp
[NeedlrAiAgent]
[AgentGraphEntry("pipeline", MaxSupersteps = 0)] // invalid
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }

[NeedlrAiAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class ReviewerAgent { }
```

### After

```csharp
[NeedlrAiAgent]
[AgentGraphEntry("pipeline", MaxSupersteps = 10)]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }

[NeedlrAiAgent]
[AgentGraphNode("pipeline", IsTerminal = true)]
public class ReviewerAgent { }
```

## When to Suppress

Do not suppress this diagnostic. A `MaxSupersteps` value of zero or less always prevents graph execution.

```csharp
#pragma warning disable NDLRMAF023
// Not recommended — graph will not execute
#pragma warning restore NDLRMAF023
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF017](NDLRMAF017.md) — graph has no entry point
- [NDLRMAF016](NDLRMAF016.md) — cycle detected in agent graph
