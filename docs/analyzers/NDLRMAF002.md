# NDLRMAF002: Group chat has fewer than two members

## Cause

A group chat named `"X"` has only one class decorated with `[AgentGroupChatMember("X")]` in the compilation.

## Rule Description

A group chat requires at least two participants to function — a single-member group chat cannot produce a multi-agent exchange. This diagnostic fires at compilation end after all `[AgentGroupChatMember]` declarations are collected.

> **Note:** This is a compilation-end diagnostic. It appears after the build completes, not as you type.

## How to Fix

Either add a second agent to the group chat, or remove the `[AgentGroupChatMember]` attribute if the group chat was declared in error.

### Before

```csharp
[NeedlrAiAgent(Instructions = "Review code.")]
[AgentGroupChatMember("code-review")]
public class ReviewerAgent { }
// No second agent in the "code-review" group
```

### After

```csharp
[NeedlrAiAgent(Instructions = "Review code.")]
[AgentGroupChatMember("code-review")]
public class ReviewerAgent { }

[NeedlrAiAgent(Instructions = "Approve or reject changes.")]
[AgentGroupChatMember("code-review")]
public class ApproverAgent { }
```

## When to Suppress

Only suppress if you are building a group chat incrementally across multiple assemblies where the second member is defined elsewhere and will be merged at link time.

```csharp
#pragma warning disable NDLRMAF002
[AgentGroupChatMember("code-review")]
public class ReviewerAgent { }
#pragma warning restore NDLRMAF002
```

## See Also

- [AI Integrations — Multi-Agent Orchestration](../ai-integrations.md#multi-agent-orchestration)
- [NDLRMAF008](NDLRMAF008.md) — agent participates in no topology
