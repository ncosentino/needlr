# NDLRMAF017: Graph has no entry point

## Cause

A named agent graph has `[AgentGraphEdge]` declarations but no `[AgentGraphEntry]` to identify the starting agent.

## Rule Description

Every named agent graph requires exactly one `[AgentGraphEntry]` declaration to identify the starting agent. Without an entry point, the source generator cannot emit a factory method for the graph workflow. The analyzer detects graphs that have edge declarations but no corresponding entry point.

## How to Fix

Add `[AgentGraphEntry]` to the agent class that should be the starting point of the graph.

### Before

```csharp
[NeedlrAiAgent]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { } // no [AgentGraphEntry]

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

Do not suppress this diagnostic. A graph without an entry point cannot be executed and no workflow factory method will be generated.

```csharp
#pragma warning disable NDLRMAF017
// Not recommended — graph will not be generated
#pragma warning restore NDLRMAF017
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF018](NDLRMAF018.md) — graph has multiple entry points
- [NDLRMAF021](NDLRMAF021.md) — graph entry point is not a declared agent
