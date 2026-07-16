using Microsoft.Extensions.AI;

using NexusLabs.Needlr;

namespace MeaiReportingExperimentApp;

/// <summary>
/// Provides deterministic subject and judge responses for the credential-free example.
/// </summary>
[DoNotAutoRegister]
internal sealed class ReportingExampleChatClient : IChatClient
{
    private readonly ChatClientMetadata _metadata = new(
        providerName: "needlr-example",
        providerUri: new Uri("https://example.invalid"),
        defaultModelId: "deterministic-example");
    private int _callCount;

    /// <summary>Gets the number of requests that reached the uncached client.</summary>
    public int CallCount => Volatile.Read(ref _callCount);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _callCount);
        var materialized = messages.ToArray();
        var isJudgeRequest = materialized.Any(message =>
            message.Role == ChatRole.System
            && message.Text?.Contains(
                "You are an evaluation judge.",
                StringComparison.Ordinal) == true);
        var text = isJudgeRequest
            ? """
              SCORE: 5
              COMPLETED: YES
              REASONING: The deterministic response directly answers the supplied task.
              """
            : "The refund request is eligible and should be approved.";
        return Task.FromResult(new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, text)])
        {
            ModelId = _metadata.DefaultModelId,
        });
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Streaming is not used in this example.");

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType == typeof(ChatClientMetadata) ? _metadata : null;
    }

    public void Dispose()
    {
    }
}
