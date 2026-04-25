using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.AspNet.Tests;

public sealed class HostedServiceRuntimeTests
{
    private static WebApplication BuildTestApp()
    {
        var app = new Syringe()
            .UsingReflection()
            .ForWebApplication()
            .UsingConfigurationCallback((builder, _) =>
                builder.WebHost.UseUrls("http://127.0.0.1:0"))
            .BuildWebApplication();
        return app;
    }

    [Fact]
    public async Task HostedService_ExecutesOnWebApplicationStart()
    {
        var app = BuildTestApp();

        var workerService = app.Services.GetRequiredService<TestBackgroundWorker>();
        Assert.False(workerService.ExecuteCalled, "ExecuteAsync should not be called before app starts");

        await app.StartAsync(TestContext.Current.CancellationToken);
        await workerService.ExecuteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        
        Assert.True(workerService.ExecuteCalled, "ExecuteAsync should be called after app starts");
        
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HostedService_StopsOnWebApplicationStop()
    {
        var app = BuildTestApp();

        var workerService = app.Services.GetRequiredService<TestBackgroundWorker>();

        await app.StartAsync(TestContext.Current.CancellationToken);
        await workerService.ExecuteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        
        Assert.False(workerService.StopCalled, "StopAsync should not be called before app stops");
        
        await app.StopAsync(TestContext.Current.CancellationToken);
        
        Assert.True(workerService.StopCalled, "StopAsync should be called after app stops");
    }

    [Fact]
    public async Task HostedService_MultipleServicesAllExecute()
    {
        var app = BuildTestApp();

        var worker1 = app.Services.GetRequiredService<TestBackgroundWorker>();
        var worker2 = app.Services.GetRequiredService<AnotherBackgroundWorker>();

        Assert.False(worker1.ExecuteCalled);
        Assert.False(worker2.ExecuteCalled);

        await app.StartAsync(TestContext.Current.CancellationToken);
        
        await Task.WhenAll(
            worker1.ExecuteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
            worker2.ExecuteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.True(worker1.ExecuteCalled, "TestBackgroundWorker.ExecuteAsync should be called");
        Assert.True(worker2.ExecuteCalled, "AnotherBackgroundWorker.ExecuteAsync should be called");

        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HostedService_ResolvedViaIHostedService_ExecutesCorrectly()
    {
        var app = BuildTestApp();

        var hostedServices = app.Services.GetServices<IHostedService>().ToList();
        
        // WebApplication has additional built-in hosted services
        Assert.True(hostedServices.Count >= 2, $"Expected at least 2 hosted services but got {hostedServices.Count}");
        
        var worker1 = app.Services.GetRequiredService<TestBackgroundWorker>();
        var worker2 = app.Services.GetRequiredService<AnotherBackgroundWorker>();

        await app.StartAsync(TestContext.Current.CancellationToken);
        
        await Task.WhenAll(
            worker1.ExecuteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
            worker2.ExecuteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.True(worker1.ExecuteCalled, "TestBackgroundWorker should have executed");
        Assert.True(worker2.ExecuteCalled, "AnotherBackgroundWorker should have executed");

        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HostedService_WithDoNotAutoRegister_DoesNotExecute()
    {
        var app = BuildTestApp();

        var excluded = app.Services.GetService<ExcludedBackgroundWorker>();
        Assert.Null(excluded);

        var hostedServices = app.Services.GetServices<IHostedService>().ToList();
        Assert.DoesNotContain(hostedServices, hs => hs is ExcludedBackgroundWorker);

        await app.StartAsync(TestContext.Current.CancellationToken);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }
}

public sealed class TestBackgroundWorker : BackgroundService
{
    public bool ExecuteCalled { get; private set; }
    public bool StopCalled { get; private set; }
    public TaskCompletionSource ExecuteStarted { get; } = new();

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ExecuteCalled = true;
        ExecuteStarted.TrySetResult();
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        StopCalled = true;
        return base.StopAsync(cancellationToken);
    }
}

public sealed class AnotherBackgroundWorker : BackgroundService
{
    public bool ExecuteCalled { get; private set; }
    public TaskCompletionSource ExecuteStarted { get; } = new();

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ExecuteCalled = true;
        ExecuteStarted.TrySetResult();
        return Task.CompletedTask;
    }
}

[DoNotAutoRegister]
public sealed class ExcludedBackgroundWorker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
