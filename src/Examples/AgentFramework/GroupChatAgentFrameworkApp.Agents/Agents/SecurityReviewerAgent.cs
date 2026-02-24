using NexusLabs.Needlr.AgentFramework;

namespace GroupChatAgentFrameworkApp.Agents;

/// <summary>
/// Reviews code submissions for security vulnerabilities, injection risks, and insecure patterns.
/// Declared as a member of the <c>code-review</c> group so the source generator includes it in
/// the generated <c>CreateCodeReviewGroupChatWorkflow()</c> extension method.
/// </summary>
[NeedlrAiAgent(
    Description = "Reviews code for security issues.",
    Instructions = """
        You are a security code reviewer. Analyse the submitted code for:
        - SQL injection, XSS, CSRF, and other injection risks
        - Hard-coded secrets or credentials
        - Insecure deserialization or file path traversal
        - Missing authentication or authorisation checks

        Be concise. Flag issues with severity (HIGH / MEDIUM / LOW) and suggest a fix.
        If no issues are found, say "Security: LGTM".
        """,
    FunctionTypes = new Type[0])]
[AgentGroupChatMember("code-review")]
public partial class SecurityReviewerAgent { }
