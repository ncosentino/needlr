namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseModelClient"/> returned when Langfuse is not configured. All members
/// are no-ops so eval setup code runs unchanged without credentials.
/// </summary>
[DoNotAutoRegister]
internal sealed class DisabledLangfuseModelClient : ILangfuseModelClient
{
    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public Task EnsureModelPriceAsync(LangfuseModelPrice price, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
