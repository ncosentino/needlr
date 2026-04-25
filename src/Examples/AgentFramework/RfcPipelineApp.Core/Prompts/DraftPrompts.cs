using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

namespace RfcPipelineApp.Core.Prompts;

/// <summary>
/// Prompt templates for the RFC drafting pipeline stages.
/// </summary>
internal static class DraftPrompts
{
    /// <summary>
    /// Builds the prompt for proposing the RFC document outline.
    /// </summary>
    internal static string BuildOutline(
        RfcAssignment assignment,
        StageExecutionContext context)
    {
        var researchBrief = PromptHelpers.ReadWorkspaceFile(context, assignment.ResearchPath);

        return $"""
            You are a senior architect designing the structure of an RFC document.

            ## Research Brief
            {researchBrief}

            ## Context
            The RFC is for: **{assignment.FeatureTitle}**
            Target audience: **{assignment.TargetAudience}**

            ## Your Task
            Propose a detailed outline for the RFC document. The outline must include at
            minimum these sections (you may add others if warranted):

            1. Problem Statement
            2. Background
            3. Proposed Solution
            4. Technical Design
            5. Alternatives Considered
            6. Trade-offs
            7. Migration Plan
            8. Rollback Strategy
            9. Open Questions

            For each section, write 1-2 sentences describing what it should contain.
            Add sub-sections where the topic is complex enough to warrant them.

            ## Output
            Produce the outline as your response — a markdown document with H2 headers for
            each section and bullet points for sub-topics.
            """;
    }

    /// <summary>
    /// Builds the prompt for drafting the Problem Statement and Background sections.
    /// </summary>
    internal static string BuildProblemStatement(
        RfcAssignment assignment,
        StageExecutionContext context)
    {
        var researchBrief = PromptHelpers.ReadWorkspaceFile(context, assignment.ResearchPath);
        var outline = PromptHelpers.ReadWorkspaceFile(context, assignment.OutlinePath);

        return $"""
            You are an RFC author writing the foundational sections of a design document.

            ## Research Brief
            {researchBrief}

            ## Outline
            {outline}

            ## Your Task
            Write the **Problem Statement** and **Background** sections of the RFC.

            ### Problem Statement Guidelines
            - State the problem in 3-5 clear paragraphs
            - Explain who is affected and how
            - Quantify impact where possible (performance, developer productivity, reliability)
            - Do NOT propose solutions here — that comes later

            ### Background Guidelines
            - Provide necessary context for readers unfamiliar with the domain
            - Reference relevant prior art from the research brief
            - Define key terminology
            - Explain the current state of the system and why it's insufficient

            ## Output Format
            Produce a markdown document as your response starting with:
            ```
            # RFC: {assignment.FeatureTitle}

            ## Problem Statement
            ...

            ## Background
            ...
            ```

            Be specific, use concrete examples, and write for the {assignment.TargetAudience}.
            Aim for 500-800 words across both sections.
            """;
    }

    /// <summary>
    /// Builds the prompt for drafting the Proposed Solution and Technical Design sections.
    /// </summary>
    internal static string BuildProposedSolution(
        RfcAssignment assignment,
        StageExecutionContext context)
    {
        var constraints = string.Join("\n- ", assignment.Constraints);
        var currentDraft = PromptHelpers.ReadWorkspaceFile(context, assignment.DraftPath);
        var researchBrief = PromptHelpers.ReadWorkspaceFile(context, assignment.ResearchPath);
        var outline = PromptHelpers.ReadWorkspaceFile(context, assignment.OutlinePath);

        return $"""
            You are an RFC author writing the core technical proposal.

            ## Current Draft
            {currentDraft}

            ## Research Brief
            {researchBrief}

            ## Outline
            {outline}

            ## Constraints That Must Be Satisfied
            - {constraints}

            ## Your Task
            Write the **Proposed Solution** and **Technical Design** sections to append
            to the existing draft above.

            ### Proposed Solution Guidelines
            - Present the recommended approach in 3-5 paragraphs
            - Explain the key insight or architectural decision
            - Show how it satisfies each constraint listed above
            - Include a high-level diagram description if helpful (use ASCII art or describe it)

            ### Technical Design Guidelines
            - Break the implementation into components or phases
            - Describe interfaces, data models, and key algorithms
            - Specify API contracts where applicable (use code blocks for signatures)
            - Address concurrency, error handling, and edge cases
            - Estimate complexity for each component (Low/Medium/High)

            ## Output
            Produce ONLY the two new sections (Proposed Solution and Technical Design) as
            your response. Do NOT repeat the existing draft content — only output the new
            sections to be appended. Aim for 600-1000 words across both sections.
            """;
    }

