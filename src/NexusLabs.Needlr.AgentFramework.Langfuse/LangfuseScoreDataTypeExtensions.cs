namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Maps a <see cref="LangfuseScoreDataType"/> to the string token expected by the Langfuse REST API.
/// </summary>
internal static class LangfuseScoreDataTypeExtensions
{
    /// <summary>Returns the Langfuse API token (<c>NUMERIC</c>, <c>BOOLEAN</c>, <c>CATEGORICAL</c>, <c>TEXT</c>).</summary>
    /// <param name="dataType">The data type.</param>
    /// <returns>The uppercase Langfuse token.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dataType"/> is not a defined value.</exception>
    public static string ToLangfuseToken(this LangfuseScoreDataType dataType) => dataType switch
    {
        LangfuseScoreDataType.Numeric => "NUMERIC",
        LangfuseScoreDataType.Boolean => "BOOLEAN",
        LangfuseScoreDataType.Categorical => "CATEGORICAL",
        LangfuseScoreDataType.Text => "TEXT",
        _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Unknown Langfuse score data type."),
    };
}
