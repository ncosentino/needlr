using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Integration tests verifying that <c>RunWithDiagnosticsAsync</c> correctly
/// captures stages, durations, and token counts for group chat workflows.
/// Each test uses real MAF workflows with mock <see cref="IChatClient"/>s.
/// </summary>
public class GroupChatDiagnosticsIntegrationTests
{
    // -------------------------------------------------------------------------
    // Stage capture: all invoked agents must have stages
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GroupChat_BothAgentsRespondWithText_BothHaveStages()
    {
        var sp = BuildGroupChatServiceProvider(
            writerResponse: "Here is the article content.",
            reviewerResponse: "Looks good. APPROVED");

        var workflowFactory = sp.GetRequiredService<IWorkflowFactory>();
        var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var workflow = workflowFactory.CreateGroupChatWorkflow(
            "diag-group-chat",
            maxIterations: 2,
            (type, o) =>
            {
                if (type == typeof(DiagWriterAgent))
                    o.Instructions = "Write an article.";
                else if (type == typeof(DiagReviewerAgent))
                    o.Instructions = "Review and say APPROVED.";
            });

        var result = await workflow.RunWithDiagnosticsAsync(
            "Write about roses.",
            diagAccessor,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Stages.Count >= 2,
            $"Expected at least 2 stages, got {result.Stages.Count}. " +
            $"Agents: [{string.Join(", ", result.Stages.Select(s => s.AgentName))}]");

        var agentNames = result.Stages.Select(s => s.AgentName).ToList();
        Assert.Contains(agentNames, n => n.StartsWith("DiagWriter"));
        Assert.Contains(agentNames, n => n.StartsWith("DiagReviewer"));
    }

