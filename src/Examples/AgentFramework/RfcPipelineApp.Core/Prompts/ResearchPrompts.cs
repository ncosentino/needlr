namespace RfcPipelineApp.Core.Prompts;

/// <summary>
/// Prompt templates for the research and research-brief pipeline stages.
/// </summary>
internal static class ResearchPrompts
{
    /// <summary>
    /// Builds the prompt for the initial research stage. The agent explores
    /// the problem space, prior art, and existing solutions.
    /// </summary>
    internal static string BuildResearch(RfcAssignment assignment)
    {
        var constraints = string.Join("\n- ", assignment.Constraints);
        var context = string.Join("\n- ", assignment.ExistingContext);

        return $"""
            You are a senior technical researcher preparing background material for an RFC.

            ## Feature Request
            **Title:** {assignment.FeatureTitle}
            **Description:** {assignment.Description}

            ## Constraints
            - {constraints}

            ## Existing Context
            - {context}

            ## Your Task
            Conduct thorough research on this problem space. You must cover:

            1. **Prior art** — How have similar features been implemented in other systems,
               frameworks, or languages? Name specific projects and approaches.
            2. **Existing solutions** — Are there libraries, RFCs, or specifications that
               already address parts of this problem?
            3. **Known pitfalls** — What challenges or failure modes have others encountered?
            4. **Industry trends** — What direction is the broader ecosystem heading?
            5. **Key terminology** — Define domain-specific terms the RFC audience needs.

            ## Output Format
            Write your findings as a structured markdown document to the workspace file
            `{assignment.ResearchPath}`. Use H2 headers for each section above.
            Be specific — cite project names, version numbers, and concrete examples.
            Aim for 800–1500 words of substantive research, not vague summaries.

            Write the file now.
            """;
    }

    /// <summary>
    /// Builds the prompt for synthesizing raw research into a concise brief.
    /// </summary>
    internal static string BuildResearchBrief(RfcAssignment assignment)
    {
        return $"""
            You are a technical writer distilling research into an actionable brief.

            ## Instructions
            Read the research document at `{assignment.ResearchPath}` in the workspace.
            Synthesize it into a focused research brief that will guide the RFC drafting stages.

            ## Required Sections
            1. **Problem Summary** — 2-3 sentences defining the core problem.
            2. **Key Findings** — Bullet list of the 5-8 most important discoveries.
            3. **Recommended Approach** — Based on the research, which direction looks most
               promising and why? Be opinionated.
            4. **Risks and Open Questions** — What remains uncertain? What could go wrong?
            5. **References** — Links or citations to the most important sources.

            ## Output
            Overwrite `{assignment.ResearchPath}` with the synthesized brief. The brief should
            be 400-800 words — dense and actionable, not a rehash of raw notes.

            Read the file and write the updated version now.
            """;
    }
}
