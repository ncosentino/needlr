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
/// Tests verifying that the MAF agent path (agent.RunAsync / agent.RunStreamingAsync)
/// does not produce duplicate <see cref="ChatCompletionDiagnostics"/> entries when
/// <c>UsingDiagnostics()</c> is active.
/// </summary>
public sealed class MafAgentDiagnosticsDeduplicationTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // F1: Single ChatCompletion per LLM call in non-streaming path
    [Fact]
    public async Task MAF_AgentRunAsync_WithDiagnostics_SingleChatCompletionPerCall()
    {
        var mockChat = CreateMockChatWithTokens("Response", inputTokens: 100, outputTokens: 50);
        var sp = BuildServiceProvider(mockChat, useDiagnostics: true);

        var factory = sp.GetRequiredService<IAgentFactory>();
        var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var agent = factory.CreateAgent(opts =>
        {
            opts.Name = "F1Agent";
            opts.Instructions = "Respond.";
        });

        using (diagAccessor.BeginCapture())
        {
            await agent.RunAsync("Hello", cancellationToken: _ct);

            var diag = diagAccessor.LastRunDiagnostics;

            Assert.NotNull(diag);
            Assert.Single(diag!.ChatCompletions);
        }
    }

    // F2: Token counts match unique ChatCompletions
    [Fact]
    public async Task MAF_AgentRunAsync_WithDiagnostics_TokenCountsMatchUnique()
    {
        var mockChat = CreateMockChatWithTokens("Response", inputTokens: 200, outputTokens: 100);
        var sp = BuildServiceProvider(mockChat, useDiagnostics: true);

        var factory = sp.GetRequiredService<IAgentFactory>();
        var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var agent = factory.CreateAgent(opts =>
        {
            opts.Name = "F2Agent";
            opts.Instructions = "Respond.";
        });

        using (diagAccessor.BeginCapture())
        {
            await agent.RunAsync("Hello", cancellationToken: _ct);

            var diag = diagAccessor.LastRunDiagnostics!;

            var expectedTotal = diag.ChatCompletions.Sum(c => c.Tokens.TotalTokens);
            Assert.Equal(expectedTotal, diag.AggregateTokenUsage.TotalTokens);
        }
    }

    // F3: Multiple LLM calls in MAF agent — chat completions not duplicated
    [Fact]
    public async Task MAF_AgentRunAsync_MultipleResponses_ChatCompletionsNotDuplicated()
    {
        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref callCount);
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, $"response-{callCount}")])
                {
                    ModelId = "test",
                    Usage = new UsageDetails
                    {
                        InputTokenCount = 100,
                        OutputTokenCount = 50,
                        TotalTokenCount = 150,
                    },
                };
            });

        var sp = BuildServiceProvider(mockChat, useDiagnostics: true);
        var factory = sp.GetRequiredService<IAgentFactory>();
        var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var agent = factory.CreateAgent(opts =>
        {
            opts.Name = "F3Agent";
            opts.Instructions = "Respond.";
        });

        using (diagAccessor.BeginCapture())
        {
            await agent.RunAsync("Get the data", cancellationToken: _ct);

            var diag = diagAccessor.LastRunDiagnostics;

            Assert.NotNull(diag);
            // Single LLM call should produce exactly 1 completion, not 2
            Assert.Single(diag!.ChatCompletions);

            var expectedTotal = diag.ChatCompletions.Sum(c => c.Tokens.TotalTokens);
            Assert.Equal(expectedTotal, diag.AggregateTokenUsage.TotalTokens);
        }
    }

    // F4: Streaming path — no duplication
    [Fact]
    public async Task MAF_AgentRunStreamingAsync_WithDiagnostics_SingleChatCompletionPerCall()
    {
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateStreamingResponse("Streamed response"));

        // Non-streaming fallback (in case the agent calls GetResponseAsync)
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "fallback")])
            {
                ModelId = "test-model",
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 20, TotalTokenCount = 30 },
            });

        var sp = BuildServiceProvider(mockChat, useDiagnostics: true);
        var factory = sp.GetRequiredService<IAgentFactory>();
        var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var agent = factory.CreateAgent(opts =>
        {
            opts.Name = "F4Agent";
            opts.Instructions = "Respond.";
        });

        using (diagAccessor.BeginCapture())
        {
            var updates = new List<Microsoft.Agents.AI.AgentResponseUpdate>();
            await foreach (var update in agent.RunStreamingAsync("Hello", cancellationToken: _ct))
            {
                updates.Add(update);
            }

            var diag = diagAccessor.LastRunDiagnostics;

            Assert.NotNull(diag);
            // Should be exactly 1 completion, not 2
            Assert.Single(diag!.ChatCompletions);
        }
    }

    // F5: Multiple LLM calls — all sequences unique
    [Fact]
    public async Task MAF_AgentRunAsync_MultipleCalls_AllSequencesUnique()
    {
        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var n = Interlocked.Increment(ref callCount);
                if (n <= 2)
                {
                    return new ChatResponse(
                    [
                        new ChatMessage(ChatRole.Assistant,
                        [
                            new FunctionCallContent($"call-{n}", "Step",
                                new Dictionary<string, object?> { ["step"] = n.ToString() })
                        ])
                    ])
                    {
                        ModelId = "test",
                        Usage = new UsageDetails
                        {
                            InputTokenCount = 100 * n,
                            OutputTokenCount = 50 * n,
                            TotalTokenCount = 150 * n,
                        },
                    };
                }

                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "all done")])
                {
                    ModelId = "test",
                    Usage = new UsageDetails
                    {
                        InputTokenCount = 80,
                        OutputTokenCount = 40,
                        TotalTokenCount = 120,
                    },
                };
            });

        var sp = BuildServiceProvider(mockChat, useDiagnostics: true);
        var factory = sp.GetRequiredService<IAgentFactory>();
        var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var tool = AIFunctionFactory.Create(() => "step done",
            new AIFunctionFactoryOptions { Name = "Step" });

        var agent = factory.CreateAgent(opts =>
        {
            opts.Name = "F5Agent";
            opts.Instructions = "Process each step.";
        });

        using (diagAccessor.BeginCapture())
        {
            await agent.RunAsync("Process all steps", cancellationToken: _ct);

            var diag = diagAccessor.LastRunDiagnostics!;

            var sequences = diag.ChatCompletions.Select(c => c.Sequence).ToList();
            Assert.Equal(sequences.Count, sequences.Distinct().Count());
        }
    }

    // F6: Consecutive runs don't cross-contaminate
    [Fact]
    public async Task MAF_AgentRunAsync_ConsecutiveRuns_NoCrossContamination()
    {
        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var n = Interlocked.Increment(ref callCount);
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, $"response-{n}")])
                {
                    ModelId = "test",
                    Usage = new UsageDetails
                    {
                        InputTokenCount = 10 * n,
                        OutputTokenCount = 5 * n,
                        TotalTokenCount = 15 * n,
                    },
                };
            });

        var sp = BuildServiceProvider(mockChat, useDiagnostics: true);
        var factory = sp.GetRequiredService<IAgentFactory>();
        var diagAccessor = sp.GetRequiredService<IAgentDiagnosticsAccessor>();

        var agent = factory.CreateAgent(opts =>
        {
            opts.Name = "F6Agent";
            opts.Instructions = "Respond.";
        });

        // First run
        IAgentRunDiagnostics diag1;
        using (diagAccessor.BeginCapture())
        {
            await agent.RunAsync("First", cancellationToken: _ct);
            diag1 = diagAccessor.LastRunDiagnostics!;
        }

        // Second run
        IAgentRunDiagnostics diag2;
        using (diagAccessor.BeginCapture())
        {
            await agent.RunAsync("Second", cancellationToken: _ct);
            diag2 = diagAccessor.LastRunDiagnostics!;
        }

        Assert.Single(diag1.ChatCompletions);
        Assert.Single(diag2.ChatCompletions);
        Assert.NotEqual(
            diag1.AggregateTokenUsage.TotalTokens,
            diag2.AggregateTokenUsage.TotalTokens);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IServiceProvider BuildServiceProvider(
        Mock<IChatClient> mockChat,
        bool useDiagnostics)
    {
        var config = new ConfigurationBuilder().Build();

        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af =>
            {
                af = af.Configure(opts => opts.ChatClientFactory = _ => mockChat.Object);
                if (useDiagnostics)
                {
                    af = af.UsingDiagnostics();
                }

                return af;
            })
            .BuildServiceProvider(config);
    }

    private static Mock<IChatClient> CreateMockChatWithTokens(
        string text, int inputTokens, int outputTokens)
    {
        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, text)])
            {
                ModelId = "test-model",
                Usage = new UsageDetails
                {
                    InputTokenCount = inputTokens,
                    OutputTokenCount = outputTokens,
                    TotalTokenCount = inputTokens + outputTokens,
                },
            });
        return mock;
    }

    private static IAsyncEnumerable<ChatResponseUpdate> CreateStreamingResponse(string text)
    {
        return CreateStreamingResponseAsync(text);

        static async IAsyncEnumerable<ChatResponseUpdate> CreateStreamingResponseAsync(string text)
        {
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new TextContent(text)],
                ModelId = "test-model",
            };
            await Task.CompletedTask;
        }
    }
}
