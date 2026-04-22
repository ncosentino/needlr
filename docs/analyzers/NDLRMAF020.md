# NDLRMAF020: Graph edge source is not a declared agent

## Cause

A class has `[AgentGraphEdge]` but is not itself decorated with `[NeedlrAiAgent]`.

## Rule Description

The class that carries `[AgentGraphEdge]` is a node in the agent graph and must be registered with Needlr via `[NeedlrAiAgent]`. Without the agent declaration, the source class will not be part of any generated workflow and the edge declaration is meaningless.

## How to Fix

Add `[NeedlrAiAgent]` to the class, or remove `[AgentGraphEdge]` if it was added by mistake.

### Before

```csharp
// Missing [NeedlrAiAgent]
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

Suppress if the `[AgentGraphEdge]` is intentionally on a non-agent class for metadata purposes (uncommon).

```csharp
#pragma warning disable NDLRMAF020
[AgentGraphEdge("pipeline", typeof(ReviewerAgent))]
public class PlannerAgent { }
#pragma warning restore NDLRMAF020
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF019](NDLRMAF019.md) — graph edge target is not a declared agent
- [NDLRMAF003](NDLRMAF003.md) — handoff source is not a declared agent (analogous rule for handoff topology)
