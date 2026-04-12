using Microsoft.Extensions.Logging;

using Xunit;

namespace NexusLabs.Needlr.Hosting.Tests;

public sealed class NeedlrBootstrapperTests
{
    // -------------------------------------------------------------------------
    // UsingLoggerFactory
    // -------------------------------------------------------------------------

    [Fact]
    public void UsingLoggerFactory_WithNullBootstrapper_ThrowsArgumentNullException()
    {
        NeedlrBootstrapper bootstrapper = null!;
        Assert.Throws<ArgumentNullException>(() =>
            bootstrapper.UsingLoggerFactory(new CapturingLoggerFactory()));
    }

    [Fact]
    public void UsingLoggerFactory_WithNullFactory_ThrowsArgumentNullException()
    {
        var bootstrapper = new NeedlrBootstrapper();
        Assert.Throws<ArgumentNullException>(() =>
            bootstrapper.UsingLoggerFactory(null!));
    }

    [Fact]
    public void UsingLoggerFactory_ReturnsNewInstance()
    {
        var bootstrapper = new NeedlrBootstrapper();
        var result = bootstrapper.UsingLoggerFactory(new CapturingLoggerFactory());

        Assert.NotSame(bootstrapper, result);
    }

    [Fact]
    public async Task UsingLoggerFactory_NewInstance_UsesProvidedFactory()
    {
        var factory = new CapturingLoggerFactory();
        ILogger? receivedLogger = null;
        var bootstrapper = new NeedlrBootstrapper().UsingLoggerFactory(factory);

        await bootstrapper.RunAsync((ctx, ct) =>
        {
            receivedLogger = ctx.Logger;
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.Same(factory.Logger, receivedLogger);
    }

    // -------------------------------------------------------------------------
    // WithCleanup
    // -------------------------------------------------------------------------

    [Fact]
    public void WithCleanup_WithNullBootstrapper_ThrowsArgumentNullException()
    {
        NeedlrBootstrapper bootstrapper = null!;
        Assert.Throws<ArgumentNullException>(() =>
            bootstrapper.WithCleanup(() => Task.CompletedTask));
    }

    [Fact]
    public void WithCleanup_WithNullCleanup_ThrowsArgumentNullException()
    {
        var bootstrapper = new NeedlrBootstrapper();
        Assert.Throws<ArgumentNullException>(() =>
            bootstrapper.WithCleanup(null!));
    }

    [Fact]
    public void WithCleanup_ReturnsNewInstance()
    {
        var bootstrapper = new NeedlrBootstrapper();
        var result = bootstrapper.WithCleanup(() => Task.CompletedTask);

        Assert.NotSame(bootstrapper, result);
    }

    // -------------------------------------------------------------------------
    // RunAsync -- null guard
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithNullCallback_ThrowsArgumentNullException()
    {
        var bootstrapper = new NeedlrBootstrapper();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            bootstrapper.RunAsync(null!, TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------------------------------
    // RunAsync -- happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_InvokesCallback()
    {
        var invoked = false;
        var bootstrapper = new NeedlrBootstrapper()
            .UsingLoggerFactory(new CapturingLoggerFactory());

        await bootstrapper.RunAsync((ctx, ct) =>
        {
            invoked = true;
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.True(invoked);
    }

    [Fact]
    public async Task RunAsync_PassesCancellationTokenToCallback()
    {
        var token = TestContext.Current.CancellationToken;
        CancellationToken received = default;
        var bootstrapper = new NeedlrBootstrapper()
            .UsingLoggerFactory(new CapturingLoggerFactory());

        await bootstrapper.RunAsync((ctx, ct) =>
        {
            received = ct;
            return Task.CompletedTask;
        }, token);

        Assert.Equal(token, received);
    }

    [Fact]
    public async Task RunAsync_Context_HasLogger()
    {
        ILogger? capturedLogger = null;
        var bootstrapper = new NeedlrBootstrapper()
            .UsingLoggerFactory(new CapturingLoggerFactory());

        await bootstrapper.RunAsync((ctx, ct) =>
        {
            capturedLogger = ctx.Logger;
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(capturedLogger);
    }

    // -------------------------------------------------------------------------
    // RunAsync -- cleanup
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_CallsCleanup_OnSuccess()
    {
        var cleanupCalled = false;
        var bootstrapper = new NeedlrBootstrapper()
            .UsingLoggerFactory(new CapturingLoggerFactory())
            .WithCleanup(() =>
            {
                cleanupCalled = true;
                return Task.CompletedTask;
            });

        await bootstrapper.RunAsync(
            (ctx, ct) => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        Assert.True(cleanupCalled);
    }

    [Fact]
    public async Task RunAsync_CallsCleanup_OnException()
    {
        var cleanupCalled = false;
        var bootstrapper = new NeedlrBootstrapper()
            .UsingLoggerFactory(new CapturingLoggerFactory())
            .WithCleanup(() =>
            {
                cleanupCalled = true;
                return Task.CompletedTask;
            });

        await bootstrapper.RunAsync(
            (ctx, ct) => throw new InvalidOperationException("boom"),
            TestContext.Current.CancellationToken);

        Assert.True(cleanupCalled);
    }

    // -------------------------------------------------------------------------
    // RunAsync -- exception handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_DoesNotRethrow_OnException()
    {
        var bootstrapper = new NeedlrBootstrapper()
            .UsingLoggerFactory(new CapturingLoggerFactory());

        // Must not throw
        await bootstrapper.RunAsync(
            (ctx, ct) => throw new InvalidOperationException("unhandled"),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RunAsync_LogsCritical_OnException()
    {
        var factory = new CapturingLoggerFactory();
        var ex = new InvalidOperationException("boom");
        var bootstrapper = new NeedlrBootstrapper()
            .UsingLoggerFactory(factory);

        await bootstrapper.RunAsync(
            (ctx, ct) => throw ex,
            TestContext.Current.CancellationToken);

        Assert.Contains(factory.Logger.Logs, e => e.Level == LogLevel.Critical && e.Ex == ex);
    }
}

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

internal sealed class CapturingLoggerFactory : ILoggerFactory
{
    public readonly CapturingLogger Logger = new();

    public ILogger CreateLogger(string categoryName) => Logger;

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }
}

internal sealed class CapturingLogger : ILogger
{
    public readonly List<(LogLevel Level, Exception? Ex, string Message)> Logs = [];

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Logs.Add((logLevel, exception, formatter(state, exception)));
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
