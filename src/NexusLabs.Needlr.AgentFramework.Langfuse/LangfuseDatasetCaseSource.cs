using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Materializes one hosted Langfuse dataset as a finite provider-neutral experiment case source.
/// </summary>
/// <typeparam name="TCase">The caller-owned case value type.</typeparam>
/// <remarks>
/// The mapper controls the case value, trial count, and tags. It must preserve each hosted dataset
/// item id as the corresponding <see cref="ExperimentCase{TCase}.Id"/> so later Langfuse trial
/// scopes can link the trace to the correct hosted item.
/// </remarks>
[DoNotAutoRegister]
public sealed class LangfuseDatasetCaseSource<TCase> : IExperimentCaseSource<TCase>
{
    private readonly ILangfuseDatasetClient _datasetClient;
    private readonly LangfuseDatasetItemMapper<TCase> _mapper;

    /// <summary>
    /// Initializes a hosted Langfuse dataset case source.
    /// </summary>
    /// <param name="datasetClient">The existing Langfuse dataset client.</param>
    /// <param name="selection">The hosted dataset name and optional version timestamp.</param>
    /// <param name="mapper">Maps each hosted item to one experiment case.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The dataset name is empty or whitespace.</exception>
    public LangfuseDatasetCaseSource(
        ILangfuseDatasetClient datasetClient,
        LangfuseDatasetSelection selection,
        LangfuseDatasetItemMapper<TCase> mapper)
    {
        ArgumentNullException.ThrowIfNull(datasetClient);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(mapper);
        selection.Validate();

        _datasetClient = datasetClient;
        _mapper = mapper;
        Selection = selection;
    }

    /// <summary>Gets the hosted dataset selection used by this source.</summary>
    public LangfuseDatasetSelection Selection { get; }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Langfuse is disabled or the hosted dataset contains no active items.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The mapper returns an invalid, duplicate, or provider-identity-breaking case id.
    /// </exception>
    /// <exception cref="LangfuseException">The provider request or response is invalid or inconsistent.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public async ValueTask<ExperimentCaseSourceResult<TCase>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_datasetClient.IsEnabled)
        {
            throw new InvalidOperationException(
                $"Langfuse dataset '{Selection.Name}' cannot be loaded because Langfuse is not configured.");
        }

        var snapshot = await _datasetClient
            .GetDatasetAsync(Selection, cancellationToken)
            .ConfigureAwait(false);
        if (snapshot.Items.Count == 0)
        {
            throw new InvalidOperationException(
                $"Langfuse dataset '{snapshot.Dataset.Name}' contains no active items.");
        }

        var mappedItems = new (LangfuseDatasetItemSnapshot Item, ExperimentCase<TCase> Case)[snapshot.Items.Count];
        for (var index = 0; index < snapshot.Items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = snapshot.Items[index];
            var mappedCase = _mapper(item)
                ?? throw new ArgumentException(
                    $"Langfuse dataset item '{item.Id}' mapped to a null experiment case.",
                    "mapper");
            ArgumentException.ThrowIfNullOrWhiteSpace(mappedCase.Id);
            mappedItems[index] = (item, mappedCase);
        }

        var caseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mappedItem in mappedItems)
        {
            if (!caseIds.Add(mappedItem.Case.Id))
            {
                throw new ArgumentException(
                    $"Langfuse dataset '{snapshot.Dataset.Name}' mapped multiple items to experiment case id '{mappedItem.Case.Id}'.",
                    "mapper");
            }
        }

        foreach (var mappedItem in mappedItems)
        {
            if (!string.Equals(mappedItem.Item.Id, mappedItem.Case.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Langfuse dataset item '{mappedItem.Item.Id}' must map to the same experiment case id so later trial scopes can bind the hosted item.",
                    "mapper");
            }
        }

        return new ExperimentCaseSourceResult<TCase>
        {
            Source = new ExperimentSourceReference
            {
                Name = snapshot.Dataset.Name,
                Id = snapshot.Dataset.Id,
                Version = Selection.GetVersionText(),
            },
            Cases = Array.AsReadOnly(mappedItems.Select(item => item.Case).ToArray()),
        };
    }
}
