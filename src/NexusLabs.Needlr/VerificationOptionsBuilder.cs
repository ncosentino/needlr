namespace NexusLabs.Needlr;

/// <summary>
/// Builder for creating <see cref="VerificationOptions"/> with a fluent API.
/// </summary>
public sealed class VerificationOptionsBuilder
{
    private VerificationBehavior _lifetimeMismatchBehavior = VerificationBehavior.Warn;
    private VerificationBehavior _circularDependencyBehavior = VerificationBehavior.Throw;
    private Action<VerificationIssue>? _issueReporter;

    /// <summary>
    /// Sets the behavior when lifetime mismatches are detected.
    /// </summary>
    public VerificationOptionsBuilder OnLifetimeMismatch(VerificationBehavior behavior)
    {
        _lifetimeMismatchBehavior = behavior;
        return this;
    }

    /// <summary>
    /// Sets the behavior when circular dependencies are detected.
    /// </summary>
    public VerificationOptionsBuilder OnCircularDependency(VerificationBehavior behavior)
    {
        _circularDependencyBehavior = behavior;
        return this;
    }

    /// <summary>
    /// Sets a custom reporter for verification issues.
    /// </summary>
    public VerificationOptionsBuilder ReportIssuesTo(Action<VerificationIssue> reporter)
    {
        _issueReporter = reporter;
        return this;
    }

    /// <summary>
    /// Throws on any verification issue.
    /// </summary>
    public VerificationOptionsBuilder Strict()
    {
        _lifetimeMismatchBehavior = VerificationBehavior.Throw;
        _circularDependencyBehavior = VerificationBehavior.Throw;
        return this;
    }

    /// <summary>
    /// Disables all verification.
    /// </summary>
    public VerificationOptionsBuilder Disabled()
    {
        _lifetimeMismatchBehavior = VerificationBehavior.Silent;
        _circularDependencyBehavior = VerificationBehavior.Silent;
        return this;
    }

    /// <summary>
    /// Builds the configured <see cref="VerificationOptions"/>.
    /// </summary>
    public VerificationOptions Build() => new()
    {
        LifetimeMismatchBehavior = _lifetimeMismatchBehavior,
        CircularDependencyBehavior = _circularDependencyBehavior,
        IssueReporter = _issueReporter
    };
}
