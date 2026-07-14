namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Associates an item status with its observed count.
/// </summary>
public sealed class ExperimentItemStatusCount
{
    /// <summary>
    /// Initializes a status count.
    /// </summary>
    /// <param name="status">The item status.</param>
    /// <param name="count">The observed non-negative count.</param>
    public ExperimentItemStatusCount(ExperimentItemStatus status, int count)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The experiment item status is not defined.");
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                "An item status count cannot be negative.");
        }

        Status = status;
        Count = count;
    }

    /// <summary>Gets the item status.</summary>
    public ExperimentItemStatus Status { get; }

    /// <summary>Gets the observed count.</summary>
    public int Count { get; }
}
