namespace NexusLabs.Needlr;

/// <summary>
/// Exception thrown when Needlr container verification detects one or more registered issues
/// that are configured to fail-fast at startup.
/// </summary>
/// <remarks>
/// <para>
/// Container verification is triggered by calling <c>VerifyContainer()</c> (or the equivalent
/// extension) after building the service provider. Each <see cref="VerificationIssue"/> in
/// <see cref="Issues"/> describes one problem — for example, a missing required dependency or
/// a misconfigured lifetime.
/// </para>
/// <para>
/// Only issues whose configured severity is set to throw will produce this exception.
/// Issues configured to warn are reported through the logging pipeline instead.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     var provider = services.BuildServiceProvider();
///     provider.VerifyContainer();
/// }
/// catch (ContainerVerificationException ex)
/// {
///     foreach (var issue in ex.Issues)
///     {
///         Console.Error.WriteLine($"[{issue.Type}] {issue.Message}");
///     }
///     throw;
/// }
/// </code>
/// </example>
public sealed class ContainerVerificationException : Exception
{
    /// <summary>
    /// Gets the verification issues that caused this exception.
    /// </summary>
    public IReadOnlyList<VerificationIssue> Issues { get; }

    /// <summary>
    /// Creates a new container verification exception.
    /// </summary>
    /// <param name="issues">The verification issues that caused the exception.</param>
    public ContainerVerificationException(IReadOnlyList<VerificationIssue> issues)
        : base(FormatMessage(issues))
    {
        Issues = issues;
    }

    private static string FormatMessage(IReadOnlyList<VerificationIssue> issues)
    {
        var grouped = issues.GroupBy(i => i.Type);
        var parts = new List<string>();

        foreach (var group in grouped)
        {
            parts.Add($"{group.Count()} {group.Key} issue(s)");
        }

        return $"Container verification failed: {string.Join(", ", parts)}. " +
               $"See the {nameof(Issues)} property for details.";
    }
}
