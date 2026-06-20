namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// A model price definition registered with Langfuse so it can compute cost for generations whose
/// model it does not price out of the box (for example mock or internal model names). Langfuse
/// derives cost from token usage and these prices when a generation carries no explicit cost.
/// </summary>
public sealed record LangfuseModelPrice
{
    /// <summary>Gets the model definition name.</summary>
    public required string ModelName { get; init; }

    /// <summary>
    /// Gets the regular expression matched against a generation's model name. For an exact match
    /// use <c>(?i)^model-name$</c>.
    /// </summary>
    public required string MatchPattern { get; init; }

    /// <summary>Gets the price (USD) per input unit, or <see langword="null"/>.</summary>
    public double? InputPrice { get; init; }

    /// <summary>Gets the price (USD) per output unit, or <see langword="null"/>.</summary>
    public double? OutputPrice { get; init; }

    /// <summary>
    /// Gets the price (USD) per total unit, or <see langword="null"/>. Cannot be combined with
    /// <see cref="InputPrice"/> / <see cref="OutputPrice"/>.
    /// </summary>
    public double? TotalPrice { get; init; }

    /// <summary>Gets the usage unit. Defaults to <c>TOKENS</c>.</summary>
    public string Unit { get; init; } = "TOKENS";
}
