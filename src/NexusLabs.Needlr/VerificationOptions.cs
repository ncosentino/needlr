using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Needlr;

/// <summary>
/// Options for controlling container verification behavior.
/// </summary>
public sealed record VerificationOptions
{
    /// <summary>
    /// Gets or sets the behavior when lifetime mismatches are detected.
    /// Default is <see cref="VerificationBehavior.Warn"/>.
    /// </summary>
    public VerificationBehavior LifetimeMismatchBehavior { get; init; } = VerificationBehavior.Warn;

    /// <summary>
    /// Gets or sets the behavior when circular dependencies are detected.
    /// Default is <see cref="VerificationBehavior.Throw"/>.
    /// </summary>
    public VerificationBehavior CircularDependencyBehavior { get; init; } = VerificationBehavior.Throw;

    /// <summary>
    /// Gets or sets a callback for reporting verification issues.
    /// If null, issues are written to Console.Error.
    /// </summary>
    public Action<VerificationIssue>? IssueReporter { get; init; }

    /// <summary>
    /// Default verification options - warns on lifetime mismatches, throws on circular dependencies.
    /// </summary>
    public static VerificationOptions Default => new();

    /// <summary>
    /// Strict verification options - throws on any verification issue.
    /// </summary>
    public static VerificationOptions Strict => new()
    {
        LifetimeMismatchBehavior = VerificationBehavior.Throw,
        CircularDependencyBehavior = VerificationBehavior.Throw
    };

    /// <summary>
    /// Disabled verification - no checks performed.
    /// </summary>
    public static VerificationOptions Disabled => new()
    {
        LifetimeMismatchBehavior = VerificationBehavior.Silent,
        CircularDependencyBehavior = VerificationBehavior.Silent
    };
}

/// <summary>
/// Specifies the behavior when a verification issue is detected.
/// </summary>
public enum VerificationBehavior
{
    /// <summary>
    /// Ignore the issue silently.
    /// </summary>
    Silent = 0,

    /// <summary>
    /// Log a warning but continue.
    /// </summary>
    Warn = 1,

    /// <summary>
    /// Throw an exception to prevent the application from starting.
    /// </summary>
    Throw = 2
}

/// <summary>
/// Represents a verification issue detected during container analysis.
/// </summary>
public sealed record VerificationIssue(
    VerificationIssueType Type,
    string Message,
    string DetailedMessage,
    VerificationBehavior ConfiguredBehavior)
{
    /// <summary>
    /// Gets the service types involved in this issue.
    /// </summary>
    public IReadOnlyList<Type> InvolvedTypes { get; init; } = [];
}

/// <summary>
/// The type of verification issue detected.
/// </summary>
public enum VerificationIssueType
{
    /// <summary>
    /// A longer-lived service depends on a shorter-lived service.
    /// </summary>
    LifetimeMismatch,

    /// <summary>
    /// A circular dependency was detected in the dependency graph.
    /// </summary>
    CircularDependency
}
