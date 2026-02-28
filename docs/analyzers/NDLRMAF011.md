# NDLRMAF011: Prefer [AgentTerminationCondition] for group chat members

## Cause

A class decorated with `[AgentGroupChatMember]` also declares `[WorkflowRunTerminationCondition]`.

## Rule Description

`[WorkflowRunTerminationCondition]` is a Layer 2 (post-response) termination mechanism — it fires inside Needlr's event loop after a complete agent response has been emitted. For group chats, a Layer 1 mechanism is available: `[AgentTerminationCondition]` wires conditions into the group chat manager, which fires before the next agent is called. Layer 1 termination is strictly preferable for group chats because:

- It stops the workflow before another round-trip to the model occurs
- It provides cleaner separation between "this response completes the workflow" and "this response starts the next turn"

`[WorkflowRunTerminationCondition]` on a group chat member is not incorrect — the condition will fire — but it incurs an extra model turn compared to `[AgentTerminationCondition]`.

This diagnostic is informational.

## How to Fix

Replace `[WorkflowRunTerminationCondition]` with `[AgentTerminationCondition]` on the group chat member.

### Before

```csharp
[NeedlrAiAgent]
[AgentGroupChatMember("code-review")]
[WorkflowRunTerminationCondition(typeof(KeywordTerminationCondition), "APPROVED")]
public class ApproverAgent { }
```

### After

```csharp
[NeedlrAiAgent]
[AgentGroupChatMember("code-review")]
[AgentTerminationCondition(typeof(KeywordTerminationCondition), "APPROVED")]
public class ApproverAgent { }
```

## When to Suppress

Suppress if you intentionally want the condition to fire in Layer 2 (e.g. you are also using the raw workflow object in code that bypasses the generated `Run*Async()` method and need consistent termination semantics).

```csharp
#pragma warning disable NDLRMAF011
[AgentGroupChatMember("code-review")]
[WorkflowRunTerminationCondition(typeof(KeywordTerminationCondition), "APPROVED")]
public class ApproverAgent { }
#pragma warning restore NDLRMAF011
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF009](NDLRMAF009.md) — condition declared on a non-agent class
- [NDLRMAF010](NDLRMAF010.md) — condition type does not implement IWorkflowTerminationCondition
