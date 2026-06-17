namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// The data type of a Langfuse score, controlling how the score value is interpreted and
/// displayed.
/// </summary>
public enum LangfuseScoreDataType
{
    /// <summary>A continuous numeric score. The value is a floating-point number.</summary>
    Numeric = 0,

    /// <summary>A boolean score. The value is <c>1</c> (true) or <c>0</c> (false).</summary>
    Boolean = 1,

    /// <summary>A categorical score. The value is a category label string.</summary>
    Categorical = 2,

    /// <summary>A free-text score. The value is an arbitrary string (1–500 characters).</summary>
    Text = 3,
}
