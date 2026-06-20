namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Serializable payload for <c>POST /api/public/models</c>. Property names are projected to
/// camelCase by <see cref="LangfuseApiClient"/>.
/// </summary>
internal sealed record LangfuseCreateModelRequest
{
    public required string ModelName { get; init; }

    public required string MatchPattern { get; init; }

    public string? Unit { get; init; }

    public double? InputPrice { get; init; }

    public double? OutputPrice { get; init; }

    public double? TotalPrice { get; init; }

    public static LangfuseCreateModelRequest From(LangfuseModelPrice price)
    {
        ArgumentNullException.ThrowIfNull(price);

        return new LangfuseCreateModelRequest
        {
            ModelName = price.ModelName,
            MatchPattern = price.MatchPattern,
            Unit = price.Unit,
            InputPrice = price.InputPrice,
            OutputPrice = price.OutputPrice,
            TotalPrice = price.TotalPrice,
        };
    }
}
