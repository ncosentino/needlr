# NDLRMAF004: Cyclic handoff chain detected

## Cause

The `[AgentHandoffsTo]` declarations in the compilation form a cycle (e.g. A → B → A, or A → B → C → A).

## Rule Description

A cyclic handoff chain means no workflow using these agents can reach a terminal state via normal handoff logic — execution will loop indefinitely between the agents in the cycle. This diagnostic fires at compilation end after the full handoff graph is constructed.

> **Note:** This is a compilation-end diagnostic. It appears after the build completes, not as you type.

## How to Fix

Break the cycle by removing one of the `[AgentHandoffsTo]` declarations or by introducing a separate non-participating agent as the terminal step.

### Before

```csharp
[NeedlrAiAgent]
[AgentHandoffsTo(typeof(AgentB))]
public class AgentA { }

[NeedlrAiAgent]
[AgentHandoffsTo(typeof(AgentA))]  // ← creates cycle A → B → A
public class AgentB { }
```

### After

```csharp
[NeedlrAiAgent]
[AgentHandoffsTo(typeof(AgentB))]
public class AgentA { }

[NeedlrAiAgent]
[AgentHandoffsTo(typeof(AgentC))]  // ← breaks cycle, routes to terminal agent
public class AgentB { }

[NeedlrAiAgent(Instructions = "Produce the final answer.")]
public class AgentC { }
```

## When to Suppress

Cyclic handoffs are almost always a design error. Suppress only if you are intentionally modelling a re-entrant workflow and handling termination via `[AgentTerminationCondition]`.

```csharp
#pragma warning disable NDLRMAF004
[AgentHandoffsTo(typeof(AgentB))]
public class AgentA { }
#pragma warning restore NDLRMAF004
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF001](NDLRMAF001.md) — target type is not a declared agent
