using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// The result of a <see cref="LangfuseMetricsQuery"/>. Each row is a dictionary of column name to
/// value; aggregated columns are named <c>{Aggregation}_{Measure}</c> and dimension columns keep
/// their field name.
/// </summary>
public sealed record LangfuseMetricsResult
{
    internal LangfuseMetricsResult(IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Rows = rows;
    }

    /// <summary>Gets the result rows.</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> Rows { get; }

    /// <summary>
    /// Gets the numeric value of the <c>{aggregation}_{measure}</c> column from the first row, or
    /// <see langword="null"/> when there are no rows or the column is absent or non-numeric. Use
    /// this for ungrouped queries that produce a single aggregate row.
    /// </summary>
    /// <param name="aggregation">The aggregation (for example <c>avg</c>).</param>
    /// <param name="measure">The measure (for example <c>value</c>).</param>
    /// <returns>The scalar value, or <see langword="null"/>.</returns>
    public double? GetScalar(string aggregation, string measure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregation);
        ArgumentException.ThrowIfNullOrWhiteSpace(measure);

        if (Rows.Count == 0)
        {
            return null;
        }

        return Rows[0].TryGetValue($"{aggregation}_{measure}", out var element)
            && element.ValueKind == JsonValueKind.Number
            ? element.GetDouble()
            : null;
    }
}
