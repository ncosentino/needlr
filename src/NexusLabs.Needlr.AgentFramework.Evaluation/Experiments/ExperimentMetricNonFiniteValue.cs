namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Identifies a non-finite numeric metric value that cannot be represented as a JSON number.
/// </summary>
public enum ExperimentMetricNonFiniteValue
{
    /// <summary>The source value is not a number.</summary>
    NaN,

    /// <summary>The source value is positive infinity.</summary>
    PositiveInfinity,

    /// <summary>The source value is negative infinity.</summary>
    NegativeInfinity,
}
