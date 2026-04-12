using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentResponseExtensionsTests
{
    [Fact]
    public void GetText_SingleMessage_ReturnsText()
    {
        var response = new AgentResponse
        {
            Messages = [new ChatMessage(ChatRole.Assistant, "Hello world")]
        };

        Assert.Equal("Hello world", response.GetText());
    }

    [Fact]
    public void GetText_MultipleMessages_ConcatenatesWithNewline()
    {
        var response = new AgentResponse
        {
            Messages =
            [
                new ChatMessage(ChatRole.Assistant, "First part."),
                new ChatMessage(ChatRole.Assistant, "Second part.")
            ]
        };

        Assert.Equal("First part.\nSecond part.", response.GetText());
    }

    [Fact]
    public void GetText_SkipsEmptyMessages()
    {
        var response = new AgentResponse
        {
            Messages =
            [
                new ChatMessage(ChatRole.Assistant, "Content"),
                new ChatMessage(ChatRole.Tool, ""),
                new ChatMessage(ChatRole.Assistant, "More content")
            ]
        };

        Assert.Equal("Content\nMore content", response.GetText());
    }

    [Fact]
    public void GetText_NoMessages_ReturnsNull()
    {
        var response = new AgentResponse { Messages = [] };

        Assert.Null(response.GetText());
    }

    [Fact]
    public void GetText_NullMessages_ReturnsNull()
    {
        var response = new AgentResponse { Messages = null };

        Assert.Null(response.GetText());
    }

    [Fact]
    public void GetText_AllWhitespaceMessages_ReturnsNull()
    {
        var response = new AgentResponse
        {
            Messages =
            [
                new ChatMessage(ChatRole.Assistant, "   "),
                new ChatMessage(ChatRole.Assistant, "\n")
            ]
        };

        Assert.Null(response.GetText());
    }

    [Fact]
    public void GetText_NullResponse_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AgentResponseExtensions.GetText(null!));
    }
}
