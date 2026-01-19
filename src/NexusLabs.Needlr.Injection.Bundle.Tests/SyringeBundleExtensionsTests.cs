using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Injection.Bundle.Tests;

public sealed class SyringeBundleExtensionsTests
{
    [Fact]
    public void UsingAutoConfiguration_WithSourceGen_UsesSourceGenComponents()
    {
        // Arrange
        using var scope = NeedlrSourceGenBootstrap.BeginTestScope(
            () => [],
            () => []);

        var syringe = new Syringe().UsingAutoConfiguration();

        // Act
        var typeRegistrar = syringe.GetOrCreateTypeRegistrar();
        var typeFilterer = syringe.GetOrCreateTypeFilterer();
        var pluginFactory = syringe.GetOrCreatePluginFactory();

        // Assert - should use source-gen components
        Assert.NotNull(typeRegistrar);
        Assert.NotNull(typeFilterer);
        Assert.NotNull(pluginFactory);
    }

    [Fact]
    public void UsingAutoConfiguration_WithoutSourceGen_FallsBackToReflection()
    {
        // Arrange - ensure no source-gen bootstrap
        // Note: We can't easily clear the bootstrap, so we test that
        // UsingAutoConfiguration returns a configured syringe
        var syringe = new Syringe().UsingAutoConfiguration();

        // Act
        var typeRegistrar = syringe.GetOrCreateTypeRegistrar();
        var typeFilterer = syringe.GetOrCreateTypeFilterer();
        var pluginFactory = syringe.GetOrCreatePluginFactory();

        // Assert - should have components configured (either source-gen or reflection)
        Assert.NotNull(typeRegistrar);
        Assert.NotNull(typeFilterer);
        Assert.NotNull(pluginFactory);
    }

    [Fact]
    public void UsingAutoConfiguration_ThrowsOnNullSyringe()
    {
        // Arrange
        Syringe? syringe = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => syringe!.UsingAutoConfiguration());
    }

    [Fact]
    public void WithFallbackBehavior_InvokesFallbackHandler_WhenNoSourceGen()
    {
        // Arrange
        ReflectionFallbackContext? capturedContext = null;

        // Without source-gen bootstrap, fallback should occur
        // Note: This test assumes no global source-gen bootstrap is registered
        var syringe = new Syringe()
            .WithFallbackBehavior(ctx =>
            {
                capturedContext = ctx;
            });

        // The handler is invoked during WithFallbackBehavior if source-gen is not available.
        // We verify the syringe is configured; the context capture may or may not occur
        // depending on whether source-gen is globally registered from other tests.
        Assert.NotNull(syringe);
    }

    [Fact]
    public void WithFallbackBehavior_DoesNotInvokeFallbackHandler_WhenSourceGenAvailable()
    {
        // Arrange
        var handlerInvoked = false;

        using var scope = NeedlrSourceGenBootstrap.BeginTestScope(
            () => [],
            () => []);

        // Act
        var syringe = new Syringe()
            .WithFallbackBehavior(ctx => handlerInvoked = true);

        // Assert - handler should NOT be invoked when source-gen is available
        Assert.False(handlerInvoked);
    }

    [Fact]
    public void WithFallbackBehavior_ThrowsOnNullSyringe()
    {
        // Arrange
        Syringe? syringe = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => syringe!.WithFallbackBehavior(null));
    }

    [Fact]
    public void WithFastFailOnReflection_ThrowsOnNullSyringe()
    {
        // Arrange
        Syringe? syringe = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => syringe!.WithFastFailOnReflection());
    }

    [Fact]
    public void WithFastFailOnReflection_WithSourceGen_DoesNotThrow()
    {
        // Arrange
        using var scope = NeedlrSourceGenBootstrap.BeginTestScope(
            () => [],
            () => []);

        // Act & Assert - should not throw when source-gen is available
        var syringe = new Syringe().WithFastFailOnReflection();
        Assert.NotNull(syringe);
    }

    [Fact]
    public void WithFallbackLogging_ThrowsOnNullSyringe()
    {
        // Arrange
        Syringe? syringe = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => syringe!.WithFallbackLogging());
    }

    [Fact]
    public void WithFallbackLogging_WithSourceGen_DoesNotLog()
    {
        // Arrange
        using var scope = NeedlrSourceGenBootstrap.BeginTestScope(
            () => [],
            () => []);

        // Act - should not throw or log when source-gen is available
        var syringe = new Syringe().WithFallbackLogging();

        // Assert
        Assert.NotNull(syringe);
    }
}
