using Microsoft.Extensions.Logging;

using Serilog.Core;
using Serilog.Events;

using Xunit;

namespace NexusLabs.Needlr.Serilog.Tests;

// All tests in one class so xUnit v3 runs them sequentially,
// avoiding parallel interference on the global Log.Logger.
public sealed class NeedlrSerilogBootstrapperTests
{
    // -------------------------------------------------------------------------
    // Configure extension method
    // -------------------------------------------------------------------------

    [Fact]
    public void Configure_WithNullBootstrapper_ThrowsArgumentNullException()
    {
        NeedlrSerilogBootstrapper bootstrapper = null!;
        Assert.Throws<ArgumentNullException>(() =>
            bootstrapper.Configure(_ => { }));
    }

    [Fact]
    public void Configure_WithNullAction_ThrowsArgumentNullException()
    {
        var bootstrapper = new NeedlrSerilogBootstrapper();
        Assert.Throws<ArgumentNullException>(() =>
            bootstrapper.Configure(null!));
    }

    [Fact]
    public void Configure_ReturnsNewInstance()
    {
        var bootstrapper = new NeedlrSerilogBootstrapper();
        var result = bootstrapper.Configure(_ => { });

        Assert.NotSame(bootstrapper, result);
    }

    // -------------------------------------------------------------------------
    // RunAsync — null guard
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithNullCallback_ThrowsArgumentNullException()
    {
        var bootstrapper = new NeedlrSerilogBootstrapper();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            bootstrapper.RunAsync(null!, TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------------------------------
    // RunAsync — happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_InvokesCallback()
    {
        var invoked = false;
        await new NeedlrSerilogBootstrapper()
            .Configure(cfg => cfg.WriteTo.Sink(new CapturingSink()))
            .RunAsync((ctx, ct) =>
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

        await new NeedlrSerilogBootstrapper()
            .Configure(cfg => cfg.WriteTo.Sink(new CapturingSink()))
            .RunAsync((ctx, ct) =>
            {
                received = ct;
                return Task.CompletedTask;
            }, token);

        Assert.Equal(token, received);
    }

    [Fact]
    public async Task RunAsync_Context_HasNonNullLogger()
    {
        ILogger? capturedLogger = null;

        await new NeedlrSerilogBootstrapper()
            .Configure(cfg => cfg.WriteTo.Sink(new CapturingSink()))
            .RunAsync((ctx, ct) =>
            {
                capturedLogger = ctx.Logger;
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

        Assert.NotNull(capturedLogger);
    }

    [Fact]
    public async Task RunAsync_WithDefaultConfig_DoesNotThrow()
    {
        // Default config (WriteTo.Console) should run without error.
        await new NeedlrSerilogBootstrapper()
            .RunAsync(
                (ctx, ct) => Task.CompletedTask,
                TestContext.Current.CancellationToken);
    }

    // -------------------------------------------------------------------------
    // RunAsync — Configure is applied
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_Configure_ActionIsInvoked()
    {
        var configureInvoked = false;

        await new NeedlrSerilogBootstrapper()
            .Configure(cfg =>
            {
                configureInvoked = true;
                cfg.WriteTo.Sink(new CapturingSink());
            })
            .RunAsync(
                (ctx, ct) => Task.CompletedTask,
                TestContext.Current.CancellationToken);

        Assert.True(configureInvoked);
    }

    [Fact]
    public async Task RunAsync_Configure_LogsRouteToConfiguredSink()
    {
        var sink = new CapturingSink();

        await new NeedlrSerilogBootstrapper()
            .Configure(cfg => cfg.WriteTo.Sink(sink))
            .RunAsync((ctx, ct) =>
            {
                ctx.Logger.LogInformation("hello from test");
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

        Assert.NotEmpty(sink.Events);
    }

    // -------------------------------------------------------------------------
    // RunAsync — exception handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_DoesNotRethrow_OnException()
    {
        // Must not propagate the exception.
        await new NeedlrSerilogBootstrapper()
            .Configure(cfg => cfg.WriteTo.Sink(new CapturingSink()))
            .RunAsync(
                (ctx, ct) => throw new InvalidOperationException("boom"),
                TestContext.Current.CancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

internal sealed class CapturingSink : ILogEventSink
{
    public readonly List<LogEvent> Events = [];

    public void Emit(LogEvent logEvent) => Events.Add(logEvent);
}
