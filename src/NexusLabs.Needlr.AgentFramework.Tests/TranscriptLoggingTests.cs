using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Needlr.AgentFramework.Workflows.Middleware;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class TranscriptLoggingTests
{
    private static readonly ChatResponse FixedResponse =
        new([new ChatMessage(ChatRole.Assistant, "hello back")]);

    private static Mock<IChatClient> CreateMockChatClient()
    {
        var mock = new Mock<IChatClient>();
        mock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(FixedResponse);
        return mock;
    }

    // -------------------------------------------------------------------------
    // TranscriptLoggingChatClient
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TranscriptLoggingChatClient_RecordsRequestAndResponse()
    {
        var writer = new InMemoryTranscriptWriter();
        var inner = CreateMockChatClient();
        var client = new TranscriptLoggingChatClient(inner.Object, writer);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "hi"),
        };

        var response = await client.GetResponseAsync(
            messages,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, writer.Entries.Count);
        Assert.Equal(TranscriptEntryKind.Request, writer.Entries[0].Kind);
        var requestMessages = writer.Entries[0].Messages;
        Assert.NotNull(requestMessages);
        Assert.Single(requestMessages);
        Assert.Null(writer.Entries[0].Response);

        Assert.Equal(TranscriptEntryKind.Response, writer.Entries[1].Kind);
        Assert.NotNull(writer.Entries[1].Response);
        Assert.Null(writer.Entries[1].Messages);
        Assert.Same(response, writer.Entries[1].Response);
    }

    [Fact]
    public async Task TranscriptLoggingChatClient_UsesCurrentStageName()
    {
        var writer = new InMemoryTranscriptWriter();
        var inner = CreateMockChatClient();
        var client = new TranscriptLoggingChatClient(inner.Object, writer)
        {
            CurrentStageName = "planning",
        };

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "plan")],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.All(writer.Entries, e => Assert.Equal("planning", e.StageName));
    }

    [Fact]
    public void TranscriptLoggingChatClient_DefaultStageNameIsUnknown()
    {
        var writer = new InMemoryTranscriptWriter();
        var inner = CreateMockChatClient();
        var client = new TranscriptLoggingChatClient(inner.Object, writer);

        Assert.Equal("unknown", client.CurrentStageName);
    }

    // -------------------------------------------------------------------------
    // InMemoryTranscriptWriter
    // -------------------------------------------------------------------------

    [Fact]
    public void InMemoryTranscriptWriter_StartsEmpty()
    {
        var writer = new InMemoryTranscriptWriter();

        Assert.Empty(writer.Entries);
    }

    [Fact]
    public void InMemoryTranscriptWriter_RecordsEntriesInOrder()
    {
        var writer = new InMemoryTranscriptWriter();

        writer.WriteRequest("stage-a", [new ChatMessage(ChatRole.User, "q1")], null);
        writer.WriteResponse("stage-a", FixedResponse);
        writer.WriteRequest("stage-b", [new ChatMessage(ChatRole.User, "q2")], null);
        writer.WriteResponse("stage-b", FixedResponse);

        Assert.Equal(4, writer.Entries.Count);
        Assert.Equal("stage-a", writer.Entries[0].StageName);
        Assert.Equal(TranscriptEntryKind.Request, writer.Entries[0].Kind);
        Assert.Equal("stage-a", writer.Entries[1].StageName);
        Assert.Equal(TranscriptEntryKind.Response, writer.Entries[1].Kind);
        Assert.Equal("stage-b", writer.Entries[2].StageName);
        Assert.Equal(TranscriptEntryKind.Request, writer.Entries[2].Kind);
        Assert.Equal("stage-b", writer.Entries[3].StageName);
        Assert.Equal(TranscriptEntryKind.Response, writer.Entries[3].Kind);
    }

    // -------------------------------------------------------------------------
    // ChatClientBuilderTranscriptExtensions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ChatClientBuilderExtension_WrapsInnerClient()
    {
        var writer = new InMemoryTranscriptWriter();
        var inner = CreateMockChatClient();

        var pipeline = new ChatClientBuilder(inner.Object)
            .UseTranscriptLogging(writer)
            .Build();

        await pipeline.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "test")],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, writer.Entries.Count);

        inner.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
