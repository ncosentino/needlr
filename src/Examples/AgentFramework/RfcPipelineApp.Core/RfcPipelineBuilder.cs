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
    /// <param name="metadata">Mutable metadata populated by the metadata stage.</param>
    /// <param name="logger">Logger for advisory stage warnings.</param>
    /// <returns>An ordered list of 16 pipeline stages ready for <see cref="SequentialPipelineRunner"/>.</returns>
    public static IReadOnlyList<PipelineStage> Build(
        RfcAssignment assignment,
        IAgentFactory agentFactory,
        RfcMetadata metadata,
        ILogger logger)
    {
        // Create dedicated agents for each responsibility.
        // Each agent gets focused instructions scoped to its role.
        var researchAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "Researcher";
            o.Instructions = "You are a thorough technical researcher. You read and write workspace files to conduct research.";
        });

        var briefAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "BriefWriter";
            o.Instructions = "You synthesize research into concise, actionable briefs. You read and write workspace files.";
        });

        var outlineAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "Architect";
            o.Instructions = "You are a senior architect who designs document structures. You read and write workspace files.";
        });

        var draftAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "DraftWriter";
            o.Instructions = "You are an RFC author who writes clear, technical prose. You read and write workspace files. Always read existing content before appending.";
        });

        var metadataAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "MetadataExtractor";
            o.Instructions = "You extract structured metadata from documents. You read workspace files and write JSON.";
        });

        var reviewAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "TechnicalReviewer";
            o.Instructions = "You are a principal engineer conducting rigorous technical reviews. You read workspace files and write review findings.";
        });

        var feedbackAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "FeedbackApplicator";
            o.Instructions = "You incorporate review feedback into documents. You read review findings and the draft, then write an improved draft.";
        });

        var criticAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "ColdReader";
            o.Instructions = "You evaluate documents with fresh eyes, as if reading them for the first time. You read workspace files.";
        });

        var reviserAgent = agentFactory.CreateAgent(o =>
        {
            o.Name = "FinalReviser";
            o.Instructions = "You make targeted revisions based on editorial feedback. You read and write workspace files.";
        });

        return
        [
            // Stage 1: Seed workspace with assignment data
            new PipelineStage(
                "SeedWorkspace",
                new DelegateStageExecutor(SeedWorkspace(assignment))),

            // Stage 2: Research the problem space
            new PipelineStage(
                "Research",
                new AgentStageExecutor(
                    researchAgent,
                    _ => ResearchPrompts.BuildResearch(assignment))),

            // Stage 3: Synthesize research into a brief
            new PipelineStage(
                "ResearchBrief",
                new AgentStageExecutor(
                    briefAgent,
                    _ => ResearchPrompts.BuildResearchBrief(assignment))),

            // Stage 4: Gate — verify research brief exists and has content
            new PipelineStage(
                "ResearchGate",
                new DelegateStageExecutor(ResearchGate(assignment))),

            // Stage 5: Propose RFC outline
            new PipelineStage(
                "Outline",
                new AgentStageExecutor(
                    outlineAgent,
                    _ => DraftPrompts.BuildOutline(assignment))),

            // Stage 6: Gate — verify outline exists
            new PipelineStage(
                "OutlineGate",
                new DelegateStageExecutor(OutlineGate(assignment))),

            // Stage 7: Draft Problem Statement + Background
            new PipelineStage(
                "DraftProblemStatement",
                new AgentStageExecutor(
                    draftAgent,
                    _ => DraftPrompts.BuildProblemStatement(assignment))),

            // Stage 8: Draft Proposed Solution + Technical Design
            new PipelineStage(
                "DraftProposedSolution",
                new AgentStageExecutor(
                    draftAgent,
                    _ => DraftPrompts.BuildProposedSolution(assignment))),

            // Stage 9: Draft Alternatives + Trade-offs
            new PipelineStage(
                "DraftAlternatives",
                new AgentStageExecutor(
                    draftAgent,
                    _ => DraftPrompts.BuildAlternatives(assignment))),

            // Stage 10: Draft Migration Plan + Rollback Strategy
            new PipelineStage(
                "DraftMigration",
                new AgentStageExecutor(
                    draftAgent,
                    _ => DraftPrompts.BuildMigration(assignment))),

            // Stage 11: Extract metadata from draft
            new PipelineStage(
                "DraftMetadata",
                new AgentStageExecutor(
                    metadataAgent,
                    _ => DraftPrompts.BuildMetadata(assignment))),

            // Stage 12: Validate structure — required sections + word count
            new PipelineStage(
                "ValidateStructure",
                new DelegateStageExecutor(ValidateStructure(assignment))),

            // Stage 13: Technical review (advisory — failures don't halt pipeline)
            new PipelineStage(
                "TechnicalReview",
                new ContinueOnFailureExecutor(
                    new AgentStageExecutor(
                        reviewAgent,
                        _ => ReviewPrompts.BuildTechnicalReview(assignment)),
                    onFailure: ex => logger.LogWarning(
                        ex,
                        "Technical review stage failed (advisory) — continuing pipeline"))),

            // Stage 14: Apply review feedback
            new PipelineStage(
                "ApplyFeedback",
                new AgentStageExecutor(
                    feedbackAgent,
                    _ => ReviewPrompts.BuildApplyFeedback(assignment)),
                new StageExecutionPolicy
                {
                    ShouldSkip = ctx =>
                    {
                        // Skip if there are no review findings to apply
                        return !ctx.Workspace.FileExists("review-findings.md");
                    },
                }),

            // Stage 15: Cold reader critique → revise loop (max 2 retries)
            new PipelineStage(
                "ColdReader",
                new CritiqueAndReviseExecutor(
                    criticAgent,
                    reviserAgent,
                    criticPromptFactory: _ => ColdReaderPrompts.BuildCritic(assignment),
                    reviserPromptFactory: (_, feedback) => ColdReaderPrompts.BuildReviser(assignment, feedback),
                    passCheck: (_, feedback) =>
                        feedback is not null &&
                        (feedback.Contains("APPROVED", StringComparison.OrdinalIgnoreCase) ||
                         feedback.Contains("PassArticle", StringComparison.OrdinalIgnoreCase)),
                    maxRetries: 2)),

            // Stage 16: Final verification — structure + completeness, set metadata status
            new PipelineStage(
                "FinalVerification",
                new DelegateStageExecutor(FinalVerification(assignment, metadata))),
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
        RfcAssignment assignment,
        RfcMetadata metadata)
    {
        return (ctx, _) =>
        {
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
