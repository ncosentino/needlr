namespace RfcPipelineApp.Core.Prompts;

/// <summary>
/// Prompt templates for the cold-reader critique-and-revise stage.
/// </summary>
internal static class ColdReaderPrompts
{
    /// <summary>
    /// Builds the critic prompt for a fresh-eyes evaluation of the RFC.
    /// The critic has not seen any prior stages — it evaluates the document cold.
    /// </summary>
    internal static string BuildCritic(RfcAssignment assignment)
    {
        return $"""
            You are a senior engineer reading this RFC for the first time — a "cold reader"
            who has no prior context about the discussions that led to this document.

            ## Context
            Read the RFC draft at `{assignment.DraftPath}`.

            ## Your Task
            Evaluate the RFC purely on what is written. You are checking whether a reader
            with no prior context can understand and evaluate the proposal.

            ## Evaluation Criteria
            1. **Self-contained** — Can you understand the problem and solution without
               external context? Are acronyms defined? Are assumptions stated?
            2. **Actionable** — Could an engineer start implementing from this document?
               Are the technical details sufficient?
            3. **Persuasive** — Does the RFC make a compelling case for the proposed approach?
               Are alternatives fairly evaluated?
            4. **Complete** — Are all required sections present and substantive?
               (Problem Statement, Background, Proposed Solution, Technical Design,
               Alternatives, Trade-offs, Migration Plan, Rollback Strategy)
            5. **Well-written** — Is the prose clear, concise, and free of jargon?

            ## Output Format
            Respond with your evaluation in this exact format:

            If the RFC passes all criteria:
            ```
            APPROVED

            The RFC is well-structured and ready for team review.
            [Brief explanation of strengths]
            ```

            If the RFC needs revision:
            ```
            NEEDS_REVISION

            ## Issues Found
            1. [Specific issue with section reference]
            2. [Another issue]

            ## Suggested Improvements
            - [Concrete suggestion]
            ```

            Read the draft and provide your evaluation now.
            """;
    }

    /// <summary>
    /// Builds the reviser prompt incorporating cold-reader feedback.
    /// </summary>
    internal static string BuildReviser(RfcAssignment assignment, string feedback)
    {
        return $"""
            You are an RFC author making final revisions based on cold-reader feedback.

            ## Context
            - Read the current draft at `{assignment.DraftPath}`
            - The cold reader provided this feedback:

            ---
            {feedback}
            ---

            ## Your Task
            Revise the RFC to address the cold reader's feedback. Focus on:
            1. Clarifying any sections the reader found confusing
            2. Adding missing context or definitions
            3. Strengthening weak arguments
            4. Filling any gaps identified

            ## Rules
            - Write the complete updated document to `{assignment.DraftPath}`
            - Preserve the overall structure
            - Do not add defensive caveats — fix the underlying issue instead
            - Every change should make the document more self-contained and actionable

            Read the draft and write the revised version now.
            """;
    }
}