    /// <summary>
    /// Builds the prompt for drafting the Alternatives and Trade-offs sections.
    /// </summary>
    internal static string BuildAlternatives(
        RfcAssignment assignment,
        StageExecutionContext context)
    {
        var currentDraft = PromptHelpers.ReadWorkspaceFile(context, assignment.DraftPath);
        var researchBrief = PromptHelpers.ReadWorkspaceFile(context, assignment.ResearchPath);

        return $"""
            You are an RFC author providing a balanced analysis of alternatives.

            ## Current Draft
            {currentDraft}

            ## Research Brief
            {researchBrief}

            ## Your Task
            Write the **Alternatives Considered** and **Trade-offs** sections to append
            to the existing draft above.

            ### Alternatives Considered Guidelines
            - Present at least 3 alternative approaches that were evaluated
            - For each alternative, explain:
              * What the approach entails
              * Its advantages
              * Why it was rejected (be specific — cost, complexity, risk, etc.)
            - Include "Do Nothing" as one alternative and explain its consequences

            ### Trade-offs Guidelines
            - Create a comparison matrix of the proposed solution vs. top 2 alternatives
            - Evaluate on dimensions: complexity, performance, maintainability, risk, time-to-market
            - Be honest about the proposed solution's weaknesses
            - Explain what you're deliberately trading away and why it's acceptable

            ## Output
            Produce ONLY the two new sections (Alternatives Considered and Trade-offs) as
            your response. Do NOT repeat the existing draft content — only output the new
            sections to be appended. Aim for 400-700 words across both sections.
            """;
    }

    /// <summary>
    /// Builds the prompt for drafting the Migration Plan and Rollback Strategy sections.
    /// </summary>
    internal static string BuildMigration(
        RfcAssignment assignment,
        StageExecutionContext context)
    {
        var currentDraft = PromptHelpers.ReadWorkspaceFile(context, assignment.DraftPath);

        return $"""
            You are an RFC author writing the operational sections of the design document.

            ## Current Draft
            {currentDraft}

            ## Your Task
            Write the **Migration Plan** and **Rollback Strategy** sections to append
            to the existing draft above.

            ### Migration Plan Guidelines
            - Define a phased rollout (Phase 1, 2, 3 at minimum)
            - For each phase: scope, duration estimate, success criteria, dependencies
            - Address data migration if applicable
            - Specify feature flags or gradual rollout mechanisms
            - Include a timeline estimate (relative, not absolute dates)

            ### Rollback Strategy Guidelines
            - Describe how to revert each phase if something goes wrong
            - Identify point-of-no-return milestones (if any)
            - Specify monitoring and alerting that must be in place
            - Define rollback decision criteria (what metrics trigger a rollback?)
            - Address data rollback separately from code rollback

            ## Output
            Produce ONLY the two new sections (Migration Plan and Rollback Strategy) as
            your response. Do NOT repeat the existing draft content — only output the new
            sections to be appended. Aim for 400-600 words across both sections.
            """;
    }

    /// <summary>
    /// Builds the prompt for extracting metadata from the completed draft.
    /// </summary>
    internal static string BuildMetadata(
        RfcAssignment assignment,
        StageExecutionContext context)
    {
        var currentDraft = PromptHelpers.ReadWorkspaceFile(context, assignment.DraftPath);

        return $$"""
            You are an RFC editor extracting metadata from a completed draft.

            ## Draft Content
            {{currentDraft}}

            ## Your Task
            Based on the draft content above, produce a JSON metadata block as your response.

            The JSON must have this exact structure:
            ```json
            {
              "title": "<RFC title derived from the draft>",
              "summary": "<One paragraph executive summary, 2-3 sentences max>",
              "status": "Draft",
              "authors": ["AI Pipeline"]
            }
            ```

            The title should be descriptive and specific (not just the feature name).
            The summary should capture the problem AND proposed solution in brief.

            Produce ONLY the JSON as your response — no surrounding text or markdown fences.
            """;
    }
}
