using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace NexusLabs.Needlr.Tests;

/// <summary>
/// Tests for IServiceCollection.Verify() extension methods.
/// These test explicit verification calls on service collections.
/// </summary>
public class ServiceCollectionVerificationTests
{
    // Test types
    public interface ISingletonService { }
    public interface IScopedService { }
    public interface ITransientService { }

    public class SingletonDependsOnScoped(IScopedService scoped) : ISingletonService
    {
        public IScopedService Scoped { get; } = scoped;
    }

    public class ScopedService : IScopedService { }

    public class SingletonDependsOnTransient(ITransientService transient) : ISingletonService
    {
        public ITransientService Transient { get; } = transient;
    }

    public class TransientService : ITransientService { }

    public class ScopedDependsOnTransient(ITransientService transient) : IScopedService
    {
        public ITransientService Transient { get; } = transient;
    }

    public class ValidSingleton : ISingletonService { }

    // Test: Default behavior warns on lifestyle mismatch (doesn't throw)
    [Fact]
    public void Verify_WithLifestyleMismatch_DefaultBehaviorWarns()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedService>();
        services.AddSingleton<ISingletonService, SingletonDependsOnScoped>();

        var warnings = new List<VerificationIssue>();
        var options = new VerificationOptions
        {
            LifestyleMismatchBehavior = VerificationBehavior.Warn,
            IssueReporter = issue => warnings.Add(issue)
        };

        // Act - should NOT throw because default behavior is Warn
        var exception = Record.Exception(() =>
        {
            services.Verify(options);
        });

        // Assert
        Assert.Null(exception);
        Assert.Single(warnings);
        Assert.Equal(VerificationIssueType.LifestyleMismatch, warnings[0].Type);
    }

    // Test: Strict mode throws on lifestyle mismatch
    [Fact]
    public void Verify_WithLifestyleMismatch_StrictModeThrows()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedService>();
        services.AddSingleton<ISingletonService, SingletonDependsOnScoped>();

        // Act & Assert
        var ex = Assert.Throws<ContainerVerificationException>(() =>
        {
            services.Verify(VerificationOptions.Strict);
        });

        Assert.Single(ex.Issues);
        Assert.Equal(VerificationIssueType.LifestyleMismatch, ex.Issues[0].Type);
        Assert.Contains("ISingletonService", ex.Issues[0].Message);
        Assert.Contains("IScopedService", ex.Issues[0].Message);
    }

    // Test: Silent mode doesn't warn or throw
    [Fact]
    public void Verify_WithLifestyleMismatch_SilentModeNoAction()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedService>();
        services.AddSingleton<ISingletonService, SingletonDependsOnScoped>();

        var warnings = new List<VerificationIssue>();
        var options = new VerificationOptions
        {
            LifestyleMismatchBehavior = VerificationBehavior.Silent,
            IssueReporter = issue => warnings.Add(issue)
        };

        // Act - should NOT throw and NOT warn
        var exception = Record.Exception(() =>
        {
            services.Verify(options);
        });

        // Assert
        Assert.Null(exception);
        Assert.Empty(warnings);
    }

    // Test: Custom issue reporter receives issues
    [Fact]
    public void Verify_CustomReporter_ReceivesIssues()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITransientService, TransientService>();
        services.AddSingleton<ISingletonService, SingletonDependsOnTransient>();

        var reported = new List<VerificationIssue>();
        var options = new VerificationOptions
        {
            LifestyleMismatchBehavior = VerificationBehavior.Warn,
            IssueReporter = issue => reported.Add(issue)
        };

        // Act
        services.Verify(options);

        // Assert
        Assert.Single(reported);
        Assert.Contains(typeof(ISingletonService), reported[0].InvolvedTypes!);
        Assert.Contains(typeof(ITransientService), reported[0].InvolvedTypes!);
    }

    // Test: No issues when configuration is valid
    [Fact]
    public void Verify_NoIssues_NoExceptionsOrWarnings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, ValidSingleton>();

        // Act - should NOT throw because there are no issues
        var exception = Record.Exception(() =>
        {
            services.Verify(VerificationOptions.Strict);
        });

        // Assert
        Assert.Null(exception);
    }

    // Test: Multiple mismatches are all detected
    [Fact]
    public void Verify_MultipleMismatches_AllDetected()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedDependsOnTransient>();
        services.AddTransient<ITransientService, TransientService>();
        services.AddSingleton<ISingletonService, SingletonDependsOnScoped>();

        var reported = new List<VerificationIssue>();
        var options = new VerificationOptions
        {
            LifestyleMismatchBehavior = VerificationBehavior.Warn,
            IssueReporter = issue => reported.Add(issue)
        };

        // Act
        services.Verify(options);

        // Assert - should detect both mismatches
        Assert.Equal(2, reported.Count);
    }

    // Test: VerifyWithDiagnostics returns result
    [Fact]
    public void VerifyWithDiagnostics_ReturnsResult()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedService>();
        services.AddSingleton<ISingletonService, SingletonDependsOnScoped>();

        // Act
        var result = services.VerifyWithDiagnostics(VerificationOptions.Strict);

        // Assert
        Assert.Single(result.Issues);
        Assert.False(result.IsValid);
    }

    // Test: VerificationResult.ToDetailedReport works
    [Fact]
    public void VerificationResult_ToDetailedReport_FormatsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IScopedService, ScopedService>();
        services.AddSingleton<ISingletonService, SingletonDependsOnScoped>();

        // Act
        var result = services.VerifyWithDiagnostics();
        var report = result.ToDetailedReport();

        // Assert
        Assert.Contains("issue(s)", report);
        Assert.Contains("LifestyleMismatch", report);
    }

    // Test: ContainerVerificationException contains all issues
    [Fact]
    public void ContainerVerificationException_ContainsAllIssues()
    {
        // Arrange
        var issues = new List<VerificationIssue>
        {
            new(VerificationIssueType.LifestyleMismatch, "Issue 1", "Details 1", VerificationBehavior.Throw),
            new(VerificationIssueType.LifestyleMismatch, "Issue 2", "Details 2", VerificationBehavior.Throw)
        };

        // Act
        var ex = new ContainerVerificationException(issues);

        // Assert
        Assert.Equal(2, ex.Issues.Count);
        Assert.Contains("2 LifestyleMismatch issue(s)", ex.Message);
    }

    // Test: Exception message is descriptive
    [Fact]
    public void ContainerVerificationException_MessageIsDescriptive()
    {
        // Arrange
        var issues = new List<VerificationIssue>
        {
            new(VerificationIssueType.LifestyleMismatch, "Singleton depends on Scoped", "...", VerificationBehavior.Throw)
        };

        // Act
        var ex = new ContainerVerificationException(issues);

        // Assert
        Assert.Contains("Container verification failed", ex.Message);
        Assert.Contains("LifestyleMismatch", ex.Message);
    }

    // Test: Verify returns same IServiceCollection for chaining
    [Fact]
    public void Verify_ReturnsServiceCollection_ForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, ValidSingleton>();

        // Act
        var result = services.Verify();

        // Assert
        Assert.Same(services, result);
    }

    // Test: Verify with null options uses defaults
    [Fact]
    public void Verify_WithNullOptions_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonService, ValidSingleton>();

        // Act - should not throw
        var exception = Record.Exception(() =>
        {
            services.Verify(null);
        });

        // Assert
        Assert.Null(exception);
    }
}
