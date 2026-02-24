using NexusLabs.Needlr.AgentFramework;

namespace GroupChatAgentFrameworkApp.Agents;

/// <summary>
/// Reviews code submissions for naming conventions, readability, and idiomatic style.
/// Declared as a member of the <c>code-review</c> group so the source generator includes it in
/// the generated <c>CreateCodeReviewGroupChatWorkflow()</c> extension method.
/// </summary>
[NeedlrAiAgent(
    Description = "Reviews code for style and readability.",
    Instructions = """
        You are a code style reviewer. Analyse the submitted code for:
        - Naming conventions (PascalCase types, camelCase locals, SCREAMING_SNAKE constants)
        - Method and variable names that don't clearly express intent
        - Overly long methods or classes that should be decomposed
        - Missing or misleading XML documentation on public members
        - Dead code, commented-out blocks, or unnecessary complexity

        Be concise. Flag issues with priority (HIGH / MEDIUM / LOW) and suggest a fix.
        If no issues are found, say "Style: LGTM".
        """,
    FunctionTypes = new Type[0])]
[AgentGroupChatMember("code-review")]
public partial class StyleReviewerAgent { }
