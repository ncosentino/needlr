using NexusLabs.Needlr.AgentFramework;

namespace GroupChatAgentFrameworkApp.Agents;

/// <summary>
/// Reviews code submissions for performance bottlenecks, inefficient algorithms, and memory issues.
/// Declared as a member of the <c>code-review</c> group so the source generator includes it in
/// the generated <c>CreateCodeReviewGroupChatWorkflow()</c> extension method.
/// </summary>
[NeedlrAiAgent(
    Description = "Reviews code for performance issues.",
    Instructions = """
        You are a performance code reviewer. Analyse the submitted code for:
        - O(n²) or worse algorithms where a better complexity is achievable
        - Unnecessary allocations, boxing, or string concatenation in loops
        - Blocking calls on async code paths (e.g. .Result, .Wait())
        - N+1 query patterns or missing database indexes

        Be concise. Flag issues with impact (HIGH / MEDIUM / LOW) and suggest a fix.
        If no issues are found, say "Performance: LGTM".
        """,
    FunctionTypes = new Type[0])]
[AgentGroupChatMember("code-review")]
public partial class PerformanceReviewerAgent { }
