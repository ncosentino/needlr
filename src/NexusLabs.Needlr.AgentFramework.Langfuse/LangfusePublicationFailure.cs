namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Describes why a Langfuse publication operation did not complete.
/// </summary>
public sealed record LangfusePublicationFailure
{
    /// <summary>
    /// Initializes a publication failure.
    /// </summary>
    /// <param name="code">The machine-actionable failure category.</param>
    /// <param name="message">The diagnostic failure message.</param>
    public LangfusePublicationFailure(
        LangfusePublicationFailureCode code,
        string message)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "The publication failure code is not defined.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
    }

    /// <summary>
    /// Gets the machine-actionable failure category.
    /// </summary>
    public LangfusePublicationFailureCode Code { get; }

    /// <summary>
    /// Gets the diagnostic failure message.
    /// </summary>
    public string Message { get; }
}
