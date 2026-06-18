namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Serializable payload for <c>POST /api/public/score-configs</c>. Property names are projected to
/// camelCase by <see cref="LangfuseApiClient"/>.
/// </summary>
internal sealed record LangfuseScoreConfigRequest
{
    /// <summary>Gets the score config name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the Langfuse data-type token (<c>NUMERIC</c>, <c>BOOLEAN</c>, <c>CATEGORICAL</c>, <c>TEXT</c>).</summary>
    public required string DataType { get; init; }

    /// <summary>Gets the categories for a categorical config, or <see langword="null"/>.</summary>
    public IReadOnlyList<LangfuseScoreConfigCategory>? Categories { get; init; }

    /// <summary>Gets the inclusive minimum for numeric configs, or <see langword="null"/>.</summary>
    public double? MinValue { get; init; }

    /// <summary>Gets the inclusive maximum for numeric configs, or <see langword="null"/>.</summary>
    public double? MaxValue { get; init; }

    /// <summary>Gets the optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Projects a public <see cref="LangfuseScoreConfig"/> to the wire payload.</summary>
    /// <param name="config">The score config to project.</param>
    /// <returns>The request payload.</returns>
    public static LangfuseScoreConfigRequest From(LangfuseScoreConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var isNumeric = config.DataType == LangfuseScoreDataType.Numeric;
        var isCategorical = config.DataType == LangfuseScoreDataType.Categorical;

        return new LangfuseScoreConfigRequest
        {
            Name = config.Name,
            DataType = config.DataType.ToLangfuseToken(),
            Categories = isCategorical ? config.Categories : null,
            MinValue = isNumeric ? config.MinValue : null,
            MaxValue = isNumeric ? config.MaxValue : null,
            Description = config.Description,
        };
    }
}
