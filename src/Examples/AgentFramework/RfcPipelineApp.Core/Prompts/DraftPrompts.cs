namespace RfcPipelineApp.Core.Prompts;

/// <summary>
/// Prompt templates for the RFC drafting pipeline stages.
/// </summary>
internal static class DraftPrompts
{
    /// <summary>
    /// Builds the prompt for proposing the RFC document outline.
    /// </summary>
    internal static string BuildOutline(RfcAssignment assignment)
    {
        return $"""
            You are a senior architect designing the structure of an RFC document.

            ## Context
            Read the research brief at `{assignment.ResearchPath}` for background.
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
            Write the outline to `{assignment.OutlinePath}` as a markdown document with
            H2 headers for each section and bullet points for sub-topics.

            Read the research brief and write the outline now.
            """;
    }

    /// <summary>
    /// Builds the prompt for drafting the Problem Statement and Background sections.
    /// </summary>
    internal static string BuildProblemStatement(RfcAssignment assignment)
    {
        return $"""
            You are an RFC author writing the foundational sections of a design document.

            ## Context
            - Read the research brief at `{assignment.ResearchPath}`
            - Read the outline at `{assignment.OutlinePath}`
            - The RFC draft will be written to `{assignment.DraftPath}`

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
            Write a markdown document to `{assignment.DraftPath}` starting with:
            ```
            # RFC: {assignment.FeatureTitle}

            ## Problem Statement
            ...

            ## Background
            ...
            ```

            Be specific, use concrete examples, and write for the {assignment.TargetAudience}.
            Aim for 500-800 words across both sections.

            Read the research and outline, then write the draft now.
            """;
    }

    /// <summary>
    /// Builds the prompt for drafting the Proposed Solution and Technical Design sections.
    /// </summary>
    internal static string BuildProposedSolution(RfcAssignment assignment)
    {
        var constraints = string.Join("\n- ", assignment.Constraints);

        return $"""
            You are an RFC author writing the core technical proposal.

            ## Context
            - Read the current draft at `{assignment.DraftPath}`
            - Read the research brief at `{assignment.ResearchPath}`
            - Read the outline at `{assignment.OutlinePath}`

            ## Constraints That Must Be Satisfied
            - {constraints}

            ## Your Task
            Append the **Proposed Solution** and **Technical Design** sections to the
            existing draft at `{assignment.DraftPath}`.

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
            Read the current draft, then append these two sections to it. Preserve all
            existing content — append only. Aim for 600-1000 words across both sections.

            Read the files and write the updated draft now.
            """;
    }

    /// <summary>
    /// Builds the prompt for drafting the Alternatives and Trade-offs sections.
    /// </summary>
    internal static string BuildAlternatives(RfcAssignment assignment)
    {
        return $"""
            You are an RFC author providing a balanced analysis of alternatives.

            ## Context
            - Read the current draft at `{assignment.DraftPath}`
            - Read the research brief at `{assignment.ResearchPath}`

            ## Your Task
            Append the **Alternatives Considered** and **Trade-offs** sections to the
            existing draft at `{assignment.DraftPath}`.

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
            Read the current draft, then append these two sections. Preserve all existing
            content. Aim for 400-700 words across both sections.

            Read the draft and write the updated version now.
            """;
    }

    /// <summary>
    /// Builds the prompt for drafting the Migration Plan and Rollback Strategy sections.
    /// </summary>
    internal static string BuildMigration(RfcAssignment assignment)
    {
        return $"""
            You are an RFC author writing the operational sections of the design document.

            ## Context
            - Read the current draft at `{assignment.DraftPath}`

            ## Your Task
            Append the **Migration Plan** and **Rollback Strategy** sections to the
            existing draft at `{assignment.DraftPath}`.

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
            Read the current draft, then append these two sections. Preserve all existing
            content. Aim for 400-600 words across both sections.

            Read the draft and write the updated version now.
            """;
    }

    /// <summary>
    /// Builds the prompt for extracting metadata from the completed draft.
    /// </summary>
    internal static string BuildMetadata(RfcAssignment assignment)
    {
        return $$"""
            You are an RFC editor extracting metadata from a completed draft.

            ## Context
            Read the current draft at `{{assignment.DraftPath}}`.

            ## Your Task
            Based on the draft content, produce a JSON metadata block and write it to
            `metadata.json` in the workspace.

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

            Read the draft and write `metadata.json` now.
            """;
    }
}
