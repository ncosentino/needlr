namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// The exception thrown when a request to the Langfuse API fails.
/// </summary>
public class LangfuseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LangfuseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public LangfuseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LangfuseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public LangfuseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
