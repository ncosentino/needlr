namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

/// <summary>
/// Provides C# source code for MAF attributes that can be embedded in analyzer tests.
/// This ensures analyzer tests use the same attribute definitions as the real packages.
/// </summary>
internal static class MafTestAttributes
{
    /// <summary>
    /// Core MAF agent attributes: NeedlrAiAgent, AgentHandoffsTo, AgentGroupChatMember.
    /// </summary>
    public const string All = @"
namespace NexusLabs.Needlr.AgentFramework
{
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class NeedlrAiAgentAttribute : System.Attribute
    {
        public string? Instructions { get; set; }
        public string? Description { get; set; }
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
}";
}
