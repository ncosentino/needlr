namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseSession"/> returned when Langfuse export is not configured. All
/// members are no-ops so calling code never needs to branch on whether credentials are present.
/// </summary>
internal sealed class DisabledLangfuseSession : ILangfuseSession
{
    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public int ScoresFailed => 0;

    /// <inheritdoc />
    public bool Flush(TimeSpan? timeout = null) => true;

    /// <inheritdoc />
    public ILangfuseScenario BeginScenario(
        string name,
        string? sessionId = null,
        string? userId = null,
        IEnumerable<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        new DisabledLangfuseScenario();

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
