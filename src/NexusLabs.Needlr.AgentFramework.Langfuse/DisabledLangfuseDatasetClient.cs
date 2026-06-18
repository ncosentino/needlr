namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseDatasetClient"/> returned when Langfuse is not configured. All members
/// are no-ops so eval setup code runs unchanged without credentials.
/// </summary>
internal sealed class DisabledLangfuseDatasetClient : ILangfuseDatasetClient
{
    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public Task EnsureDatasetAsync(string name, string? description = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task UpsertItemAsync(LangfuseDatasetItem item, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
