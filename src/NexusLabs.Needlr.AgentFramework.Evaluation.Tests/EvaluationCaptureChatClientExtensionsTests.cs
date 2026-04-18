using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class EvaluationCaptureChatClientExtensionsTests
{
    [Fact]
    public void WithEvaluationCapture_Store_NullInnerClient_Throws()
    {
        IChatClient inner = null!;
        Assert.Throws<ArgumentNullException>(
            () => inner.WithEvaluationCapture(new InMemoryEvaluationCaptureStore()));
    }

    [Fact]
    public void WithEvaluationCapture_Store_NullStore_Throws()
    {
        using var inner = new RecordingChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "x")));
        Assert.Throws<ArgumentNullException>(
            () => inner.WithEvaluationCapture((IEvaluationCaptureStore)null!));
    }

    [Fact]
    public void WithEvaluationCapture_Store_ReturnsCaptureClient()
    {
        using var inner = new RecordingChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "x")));

        var wrapped = inner.WithEvaluationCapture(new InMemoryEvaluationCaptureStore());

        Assert.IsType<EvaluationCaptureChatClient>(wrapped);
    }

    [Fact]
    public void WithEvaluationCapture_Directory_NullInnerClient_Throws()
    {
        IChatClient inner = null!;
        Assert.Throws<ArgumentNullException>(
            () => inner.WithEvaluationCapture("/tmp/x"));
    }

    [Fact]
    public void WithEvaluationCapture_Directory_NullOrWhitespace_Throws()
    {
        using var inner = new RecordingChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "x")));

        Assert.Throws<ArgumentNullException>(
            () => inner.WithEvaluationCapture(cacheDirectory: null!));
        Assert.Throws<ArgumentException>(
            () => inner.WithEvaluationCapture(cacheDirectory: ""));
        Assert.Throws<ArgumentException>(
            () => inner.WithEvaluationCapture(cacheDirectory: "   "));
    }

    [Fact]
    public void WithEvaluationCapture_Directory_ReturnsCaptureClient()
    {
        using var inner = new RecordingChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "x")));

        var wrapped = inner.WithEvaluationCapture(
            Path.Combine(Path.GetTempPath(), "needlr-ext-" + Guid.NewGuid().ToString("N")));

        Assert.IsType<EvaluationCaptureChatClient>(wrapped);
    }
}
