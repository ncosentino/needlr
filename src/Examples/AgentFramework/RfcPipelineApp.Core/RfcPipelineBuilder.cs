using System.Text.Json;

using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

using RfcPipelineApp.Core.Prompts;
using RfcPipelineApp.Core.Validators;

namespace RfcPipelineApp.Core;

/// <summary>
/// Constructs the 16-stage RFC pipeline. Each stage is either an
/// <see cref="AgentStageExecutor"/> (LLM-driven) or a
/// <see cref="DelegateStageExecutor"/> (programmatic gate/validator).
/// </summary>
public static class RfcPipelineBuilder
{
    /// <summary>
    /// Builds the complete list of pipeline stages for an RFC generation run.
    /// </summary>
    /// <param name="assignment">The feature request to convert into an RFC.</param>
    /// <param name="agentFactory">Factory for creating AI agents.</param>
    /// <param name="state">Typed pipeline state shared across all stages.</param>
    /// <param name="logger">Logger for advisory stage warnings.</param>
    /// <returns>An ordered list of 16 pipeline stages ready for <see cref="SequentialPipelineRunner"/>.</returns>
    public static IReadOnlyList<PipelineStage> Build(
        RfcAssignment assignment,
        IAgentFactory agentFactory,
        RfcPipelineState state,
        ILogger logger)
    {
        // Create dedicated agents for each responsibility.
        // Each agent gets focused instructions scoped to its role.
        var researchAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "Researcher";
            o.Instructions = "You are a thorough technical researcher. You produce comprehensive research as structured markdown.";
        });

        var briefAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "BriefWriter";
            o.Instructions = "You synthesize research into concise, actionable briefs.";
        });

        var outlineAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "Architect";
            o.Instructions = "You are a senior architect who designs document structures.";
        });

        var draftAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "DraftWriter";
            o.Instructions = "You are an RFC author who writes clear, technical prose.";
        });

        var metadataAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "MetadataExtractor";
            o.Instructions = "You extract structured metadata from documents and produce JSON.";
        });

        var reviewAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "TechnicalReviewer";
            o.Instructions = "You are a principal engineer conducting rigorous technical reviews.";
        });

        var feedbackAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "FeedbackApplicator";
            o.Instructions = "You incorporate review feedback into documents, producing improved drafts.";
        });

        var criticAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "ColdReader";
            o.Instructions = "You evaluate documents with fresh eyes, as if reading them for the first time.";
        });

        var reviserAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "FinalReviser";
            o.Instructions = "You make targeted revisions based on editorial feedback, producing complete revised documents.";
        });

        return
        [
            // Stage 1: Seed workspace with assignment data
            new PipelineStage(
                "SeedWorkspace",
                new DelegateStageExecutor(SeedWorkspace(assignment))),

            // Stage 2: Research the problem space
            // Wrapped in TimeoutExecutor to cap LLM research time,
            // with FallbackExecutor providing a minimal brief if research times out.
            new PipelineStage(
                "Research",
                new FallbackExecutor(
                    primary: new TimeoutExecutor(
                        new WriteToWorkspaceExecutor(
                            new AgentStageExecutor(
                                researchAgent,
                                ctx => ResearchPrompts.BuildResearch(assignment, ctx)),
                            "research-notes.md"),
                        timeout: TimeSpan.FromMinutes(3)),
                    fallback: new DelegateStageExecutor((ctx, _) =>
                    {
                        // Fallback: write minimal research notes so pipeline can continue
                        ctx.Workspace.TryWriteFile("research-notes.md",
                            $"# Research Notes (fallback)\n\nResearch timed out. " +
                            $"Proceeding with assignment context only.\n\n" +
                            $"## Feature: {assignment.FeatureTitle}\n\n{assignment.Description}");
                        return Task.CompletedTask;
                    }))),

            // Stage 3: Synthesize research into a brief
            new PipelineStage(
                "ResearchBrief",
                new WriteToWorkspaceExecutor(
                    new AgentStageExecutor(
                        briefAgent,
                        ctx => ResearchPrompts.BuildResearchBrief(assignment, ctx)),
                    assignment.ResearchPath,
                    overwrite: true)),

            // Stage 4: Gate — verify research brief exists and has content
            new PipelineStage(
                "ResearchGate",
                new DelegateStageExecutor(ResearchGate(assignment))),

            // Stage 5: Propose RFC outline
            new PipelineStage(
                "Outline",
                new WriteToWorkspaceExecutor(
                    new AgentStageExecutor(
                        outlineAgent,
                        ctx => DraftPrompts.BuildOutline(assignment, ctx)),
                    assignment.OutlinePath,
                    overwrite: true)),

            // Stage 6: Gate — verify outline exists
            new PipelineStage(
                "OutlineGate",
                new DelegateStageExecutor(OutlineGate(assignment))),

            // Stage 7: Draft Problem Statement + Background
            new PipelineStage(
                "DraftProblemStatement",
                new WriteToWorkspaceExecutor(
                    new AgentStageExecutor(
                        draftAgent,
                        ctx => DraftPrompts.BuildProblemStatement(assignment, ctx)),
                    assignment.DraftPath,
                    overwrite: true)),

            // Stage 8: Draft Proposed Solution + Technical Design
            new PipelineStage(
                "DraftProposedSolution",
                new WriteToWorkspaceExecutor(
                    new AgentStageExecutor(
                        draftAgent,
                        ctx => DraftPrompts.BuildProposedSolution(assignment, ctx)),
                    assignment.DraftPath)),

            // Stage 9: Draft Alternatives + Trade-offs
            new PipelineStage(
                "DraftAlternatives",
                new WriteToWorkspaceExecutor(
                    new AgentStageExecutor(
                        draftAgent,
                        ctx => DraftPrompts.BuildAlternatives(assignment, ctx)),
                    assignment.DraftPath)),

            // Stage 10: Draft Migration Plan + Rollback Strategy
            new PipelineStage(
                "DraftMigration",
                new WriteToWorkspaceExecutor(
                    new AgentStageExecutor(
                        draftAgent,
                        ctx => DraftPrompts.BuildMigration(assignment, ctx)),
                    assignment.DraftPath)),

            // Stage 11: Extract metadata from draft
            new PipelineStage(
                "DraftMetadata",
                new WriteToWorkspaceExecutor(
                    new AgentStageExecutor(
                        metadataAgent,
                        ctx => DraftPrompts.BuildMetadata(assignment, ctx)),
                    "metadata.json",
                    overwrite: true)),

            // Stage 12: Validate structure — required sections + word count
            // AfterExecution records pass/fail in typed pipeline state
            new PipelineStage(
                "ValidateStructure",
                new DelegateStageExecutor(ValidateStructure(assignment)),
                new StageExecutionPolicy
                {
                    AfterExecution = (result, ctx) =>
                    {
                        var s = ctx.GetRequiredState<RfcPipelineState>();
                        s.StructureValidationPassed = result.Succeeded;
                        return Task.CompletedTask;
                    },
                }),

            // Stage 13: Technical review (advisory — ContinueOnFailureExecutor
            // returns ContinueAdvisory disposition so pipeline continues)
            new PipelineStage(
                "TechnicalReview",
                new ContinueOnFailureExecutor(
                    new WriteToWorkspaceExecutor(
                        new AgentStageExecutor(
                            reviewAgent,
                            ctx => ReviewPrompts.BuildTechnicalReview(assignment, ctx)),
                        "review-findings.md",
                        overwrite: true),
                    onFailure: ex => logger.LogWarning(
                        ex,
                        "Technical review stage failed (advisory) — continuing pipeline")),
                new StageExecutionPolicy
                {
                    AfterExecution = (result, ctx) =>
                    {
                        var s = ctx.GetRequiredState<RfcPipelineState>();
                        s.TechnicalReviewPassed = result.Succeeded;
                        return Task.CompletedTask;
                    },
                }),

            // Stage 14: Apply review feedback (skipped if no findings)
            new PipelineStage(
                "ApplyFeedback",
                new WriteToWorkspaceExecutor(
                    new AgentStageExecutor(
                        feedbackAgent,
                        ctx => ReviewPrompts.BuildApplyFeedback(assignment, ctx)),
                    assignment.DraftPath,
                    overwrite: true),
                new StageExecutionPolicy
                {
                    ShouldSkip = ctx =>
                    {
                        // Skip if technical review didn't pass (no findings to apply)
                        // OR if review findings file doesn't exist
                        var s = ctx.GetRequiredState<RfcPipelineState>();
                        return !s.TechnicalReviewPassed ||
                            !ctx.Workspace.FileExists("review-findings.md");
                    },
                    AfterExecution = (result, ctx) =>
                    {
                        var s = ctx.GetRequiredState<RfcPipelineState>();
                        s.AppliedFixes.Add("Applied technical review feedback");
                        return Task.CompletedTask;
                    },
                }),

            // Stage 15: Cold reader critique → revise loop (max 2 retries)
            // Uses postPassCheck to verify no [TODO] placeholders remain,
            // and onRevisionCompleted to persist reviser output to workspace.
            new PipelineStage(
                "ColdReader",
                new CritiqueAndReviseExecutor(
                    criticAgent,
                    reviserAgent,
                    criticPromptFactory: ctx => ColdReaderPrompts.BuildCritic(assignment, ctx),
                    reviserPromptFactory: (ctx, feedback) => ColdReaderPrompts.BuildReviser(assignment, ctx, feedback),
                    passCheck: (_, feedback) =>
                        feedback is not null &&
                        (feedback.Contains("APPROVED", StringComparison.OrdinalIgnoreCase) ||
                         feedback.Contains("PassArticle", StringComparison.OrdinalIgnoreCase)),
                    maxRetries: 2,
                    postPassCheck: (ctx, feedback) =>
                    {
                        // Post-pass verification: reject if draft still has placeholder text
                        var content = ctx.Workspace.TryReadFile(assignment.DraftPath);
                        if (!content.Success)
                        {
                            return false;
                        }

                        return !content.Value.Content.Contains("[TODO]", StringComparison.OrdinalIgnoreCase);
                    },
                    onRevisionCompleted: (ctx, reviserOutput) =>
                    {
                        // Persist reviser output back to workspace
                        if (!string.IsNullOrWhiteSpace(reviserOutput))
                        {
                            ctx.Workspace.TryWriteFile(assignment.DraftPath, reviserOutput);
                        }

                        var s = ctx.GetRequiredState<RfcPipelineState>();
                        s.ColdReaderAttempts++;
                        return Task.CompletedTask;
                    }),
                new StageExecutionPolicy
                {
                    AfterExecution = (result, ctx) =>
                    {
                        var s = ctx.GetRequiredState<RfcPipelineState>();
                        s.ColdReaderPassed = result.Succeeded;
                        return Task.CompletedTask;
                    },
                }),

            // Stage 16: Final verification — structure + completeness, set metadata status
            // Reads accumulated pipeline state for final decisions
            new PipelineStage(
                "FinalVerification",
                new DelegateStageExecutor(FinalVerification(assignment))),
        ];
    }

    private static Func<StageExecutionContext, CancellationToken, Task> SeedWorkspace(
        RfcAssignment assignment)
    {
        return (ctx, _) =>
        {
            var assignmentJson = JsonSerializer.Serialize(new
            {
                featureTitle = assignment.FeatureTitle,
                description = assignment.Description,
                constraints = assignment.Constraints,
                existingContext = assignment.ExistingContext,
                targetAudience = assignment.TargetAudience,
            }, new JsonSerializerOptions { WriteIndented = true });

            ctx.Workspace.TryWriteFile("assignment.json", assignmentJson);
            ctx.Workspace.TryWriteFile(assignment.DraftPath, string.Empty);
            ctx.Workspace.TryWriteFile(assignment.ResearchPath, string.Empty);
            ctx.Workspace.TryWriteFile(assignment.OutlinePath, string.Empty);

            return Task.CompletedTask;
        };
    }

    private static Func<StageExecutionContext, CancellationToken, Task> ResearchGate(
        RfcAssignment assignment)
    {
        return (ctx, _) =>
        {
            if (!ctx.Workspace.FileExists(assignment.ResearchPath))
            {
                throw new InvalidOperationException(
                    $"Research gate failed: '{assignment.ResearchPath}' does not exist.");
            }

            var result = ctx.Workspace.TryReadFile(assignment.ResearchPath);
            if (!result.Success || string.IsNullOrWhiteSpace(result.Value.Content))
            {
                throw new InvalidOperationException(
                    $"Research gate failed: '{assignment.ResearchPath}' is empty.");
            }

            return Task.CompletedTask;
        };
    }

    private static Func<StageExecutionContext, CancellationToken, Task> OutlineGate(
        RfcAssignment assignment)
    {
        return (ctx, _) =>
        {
            if (!ctx.Workspace.FileExists(assignment.OutlinePath))
            {
                throw new InvalidOperationException(
                    $"Outline gate failed: '{assignment.OutlinePath}' does not exist.");
            }

            return Task.CompletedTask;
        };
    }

    private static Func<StageExecutionContext, CancellationToken, Task> ValidateStructure(
        RfcAssignment assignment)
    {
        return (ctx, _) =>
        {
            var draftResult = ctx.Workspace.TryReadFile(assignment.DraftPath);
            if (!draftResult.Success)
            {
                throw new InvalidOperationException(
                    $"Structure validation failed: '{assignment.DraftPath}' not found.");
            }

            var error = StructureValidator.Validate(draftResult.Value.Content);
            if (error is not null)
            {
                throw new InvalidOperationException($"Structure validation failed: {error}");
            }

            return Task.CompletedTask;
        };
    }

    private static Func<StageExecutionContext, CancellationToken, Task> FinalVerification(
        RfcAssignment assignment)
    {
        return (ctx, _) =>
        {
            var state = ctx.GetRequiredState<RfcPipelineState>();
            var metadata = state.Metadata;

            var draftResult = ctx.Workspace.TryReadFile(assignment.DraftPath);
            if (!draftResult.Success)
            {
                throw new InvalidOperationException("RFC draft not found for final verification.");
            }

            var content = draftResult.Value.Content;

            var structureError = StructureValidator.Validate(content);
            if (structureError is not null)
            {
                throw new InvalidOperationException($"Final structure check failed: {structureError}");
            }

            var completenessError = CompletenessValidator.Validate(content);
            if (completenessError is not null)
            {
                throw new InvalidOperationException($"Final completeness check failed: {completenessError}");
            }

            // Populate metadata from the metadata.json written by DraftMetadata stage
            if (ctx.Workspace.FileExists("metadata.json"))
            {
                var metadataJson = ctx.Workspace.TryReadFile("metadata.json").Value.Content;
                try
                {
                    var parsed = JsonSerializer.Deserialize<JsonElement>(metadataJson);
                    metadata.Title = parsed.TryGetProperty("title", out var t)
                        ? t.GetString() ?? assignment.FeatureTitle
                        : assignment.FeatureTitle;
                    metadata.Summary = parsed.TryGetProperty("summary", out var s)
                        ? s.GetString() ?? string.Empty
                        : string.Empty;
                    metadata.Authors = parsed.TryGetProperty("authors", out var a)
                        ? a.EnumerateArray().Select(e => e.GetString() ?? "Unknown").ToList()
                        : ["AI Pipeline"];
                }
                catch (JsonException)
                {
                    metadata.Title = assignment.FeatureTitle;
                    metadata.Authors = ["AI Pipeline"];
                }
            }
            else
            {
                metadata.Title = assignment.FeatureTitle;
                metadata.Authors = ["AI Pipeline"];
            }

            metadata.Status = "Draft";

            return Task.CompletedTask;
        };
    }

}