    // -------------------------------------------------------------------------
    // Token capture: non-zero tokens from collector fallback
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GroupChat_MockReturnsTokenUsage_StagesHaveNonZeroTokens()
    {
        var sp = BuildGroupChatServiceProvider(
            writerResponse: "Article text.",
            reviewerResponse: "APPROVED",
            inputTokens: 200,
            outputTokens: 80);

        var workflowFactory = sp.GetRequiredService<IWorkflowFactory>();
        var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var workflow = workflowFactory.CreateGroupChatWorkflow(
            "diag-group-chat",
            maxIterations: 2,
            (type, o) =>
            {
                if (type == typeof(DiagWriterAgent))
                    o.Instructions = "Write.";
                else if (type == typeof(DiagReviewerAgent))
                    o.Instructions = "Say APPROVED.";
            });

        var result = await workflow.RunWithDiagnosticsAsync(
            "Write about roses.",
            diagAccessor,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEmpty(result.Stages);

        var totalInputTokens = result.Stages
            .Where(s => s.Diagnostics is not null)
            .Sum(s => s.Diagnostics!.AggregateTokenUsage.InputTokens);
        var totalOutputTokens = result.Stages
            .Where(s => s.Diagnostics is not null)
            .Sum(s => s.Diagnostics!.AggregateTokenUsage.OutputTokens);

        Assert.True(totalInputTokens > 0,
            $"Expected non-zero total input tokens across all stages, got {totalInputTokens}");
        Assert.True(totalOutputTokens > 0,
            $"Expected non-zero total output tokens across all stages, got {totalOutputTokens}");
    }

    // -------------------------------------------------------------------------
    // Duration: stage duration must reflect actual execution time
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GroupChat_AgentWithDelay_StageDurationReflectsExecutionTime()
    {
        const int delayMs = 500;
        var sp = BuildGroupChatServiceProvider(
            writerResponse: "Content here.",
            reviewerResponse: "APPROVED",
            responseDelayMs: delayMs);

        var workflowFactory = sp.GetRequiredService<IWorkflowFactory>();
        var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var workflow = workflowFactory.CreateGroupChatWorkflow(
            "diag-group-chat",
            maxIterations: 2,
            (type, o) =>
            {
                if (type == typeof(DiagWriterAgent))
                    o.Instructions = "Write.";
                else if (type == typeof(DiagReviewerAgent))
                    o.Instructions = "Say APPROVED.";
            });

        var result = await workflow.RunWithDiagnosticsAsync(
            "Write about roses.",
            diagAccessor,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEmpty(result.Stages);

        // Each agent should have duration reflecting the mock delay
        foreach (var stage in result.Stages)
        {
            Assert.True(
                stage.Diagnostics?.TotalDuration >= TimeSpan.FromMilliseconds(delayMs * 0.5),
                $"Stage '{stage.AgentName}' duration {stage.Diagnostics?.TotalDuration} " +
                $"is too short — expected at least {delayMs * 0.5}ms to reflect the " +
                $"{delayMs}ms mock delay");
        }
    }

    // -------------------------------------------------------------------------
    // Succeeded / error propagation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GroupChat_NormalCompletion_Succeeds()
    {
        var sp = BuildGroupChatServiceProvider(
            writerResponse: "Done.",
            reviewerResponse: "APPROVED");

        var workflowFactory = sp.GetRequiredService<IWorkflowFactory>();
        var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var workflow = workflowFactory.CreateGroupChatWorkflow(
            "diag-group-chat",
            maxIterations: 2,
            (type, o) =>
            {
                if (type == typeof(DiagWriterAgent))
                    o.Instructions = "Write.";
                else if (type == typeof(DiagReviewerAgent))
                    o.Instructions = "Say APPROVED.";
            });

        var result = await workflow.RunWithDiagnosticsAsync(
            "Write.",
            diagAccessor,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, $"Expected succeeded but got error: {result.ErrorMessage}");
        Assert.True(result.TotalDuration > TimeSpan.Zero);
    }

    // -------------------------------------------------------------------------
    // Response text: stage captures what the agent said
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GroupChat_ReviewerSaysApproved_StageResponseTextContainsApproved()
    {
        var sp = BuildGroupChatServiceProvider(
            writerResponse: "Article about roses.",
            reviewerResponse: "No issues found. APPROVED");

        var workflowFactory = sp.GetRequiredService<IWorkflowFactory>();
        var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var workflow = workflowFactory.CreateGroupChatWorkflow(
            "diag-group-chat",
            maxIterations: 2,
            (type, o) =>
            {
                if (type == typeof(DiagWriterAgent))
                    o.Instructions = "Write.";
                else if (type == typeof(DiagReviewerAgent))
                    o.Instructions = "Say APPROVED.";
            });

        var result = await workflow.RunWithDiagnosticsAsync(
            "Write.",
            diagAccessor,
            cancellationToken: TestContext.Current.CancellationToken);

        var reviewerStage = result.Stages.FirstOrDefault(
            s => s.AgentName.StartsWith("DiagReviewer"));
        Assert.NotNull(reviewerStage);
        Assert.Contains("APPROVED", reviewerStage.FinalResponse?.Text ?? string.Empty);
    }

    // -------------------------------------------------------------------------
    // Per-stage token attribution
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GroupChat_TwoAgents_EachStageHasOwnTokens()
    {
        var sp = BuildGroupChatServiceProvider(
            writerResponse: "Content.",
            reviewerResponse: "APPROVED",
            inputTokens: 150,
            outputTokens: 60);

        var workflowFactory = sp.GetRequiredService<IWorkflowFactory>();
        var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var workflow = workflowFactory.CreateGroupChatWorkflow(
            "diag-group-chat",
            maxIterations: 2,
            (type, o) =>
            {
                if (type == typeof(DiagWriterAgent))
                    o.Instructions = "Write.";
                else if (type == typeof(DiagReviewerAgent))
                    o.Instructions = "Say APPROVED.";
            });

        var result = await workflow.RunWithDiagnosticsAsync(
            "Write.",
            diagAccessor,
            cancellationToken: TestContext.Current.CancellationToken);

        foreach (var stage in result.Stages)
        {
            Assert.NotNull(stage.Diagnostics);
            Assert.True(stage.Diagnostics!.AggregateTokenUsage.InputTokens > 0,
                $"Stage '{stage.AgentName}' has 0 input tokens");
            Assert.True(stage.Diagnostics.AggregateTokenUsage.OutputTokens > 0,
                $"Stage '{stage.AgentName}' has 0 output tokens");
        }
    }

    // -------------------------------------------------------------------------
    // Test agents
    // -------------------------------------------------------------------------

    [NeedlrAiAgent(Description = "Test writer for diagnostics tests")]
    [AgentGroupChatMember("diag-group-chat", Order = 1)]
    public sealed class DiagWriterAgent;

    [NeedlrAiAgent(Description = "Test reviewer for diagnostics tests")]
    [AgentGroupChatMember("diag-group-chat", Order = 2)]
    [AgentTerminationCondition(typeof(KeywordTerminationCondition), "APPROVED", "DiagReviewer")]
    public sealed class DiagReviewerAgent;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IServiceProvider BuildGroupChatServiceProvider(
        string writerResponse,
        string reviewerResponse,
        long inputTokens = 0,
        long outputTokens = 0,
        int responseDelayMs = 0)
    {
        var callCount = 0;

        async Task<ChatResponse> ProduceResponseAsync(CancellationToken ct)
        {
            if (responseDelayMs > 0)
                await Task.Delay(responseDelayMs, ct);

            var turn = Interlocked.Increment(ref callCount);
            var text = turn == 1 ? writerResponse : reviewerResponse;
            var response = new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, text)]);

            if (inputTokens > 0 || outputTokens > 0)
            {
                response.Usage = new UsageDetails
                {
                    InputTokenCount = (int)inputTokens,
                    OutputTokenCount = (int)outputTokens,
                    TotalTokenCount = (int)(inputTokens + outputTokens),
                };
            }

            return response;
        }

        static async IAsyncEnumerable<ChatResponseUpdate> ToStream(
            Task<ChatResponse> responseTask)
        {
            var response = await responseTask;
            foreach (var update in response.ToChatResponseUpdates())
                yield return update;
        }

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>(
                (_, __, ct) => ProduceResponseAsync(ct));

        mockChat
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>(
                (_, __, ct) => ToStream(ProduceResponseAsync(ct)));

        var config = new ConfigurationBuilder().Build();
        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object)
                .UsingDiagnostics()
                .AddAgent<DiagWriterAgent>()
                .AddAgent<DiagReviewerAgent>())
            .BuildServiceProvider(config);
    }
}
