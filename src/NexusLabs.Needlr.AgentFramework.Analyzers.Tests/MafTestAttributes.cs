namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

/// <summary>
/// Provides C# source code for MAF attributes that can be embedded in analyzer tests.
/// This ensures analyzer tests use the same attribute definitions as the real packages.
/// </summary>
internal static class MafTestAttributes
{
    /// <summary>
    /// Core MAF agent attributes: NeedlrAiAgent, AgentHandoffsTo, AgentGroupChatMember,
    /// AgentFunctionGroup, AgentSequenceMember.
    /// </summary>
    public const string All = @"
namespace NexusLabs.Needlr.AgentFramework
{
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class NeedlrAiAgentAttribute : System.Attribute
    {
        public string? Instructions { get; set; }
        public string? Description { get; set; }
        public System.Type[]? FunctionTypes { get; set; }
        public string[]? FunctionGroups { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class AgentHandoffsToAttribute : System.Attribute
    {
        public AgentHandoffsToAttribute(System.Type targetAgentType, string? handoffReason = null)
        {
            TargetAgentType = targetAgentType;
            HandoffReason = handoffReason;
        }
        public System.Type TargetAgentType { get; }
        public string? HandoffReason { get; }
    }

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class AgentGroupChatMemberAttribute : System.Attribute
    {
        public AgentGroupChatMemberAttribute(string groupName) => GroupName = groupName;
        public string GroupName { get; }
    }

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class AgentFunctionGroupAttribute : System.Attribute
    {
        public AgentFunctionGroupAttribute(string groupName) => GroupName = groupName;
        public string GroupName { get; }
    }

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class AgentSequenceMemberAttribute : System.Attribute
    {
        public AgentSequenceMemberAttribute(string pipelineName, int order)
        {
            PipelineName = pipelineName;
            Order = order;
        }
        public string PipelineName { get; }
        public int Order { get; }
    }

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class WorkflowRunTerminationConditionAttribute : System.Attribute
    {
        public WorkflowRunTerminationConditionAttribute(System.Type conditionType, params object[] ctorArgs)
        {
            ConditionType = conditionType;
            CtorArgs = ctorArgs ?? System.Array.Empty<object>();
        }
        public System.Type ConditionType { get; }
        public object[] CtorArgs { get; }
    }

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class AgentTerminationConditionAttribute : System.Attribute
    {
        public AgentTerminationConditionAttribute(System.Type conditionType, params object[] ctorArgs)
        {
            ConditionType = conditionType;
            CtorArgs = ctorArgs ?? System.Array.Empty<object>();
        }
        public System.Type ConditionType { get; }
        public object[] CtorArgs { get; }
    }

    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class AgentFunctionAttribute : System.Attribute { }

    public interface IWorkflowTerminationCondition
    {
        bool ShouldTerminate(object context);
    }
}
";

    /// <summary>
    /// DAG/graph-specific attributes: AgentGraphEdge, AgentGraphEntry, AgentGraphNode.
    /// </summary>
    public const string GraphAttributes = @"
namespace NexusLabs.Needlr.AgentFramework
{
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class AgentGraphEdgeAttribute : System.Attribute
    {
        public AgentGraphEdgeAttribute(string graphName, System.Type targetAgentType)
        {
            GraphName = graphName;
            TargetAgentType = targetAgentType;
        }
        public string GraphName { get; }
        public System.Type TargetAgentType { get; }
        public string? Condition { get; set; }
        public bool IsRequired { get; set; } = true;
    }

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class AgentGraphEntryAttribute : System.Attribute
    {
        public AgentGraphEntryAttribute(string graphName)
        {
            GraphName = graphName;
        }
        public string GraphName { get; }
        public int MaxSupersteps { get; set; } = 100;
    }

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class AgentGraphNodeAttribute : System.Attribute
    {
        public AgentGraphNodeAttribute(string graphName)
        {
            GraphName = graphName;
        }
        public string GraphName { get; }
        public bool IsTerminal { get; set; }
        public GraphJoinMode JoinMode { get; set; }
    }

    public enum GraphJoinMode
    {
        WaitAll = 0,
        WaitAny = 1,
    }
}
";
}
