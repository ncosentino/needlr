using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Reflection;

/// <summary>
/// Integration tests for Syringe verification during BuildServiceProvider().
/// These tests verify that verification runs automatically as part of the build flow.
/// </summary>
public sealed class SyringeVerificationIntegrationTests
{
    // Test service types - defined locally without Needlr attributes
    // to avoid auto-registration issues
    public interface ISingletonService { }
    public interface IScopedService { }

    public class SingletonDependsOnScoped : ISingletonService
    {
        public SingletonDependsOnScoped(IScopedService scoped) { }
    }

    public class ScopedService : IScopedService { }

    public class ValidSingleton : ISingletonService { }

    [Fact]
    public void BuildServiceProvider_WithStrictVerification_ThrowsOnMismatch()
    {
        // Arrange - manually register services with a mismatch
        var syringe = new Syringe()
            .UsingReflection()
            .WithVerification(VerificationOptions.Strict)
            .UsingPostPluginRegistrationCallback(services =>
            {
                // Clear auto-registered and manually add mismatch
                services.Clear();
                services.AddScoped<IScopedService, ScopedService>();
                services.AddSingleton<ISingletonService, SingletonDependsOnScoped>();
            });

        // Act & Assert
        var ex = Assert.Throws<ContainerVerificationException>(() =>
        {
            syringe.BuildServiceProvider();
        });

        Assert.NotEmpty(ex.Issues);
        Assert.Contains(ex.Issues, i => i.Type == VerificationIssueType.LifetimeMismatch);
    }

    [Fact]
    public void BuildServiceProvider_WithDefaultVerification_WarnsButDoesNotThrow()
    {
        // Arrange
        var warnings = new List<VerificationIssue>();
        var syringe = new Syringe()
            .UsingReflection()
            .WithVerification(new VerificationOptions
            {
                LifetimeMismatchBehavior = VerificationBehavior.Warn,
                IssueReporter = issue => warnings.Add(issue)
            })
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.Clear();
                services.AddScoped<IScopedService, ScopedService>();
                services.AddSingleton<ISingletonService, SingletonDependsOnScoped>();
            });

        // Act - should NOT throw
        var exception = Record.Exception(() =>
        {
            syringe.BuildServiceProvider();
        });

        // Assert
        Assert.Null(exception);
        Assert.NotEmpty(warnings);
        Assert.Contains(warnings, w => w.Type == VerificationIssueType.LifetimeMismatch);
    }

    [Fact]
    public void BuildServiceProvider_WithDisabledVerification_NoWarningsOrThrows()
    {
        // Arrange
        var warnings = new List<VerificationIssue>();
        var syringe = new Syringe()
            .UsingReflection()
            .WithVerification(VerificationOptions.Disabled)
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.Clear();
                services.AddScoped<IScopedService, ScopedService>();
                services.AddSingleton<ISingletonService, SingletonDependsOnScoped>();
            });

        // Act - should NOT throw
        var exception = Record.Exception(() =>
        {
            syringe.BuildServiceProvider();
        });

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void BuildServiceProvider_WithNoIssues_Succeeds()
    {
        // Arrange
        var syringe = new Syringe()
            .UsingReflection()
            .WithVerification(VerificationOptions.Strict)
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.Clear();
                services.AddSingleton<ISingletonService, ValidSingleton>();
            });

        // Act - should NOT throw
        var exception = Record.Exception(() =>
        {
            syringe.BuildServiceProvider();
        });

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void BuildServiceProvider_WithFluentBuilder_ConfiguresCorrectly()
    {
        // Arrange
        var syringe = new Syringe()
            .UsingReflection()
            .WithVerification(opts => opts
                .OnLifetimeMismatch(VerificationBehavior.Throw)
                .OnCircularDependency(VerificationBehavior.Throw))
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.Clear();
                services.AddScoped<IScopedService, ScopedService>();
                services.AddSingleton<ISingletonService, SingletonDependsOnScoped>();
            });

        // Act & Assert - should throw because OnLifetimeMismatch is Throw
        Assert.Throws<ContainerVerificationException>(() =>
        {
            syringe.BuildServiceProvider();
        });
    }

    [Fact]
    public void BuildServiceProvider_CustomReporter_ReceivesIssues()
    {
        // Arrange
        var reportedIssues = new List<VerificationIssue>();
        var syringe = new Syringe()
            .UsingReflection()
            .WithVerification(opts => opts
                .OnLifetimeMismatch(VerificationBehavior.Warn)
                .ReportIssuesTo(issue => reportedIssues.Add(issue)))
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.Clear();
                services.AddScoped<IScopedService, ScopedService>();
                services.AddSingleton<ISingletonService, SingletonDependsOnScoped>();
            });

        // Act
        syringe.BuildServiceProvider();

        // Assert
        Assert.Single(reportedIssues);
        Assert.Equal(VerificationIssueType.LifetimeMismatch, reportedIssues[0].Type);
    }

    [Fact]
    public void WithVerification_ReturnsNewSyringeInstance()
    {
        // Arrange
        var original = new Syringe().UsingReflection();

        // Act
        var configured = original.WithVerification(VerificationOptions.Strict);

        // Assert - should be different instances (immutable pattern)
        Assert.NotSame(original, configured);
    }
}
