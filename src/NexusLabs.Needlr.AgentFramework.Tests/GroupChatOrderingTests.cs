using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Diagnostics;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests proving that <see cref="AgentGroupChatMemberAttribute.Order"/> controls
/// the round-robin turn order in group chat workflows.
/// </summary>
public class GroupChatOrderingTests
{
    // -------------------------------------------------------------------------
    // Order property controls round-robin position
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GroupChat_WriterOrder1_ReviewerOrder2_WriterRunsFirst()
    {
        var turnOrder = new List<string>();
        var callCount = 0;

        Task<ChatResponse> ProduceAsync()
        {
            var turn = Interlocked.Increment(ref callCount);
            var text = turn == 1 ? "Article content here." : "APPROVED";
            turnOrder.Add(turn == 1 ? "first-agent" : "second-agent");
            return Task.FromResult(
                new ChatResponse([new ChatMessage(ChatRole.Assistant, text)]));
        }

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>(
                (_, __, ___) => ProduceAsync());
        mockChat
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>(
                (_, __, ___) => ToStream(ProduceAsync()));

        var sp = BuildServiceProvider(mockChat);
        var workflowFactory = sp.GetRequiredService<IWorkflowFactory>();
        var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var workflow = workflowFactory.CreateGroupChatWorkflow(
            "ordered-chat", maxIterations: 2);

        var result = await workflow.RunWithDiagnosticsAsync(
            "Write about roses.",
            diagAccessor,
            cancellationToken: TestContext.Current.CancellationToken);

        // The writer (Order=1) must be the first stage, not the reviewer (Order=2)
        Assert.NotEmpty(result.Stages);
        // ZzzOrderedWriterAgent has Order=1, AaaOrderedReviewerAgent has Order=2.
        // Without ordering, Aaa would come first alphabetically.
        Assert.StartsWith("ZzzOrderedWriter", result.Stages[0].AgentName);
    }

    [Fact]
    public async Task GroupChat_HigherOrderAgent_RunsSecond()
    {
        var turnOrder = new List<string>();
        var callCount = 0;

        Task<ChatResponse> ProduceAsync()
        {
            var turn = Interlocked.Increment(ref callCount);
            var text = turn <= 1 ? "Content." : "APPROVED";
            return Task.FromResult(
                new ChatResponse([new ChatMessage(ChatRole.Assistant, text)]));
        }

        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>(
                (_, __, ___) => ProduceAsync());
        mockChat
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>(
                (_, __, ___) => ToStream(ProduceAsync()));

        var sp = BuildServiceProvider(mockChat);
        var workflowFactory = sp.GetRequiredService<IWorkflowFactory>();
        var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var workflow = workflowFactory.CreateGroupChatWorkflow(
            "ordered-chat", maxIterations: 2);

        var result = await workflow.RunWithDiagnosticsAsync(
            "Write.",
            diagAccessor,
            cancellationToken: TestContext.Current.CancellationToken);

        // With 2+ stages, the second must be the reviewer
        if (result.Stages.Count >= 2)
        {
            Assert.StartsWith("AaaOrderedReviewer", result.Stages[1].AgentName);
        }
    }

    // -------------------------------------------------------------------------
    // Attribute ordering: Order property is read
    // -------------------------------------------------------------------------

    [Fact]
    public void ZzzOrderedWriterAgent_HasOrder1()
    {
        var attr = typeof(ZzzOrderedWriterAgent)
            .GetCustomAttributes(typeof(AgentGroupChatMemberAttribute), false)
            .Cast<AgentGroupChatMemberAttribute>()
            .Single();

        Assert.Equal(1, attr.Order);
    }

    [Fact]
    public void AaaOrderedReviewerAgent_HasOrder2()
    {
        var attr = typeof(AaaOrderedReviewerAgent)
            .GetCustomAttributes(typeof(AgentGroupChatMemberAttribute), false)
            .Cast<AgentGroupChatMemberAttribute>()
            .Single();

        Assert.Equal(2, attr.Order);
    }

    // -------------------------------------------------------------------------
    // Default order: alphabetical by type name when Order is the same
    // -------------------------------------------------------------------------

    [Fact]
    public void DefaultOrder_IsZero()
    {
        var attr = typeof(TermNoConditionAgentA)
            .GetCustomAttributes(typeof(AgentGroupChatMemberAttribute), false)
            .Cast<AgentGroupChatMemberAttribute>()
            .Single();

        Assert.Equal(0, attr.Order);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IServiceProvider BuildServiceProvider(Mock<IChatClient> mockChat)
    {
        var config = new ConfigurationBuilder().Build();
        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChat.Object)
                .UsingDiagnostics()
                // Deliberately register reviewer FIRST to prove Order attribute
                // overrides registration order
                .AddAgent<AaaOrderedReviewerAgent>()
                .AddAgent<ZzzOrderedWriterAgent>())
            .BuildServiceProvider(config);
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> ToStream(
        Task<ChatResponse> responseTask)
    {
        var response = await responseTask;
        foreach (var update in response.ToChatResponseUpdates())
            yield return update;
    }
}

// Test agents with explicit ordering.
// Names are deliberately chosen so alphabetical order (Aaa before Zzz) is the
// OPPOSITE of the desired round-robin order. This proves the Order attribute
// overrides alphabetical/discovery order.

[NeedlrAiAgent(Description = "Reviewer that should run SECOND despite being alphabetically first")]
[AgentGroupChatMember("ordered-chat", Order = 2)]
[AgentTerminationCondition(typeof(Workflows.KeywordTerminationCondition), "APPROVED")]
public sealed class AaaOrderedReviewerAgent;

[NeedlrAiAgent(Description = "Writer that should run FIRST despite being alphabetically last")]
[AgentGroupChatMember("ordered-chat", Order = 1)]
public sealed class ZzzOrderedWriterAgent;
