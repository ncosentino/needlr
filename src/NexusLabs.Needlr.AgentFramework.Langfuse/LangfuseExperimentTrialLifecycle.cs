namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Owns one scenario trace and its optional hosted dataset link across a statistical trial.
/// </summary>
[DoNotAutoRegister]
internal sealed class LangfuseExperimentTrialLifecycle : IAsyncDisposable
{
    private readonly ILangfuseActivatableScenario _scenario;
    private int _disposed;

    public LangfuseExperimentTrialLifecycle(
        ILangfuseActivatableScenario scenario,
        string? recordedTraceId,
        LangfuseExperimentItemLinkResult? link)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        _scenario = scenario;
        RecordedTraceId = recordedTraceId;
        Link = link;
    }

    public ILangfuseScenario Scenario => _scenario;

    public string? RecordedTraceId { get; }

    public LangfuseExperimentItemLinkResult? Link { get; }

    public bool IsDisabled => !_scenario.IsEnabled;

    public IDisposable? Activate()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        return _scenario.Activate();
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _scenario.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
