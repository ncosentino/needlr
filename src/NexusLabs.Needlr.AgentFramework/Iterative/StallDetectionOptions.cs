namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// Configuration for iterative loop stall detection.
/// </summary>
/// <remarks>
/// A "stall" is detected when <see cref="ConsecutiveThreshold"/> consecutive
/// iterations produce total token counts within <see cref="TolerancePercent"/>
/// of each other. This is a strong signal the LLM is repeating the same work
/// because it has no cross-iteration memory of what it already did.
/// </remarks>
public sealed class StallDetectionOptions
{
    private int _consecutiveThreshold = 3;
    private double _tolerancePercent = 0.10;

    /// <summary>
    /// The number of consecutive iterations with similar token counts before
    /// terminating. Defaults to <c>3</c>. Minimum <c>2</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Value is less than 2.</exception>
    public int ConsecutiveThreshold
    {
        get => _consecutiveThreshold;
        set
        {
            if (value < 2)
                throw new ArgumentOutOfRangeException(nameof(value), "ConsecutiveThreshold must be at least 2.");
            _consecutiveThreshold = value;
        }
    }

    /// <summary>
    /// How close token counts must be (as a fraction of the previous iteration's
    /// count) to be considered "similar." Defaults to <c>0.10</c> (10%).
    /// </summary>
    /// <remarks>
    /// A tolerance of 0.10 means iterations with 15,000 and 16,200 tokens are
    /// considered similar (8% difference), but 15,000 and 18,000 are not (20%).
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Value is negative or ≥ 1.0.</exception>
    public double TolerancePercent
    {
        get => _tolerancePercent;
        set
        {
            if (value is < 0.0 or >= 1.0)
                throw new ArgumentOutOfRangeException(nameof(value), "TolerancePercent must be between 0.0 (inclusive) and 1.0 (exclusive).");
            _tolerancePercent = value;
        }
    }
}
