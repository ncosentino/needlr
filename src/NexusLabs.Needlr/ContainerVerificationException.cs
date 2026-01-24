namespace NexusLabs.Needlr;

/// <summary>
/// Exception thrown when container verification fails with issues configured to throw.
/// </summary>
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
