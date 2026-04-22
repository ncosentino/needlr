# NDLRMAF019: Graph edge target is not a declared agent

## Cause

An `[AgentGraphEdge]` references a target type that is not decorated with `[NeedlrAiAgent]`.

## Rule Description

Types used as targets in `[AgentGraphEdge]` must be decorated with `[NeedlrAiAgent]` so Needlr can register and resolve them at runtime. Without the agent declaration, the target type will not be discovered by the source generator and the graph will fail to resolve the agent at runtime.

## How to Fix

Add `[NeedlrAiAgent]` to the target type, or remove it from `[AgentGraphEdge]` if it was referenced by mistake.

### Before

```csharp
[NeedlrAiAgent]
[AgentGraphEntry("pipeline")]
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }

// Missing [NeedlrAiAgent]
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

Do not suppress this diagnostic. The graph will fail at runtime if an edge target is not a registered agent.

```csharp
#pragma warning disable NDLRMAF019
// Not recommended — graph edge will fail to resolve
#pragma warning restore NDLRMAF019
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF020](NDLRMAF020.md) — graph edge source is not a declared agent
- [NDLRMAF001](NDLRMAF001.md) — handoff target is not a declared agent (analogous rule for handoff topology)
