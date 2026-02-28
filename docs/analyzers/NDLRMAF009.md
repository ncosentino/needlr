# NDLRMAF009: [WorkflowRunTerminationCondition] declared on a non-agent class

## Cause

A class bearing `[WorkflowRunTerminationCondition]` is not decorated with any topology attribute (`[AgentHandoffsTo]`, `[AgentGroupChatMember]`, or `[AgentSequenceMember]`).

## Rule Description

`[WorkflowRunTerminationCondition]` is a Layer 2 termination declaration — it instructs the source generator to emit a `Run*Async()` method that includes the declared condition. The generator infers which workflow to attach the condition to from the agent's topology membership. If the class is not part of any topology, the attribute is orphaned and the condition will never be wired into a generated runner.

## How to Fix

Add the appropriate topology attribute, or move the `[WorkflowRunTerminationCondition]` to a class that participates in a workflow.

### Before

```csharp
[NeedlrAiAgent]
[WorkflowRunTerminationCondition(typeof(KeywordTerminationCondition), "DONE")]
public class SummaryAgent { }   // ← no topology attribute
```

### After

```csharp
[NeedlrAiAgent]
[AgentSequenceMember("content-pipeline", Order = 1)]
[WorkflowRunTerminationCondition(typeof(KeywordTerminationCondition), "DONE")]
public class SummaryAgent { }
```

## When to Suppress

Only suppress if you are attaching a condition to an agent that you plan to invoke programmatically and the topology will be configured at runtime.

```csharp
#pragma warning disable NDLRMAF009
[WorkflowRunTerminationCondition(typeof(KeywordTerminationCondition), "DONE")]
public class SummaryAgent { }
#pragma warning restore NDLRMAF009
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF010](NDLRMAF010.md) — condition type does not implement IWorkflowTerminationCondition
- [NDLRMAF011](NDLRMAF011.md) — prefer [AgentTerminationCondition] for group chat members
