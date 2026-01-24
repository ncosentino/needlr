using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Needlr;

/// <summary>
/// Extension methods for verifying service collection configuration.
/// </summary>
public static class ServiceCollectionVerificationExtensions
{
    /// <summary>
    /// Verifies the service collection configuration and throws if issues are configured to throw.
    /// </summary>
    /// <param name="services">The service collection to verify.</param>
    /// <param name="options">The verification options. Defaults to <see cref="VerificationOptions.Default"/>.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <exception cref="ContainerVerificationException">
    /// Thrown if verification issues are detected and the configured behavior is <see cref="VerificationBehavior.Throw"/>.
    /// </exception>
    public static IServiceCollection Verify(
        this IServiceCollection services,
        VerificationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        options ??= VerificationOptions.Default;

        var issues = new List<VerificationIssue>();

        // Check for lifetime mismatches
        if (options.LifetimeMismatchBehavior != VerificationBehavior.Silent)
        {
            var mismatches = services.DetectLifetimeMismatches();
            foreach (var mismatch in mismatches)
            {
                issues.Add(new VerificationIssue(
                    Type: VerificationIssueType.LifetimeMismatch,
                    Message: $"Lifetime mismatch: {mismatch.ConsumerServiceType.Name} ({mismatch.ConsumerLifetime}) depends on {mismatch.DependencyServiceType.Name} ({mismatch.DependencyLifetime})",
                    DetailedMessage: mismatch.ToDetailedString(),
                    ConfiguredBehavior: options.LifetimeMismatchBehavior)
                {
                    InvolvedTypes = [mismatch.ConsumerServiceType, mismatch.DependencyServiceType]
                });
            }
        }

        // Process issues based on configured behavior
        ProcessVerificationIssues(issues, options);

        return services;
    }

    /// <summary>
    /// Verifies the service collection and returns detailed diagnostic results.
    /// </summary>
    /// <param name="services">The service collection to verify.</param>
    /// <param name="options">The verification options. Defaults to <see cref="VerificationOptions.Default"/>.</param>
    /// <returns>A <see cref="VerificationResult"/> containing all detected issues.</returns>
    public static VerificationResult VerifyWithDiagnostics(
        this IServiceCollection services,
        VerificationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        options ??= VerificationOptions.Default;

        var issues = new List<VerificationIssue>();

        // Check for lifetime mismatches
        if (options.LifetimeMismatchBehavior != VerificationBehavior.Silent)
        {
            var mismatches = services.DetectLifetimeMismatches();
            foreach (var mismatch in mismatches)
            {
                issues.Add(new VerificationIssue(
                    Type: VerificationIssueType.LifetimeMismatch,
                    Message: $"Lifetime mismatch: {mismatch.ConsumerServiceType.Name} ({mismatch.ConsumerLifetime}) depends on {mismatch.DependencyServiceType.Name} ({mismatch.DependencyLifetime})",
                    DetailedMessage: mismatch.ToDetailedString(),
                    ConfiguredBehavior: options.LifetimeMismatchBehavior)
                {
                    InvolvedTypes = [mismatch.ConsumerServiceType, mismatch.DependencyServiceType]
                });
            }
        }

        return new VerificationResult(issues);
    }

    private static void ProcessVerificationIssues(List<VerificationIssue> issues, VerificationOptions options)
    {
        var issuesByBehavior = issues.GroupBy(i => i.ConfiguredBehavior);

        foreach (var group in issuesByBehavior)
        {
            switch (group.Key)
            {
                case VerificationBehavior.Warn:
                    foreach (var issue in group)
                    {
                        if (options.IssueReporter is not null)
                        {
                            options.IssueReporter(issue);
                        }
                        else
                        {
                            Console.Error.WriteLine($"[Needlr Warning] {issue.Message}");
                            Console.Error.WriteLine(issue.DetailedMessage);
                            Console.Error.WriteLine();
                        }
                    }
                    break;

                case VerificationBehavior.Throw:
                    var throwableIssues = group.ToList();
                    if (throwableIssues.Count > 0)
                    {
                        throw new ContainerVerificationException(throwableIssues);
                    }
                    break;
            }
        }
    }
}

/// <summary>
/// Result of container verification containing all detected issues.
/// </summary>
/// <param name="Issues">The list of verification issues detected.</param>
public sealed record VerificationResult(IReadOnlyList<VerificationIssue> Issues)
{
    /// <summary>
    /// Gets whether the container configuration is valid (no errors).
    /// </summary>
    public bool IsValid => !Issues.Any(i => i.ConfiguredBehavior == VerificationBehavior.Throw);

    /// <summary>
    /// Throws <see cref="ContainerVerificationException"/> if any issues are present.
    /// </summary>
    public void ThrowIfInvalid()
    {
        if (Issues.Count > 0)
        {
            throw new ContainerVerificationException(Issues.ToList());
        }
    }

    /// <summary>
    /// Generates a detailed diagnostic report of all issues.
    /// </summary>
    public string ToDetailedReport()
    {
        if (Issues.Count == 0)
        {
            return "✅ Container verification passed. No issues detected.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"❌ Container verification found {Issues.Count} issue(s):");
        sb.AppendLine();

        foreach (var issue in Issues)
        {
            sb.AppendLine($"[{issue.Type}] {issue.Message}");
            if (!string.IsNullOrWhiteSpace(issue.DetailedMessage))
            {
                sb.AppendLine(issue.DetailedMessage);
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
