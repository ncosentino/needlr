namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Thread-safe identity and direct-publication state for one experiment run instance.
/// </summary>
[DoNotAutoRegister]
internal sealed class LangfuseExperimentRunState
{
    private readonly object _gate = new();
    private LangfuseDatasetRunIdentityStatus _identityStatus;
    private string? _datasetRunId;
    private int _operationsInFlight;
    private int _linksLinked;
    private int _linksFailed;
    private int _linksInconsistent;
    private int _linksNotSampled;
    private int _linksDisabled;
    private int _scoresAccepted;
    private int _scoresFailed;
    private int _scoresNotAttempted;
    private int _scoresSkipped;
    private int _scoresDisabled;

    public LangfuseExperimentRunState(bool disabled)
    {
        _identityStatus = disabled
            ? LangfuseDatasetRunIdentityStatus.Disabled
            : LangfuseDatasetRunIdentityStatus.Unresolved;
    }

    public string? DatasetRunId
    {
        get
        {
            lock (_gate)
            {
                return _identityStatus is LangfuseDatasetRunIdentityStatus.Resolved
                    ? _datasetRunId
                    : null;
            }
        }
    }

    public LangfuseDatasetRunIdentityStatus IdentityStatus
    {
        get
        {
            lock (_gate)
            {
                return _identityStatus;
            }
        }
    }

    public void BeginOperation()
    {
        lock (_gate)
        {
            _operationsInFlight++;
        }
    }

    public void EndOperation()
    {
        lock (_gate)
        {
            _operationsInFlight--;
        }
    }

    public LangfuseExperimentItemLinkStatus ObserveDatasetRunId(string datasetRunId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetRunId);

        lock (_gate)
        {
            if (_identityStatus is LangfuseDatasetRunIdentityStatus.Inconsistent)
            {
                return LangfuseExperimentItemLinkStatus.Inconsistent;
            }

            if (_identityStatus is LangfuseDatasetRunIdentityStatus.Unresolved)
            {
                _datasetRunId = datasetRunId;
                _identityStatus = LangfuseDatasetRunIdentityStatus.Resolved;
                return LangfuseExperimentItemLinkStatus.Linked;
            }

            if (string.Equals(_datasetRunId, datasetRunId, StringComparison.Ordinal))
            {
                return LangfuseExperimentItemLinkStatus.Linked;
            }

            _datasetRunId = null;
            _identityStatus = LangfuseDatasetRunIdentityStatus.Inconsistent;
            return LangfuseExperimentItemLinkStatus.Inconsistent;
        }
    }

    public void RecordItemLink(LangfuseExperimentItemLinkStatus status)
    {
        lock (_gate)
        {
            switch (status)
            {
                case LangfuseExperimentItemLinkStatus.Linked:
                    _linksLinked++;
                    break;
                case LangfuseExperimentItemLinkStatus.Failed:
                    _linksFailed++;
                    break;
                case LangfuseExperimentItemLinkStatus.Inconsistent:
                    _linksInconsistent++;
                    break;
                case LangfuseExperimentItemLinkStatus.NotSampled:
                    _linksNotSampled++;
                    break;
                case LangfuseExperimentItemLinkStatus.Disabled:
                    _linksDisabled++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, "The experiment item link status is not defined.");
            }
        }
    }

    public void RecordRunScore(LangfuseExperimentScoreStatus status)
    {
        lock (_gate)
        {
            switch (status)
            {
                case LangfuseExperimentScoreStatus.Accepted:
                    _scoresAccepted++;
                    break;
                case LangfuseExperimentScoreStatus.Failed:
                    _scoresFailed++;
                    break;
                case LangfuseExperimentScoreStatus.NotAttempted:
                    _scoresNotAttempted++;
                    break;
                case LangfuseExperimentScoreStatus.Skipped:
                    _scoresSkipped++;
                    break;
                case LangfuseExperimentScoreStatus.Disabled:
                    _scoresDisabled++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, "The experiment run score status is not defined.");
            }
        }
    }

    public LangfuseExperimentRunPublicationSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var itemLinks = new LangfuseExperimentItemLinkCounts(
                _linksLinked,
                _linksFailed,
                _linksInconsistent,
                _linksNotSampled,
                _linksDisabled);
            var runScores = new LangfuseExperimentScoreCounts(
                _scoresAccepted,
                _scoresFailed,
                _scoresNotAttempted,
                _scoresSkipped,
                _scoresDisabled);
            return new LangfuseExperimentRunPublicationSnapshot(
                _identityStatus,
                _identityStatus is LangfuseDatasetRunIdentityStatus.Resolved ? _datasetRunId : null,
                _operationsInFlight,
                itemLinks,
                runScores,
                GetApiPublicationStatus(itemLinks, runScores));
        }
    }

    private LangfuseExperimentApiPublicationStatus GetApiPublicationStatus(
        LangfuseExperimentItemLinkCounts itemLinks,
        LangfuseExperimentScoreCounts runScores)
    {
        if (_identityStatus is LangfuseDatasetRunIdentityStatus.Disabled)
        {
            return LangfuseExperimentApiPublicationStatus.Disabled;
        }

        if (_operationsInFlight > 0)
        {
            return LangfuseExperimentApiPublicationStatus.InProgress;
        }

        if (itemLinks.Total + runScores.Total == 0)
        {
            return LangfuseExperimentApiPublicationStatus.NotAttempted;
        }

        var incomplete =
            itemLinks.Failed +
            itemLinks.Inconsistent +
            itemLinks.NotSampled +
            runScores.Failed +
            runScores.NotAttempted;
        if (incomplete == 0)
        {
            return LangfuseExperimentApiPublicationStatus.Complete;
        }

        var completed =
            itemLinks.Linked +
            runScores.Accepted +
            runScores.Skipped;
        return completed > 0
            ? LangfuseExperimentApiPublicationStatus.Partial
            : LangfuseExperimentApiPublicationStatus.Failed;
    }
}
