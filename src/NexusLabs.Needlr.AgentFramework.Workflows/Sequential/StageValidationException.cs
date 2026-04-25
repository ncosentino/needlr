namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Thrown when a pipeline stage exhausts all retry attempts and still fails
/// post-validation.
/// </summary>
/// <example>
/// <code>
/// try
/// {
///     var result = await runner.RunAsync(workspace, stages, options, ct);
/// }
/// catch (StageValidationException ex)
/// {
///     Console.WriteLine($"Stage '{ex.StageName}' failed: {ex.ValidationError}");
/// }
/// </code>
/// </example>
public sealed class StageValidationException : Exception
{
    /// <summary>Gets the name of the stage that failed validation.</summary>
    public string StageName { get; }

    /// <summary>Gets the validation error message from the last failed attempt.</summary>
    public string ValidationError { get; }

    /// <summary>
    /// Initializes a new <see cref="StageValidationException"/>.
    /// </summary>
    /// <param name="stageName">The name of the stage that failed validation.</param>
    /// <param name="validationError">The error message from the post-validation function.</param>
    public StageValidationException(string stageName, string validationError)
        : base($"Stage '{stageName}' failed post-validation: {validationError}")
    {
        StageName = stageName;
        ValidationError = validationError;
    }
}
