namespace NexusLabs.Needlr.AgentFramework.Testing;

/// <summary>
/// Thrown by <see cref="PromptAssert"/> methods when a prompt integrity assertion fails.
/// </summary>
/// <remarks>
/// This exception is framework-agnostic — it does not depend on xUnit, NUnit, or MSTest.
/// Any test runner will surface it as a failed test with the descriptive message.
/// </remarks>
public sealed class PromptAssertionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PromptAssertionException"/> class
    /// with the specified failure message.
    /// </summary>
    /// <param name="message">A description of the assertion failure.</param>
    public PromptAssertionException(string message) : base(message)
    {
    }
}
