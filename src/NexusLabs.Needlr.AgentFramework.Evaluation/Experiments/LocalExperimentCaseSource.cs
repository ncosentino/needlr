namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides a finite in-process experiment case collection.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
[DoNotAutoRegister]
public sealed class LocalExperimentCaseSource<TCase> : IExperimentCaseSource<TCase>
{
    private readonly ExperimentCase<TCase>[] _cases;
    private readonly ExperimentSourceReference _source;

    /// <summary>
    /// Initializes a local source by copying the supplied case enumeration.
    /// </summary>
    /// <param name="name">The source name recorded in the canonical result.</param>
    /// <param name="cases">The finite ordered cases.</param>
    public LocalExperimentCaseSource(
        string name,
        IEnumerable<ExperimentCase<TCase>> cases)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(cases);

        _source = new ExperimentSourceReference { Name = name };
        _cases = cases.ToArray();
    }

    /// <summary>
    /// Loads the complete local case collection without caller cancellation.
    /// </summary>
    /// <returns>The local source identity and ordered cases.</returns>
    public ValueTask<ExperimentCaseSourceResult<TCase>> LoadAsync() =>
        LoadAsync(CancellationToken.None);

    /// <summary>
    /// Loads the complete local case collection with caller cancellation.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The local source identity and ordered cases.</returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    public ValueTask<ExperimentCaseSourceResult<TCase>> LoadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ExperimentCaseSourceResult<TCase>
        {
            Source = _source,
            Cases = Array.AsReadOnly(_cases),
        });
    }
}
