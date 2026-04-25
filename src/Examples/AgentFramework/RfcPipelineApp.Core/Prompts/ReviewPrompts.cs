namespace RfcPipelineApp.Core.Prompts;

/// <summary>
/// Prompt templates for the technical review pipeline stage.
/// </summary>
internal static class ReviewPrompts
{
    /// <summary>
    /// Builds the prompt for the technical review agent. This stage is wrapped
    /// in a <c>ContinueOnFailureExecutor</c> so failures are advisory, not fatal.
    /// </summary>
    internal static string BuildTechnicalReview(RfcAssignment assignment)
    {
        return $"""
            You are a principal engineer conducting a rigorous technical review of an RFC.

            ## Context
            Read the RFC draft at `{assignment.DraftPath}`.

            ## Review Checklist
            Evaluate the RFC against each of these criteria:

            1. **Logical Consistency** — Does the proposed solution follow logically from
               the problem statement? Are there contradictions between sections?
            2. **Technical Feasibility** — Is the technical design realistic? Are complexity
               estimates reasonable? Are there hidden dependencies?
            3. **Completeness** — Are there gaps? Missing error handling? Unaddressed edge cases?
            4. **Constraint Satisfaction** — Does the proposal actually meet all stated constraints?
            5. **Risk Assessment** — Are the trade-offs honestly assessed? Are rollback plans realistic?
            6. **Clarity** — Could the {assignment.TargetAudience} implement this based on the RFC alone?

            ## Output
            Write your review findings to `review-findings.md` in the workspace.

            Structure your review as:
            ```
            ## Technical Review Findings

            ### Critical Issues
            - (issues that must be fixed before approval)

            ### Recommendations
            - (improvements that would strengthen the RFC)

            ### Strengths
            - (what the RFC does well)

            ### Verdict
            NEEDS_REVISION | APPROVED
            ```

            Be specific — reference section names and quote problematic text.
            If the RFC is solid, say so. Don't manufacture issues that don't exist.

            Read the draft and write your review now.
            """;
    }

    /// <summary>
    /// Builds the prompt for applying review feedback to the RFC draft.
    /// </summary>
    internal static string BuildApplyFeedback(RfcAssignment assignment)
    {
        return $"""
            You are an RFC author incorporating technical review feedback.

            ## Context
            - Read the current draft at `{assignment.DraftPath}`
            - Read the review findings at `review-findings.md`

            ## Your Task
            Apply the feedback from the technical review to improve the RFC draft.

            ### Rules
            1. Address every **Critical Issue** listed in the review
            2. Incorporate **Recommendations** where they materially improve the document
            3. Preserve the document structure — do not reorganize sections
            4. Do not remove content unless the review specifically flags it as incorrect
            5. If a recommendation conflicts with a stated constraint, note the conflict
               but keep the constrained approach
            6. Add an "## Open Questions" section at the end if one doesn't exist,
               capturing any unresolved items from the review

            ## Output
            Read both files, then write the updated draft to `{assignment.DraftPath}`.
            The updated draft should be a complete document — not a diff or patch.

            Read the files and write the updated draft now.
            """;
    }
}
