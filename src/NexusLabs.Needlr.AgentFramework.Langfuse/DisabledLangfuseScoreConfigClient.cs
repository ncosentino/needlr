namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseScoreConfigClient"/> returned when Langfuse is not configured. All
/// members are no-ops so eval setup code runs unchanged without credentials.
/// </summary>
[DoNotAutoRegister]
internal sealed class DisabledLangfuseScoreConfigClient : ILangfuseScoreConfigClient
{
    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public Task EnsureScoreConfigAsync(LangfuseScoreConfig config, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
