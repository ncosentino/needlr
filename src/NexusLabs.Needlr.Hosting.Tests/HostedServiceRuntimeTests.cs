using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Hosting.Tests;

public sealed class HostedServiceRuntimeTests
{
    [Fact]
    public async Task HostedService_ExecutesOnHostStart()
    {
        using var host = new Syringe()
            .UsingReflection()
            .ForHost()
            .BuildHost();

        var workerService = host.Services.GetRequiredService<TestBackgroundWorker>();
        Assert.False(workerService.ExecuteCalled, "ExecuteAsync should not be called before host starts");

        await host.StartAsync(TestContext.Current.CancellationToken);
        await workerService.ExecuteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        
        Assert.True(workerService.ExecuteCalled, "ExecuteAsync should be called after host starts");
        
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HostedService_StopsOnHostStop()
    {
        using var host = new Syringe()
            .UsingReflection()
            .ForHost()
            .BuildHost();

        var workerService = host.Services.GetRequiredService<TestBackgroundWorker>();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await workerService.ExecuteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        
        Assert.False(workerService.StopCalled, "StopAsync should not be called before host stops");
        
        await host.StopAsync(TestContext.Current.CancellationToken);
        
        Assert.True(workerService.StopCalled, "StopAsync should be called after host stops");
    }

    [Fact]
    public async Task HostedService_MultipleServicesAllExecute()
    {
        using var host = new Syringe()
            .UsingReflection()
            .ForHost()
            .BuildHost();

        var worker1 = host.Services.GetRequiredService<TestBackgroundWorker>();
        var worker2 = host.Services.GetRequiredService<AnotherBackgroundWorker>();

        Assert.False(worker1.ExecuteCalled);
        Assert.False(worker2.ExecuteCalled);

        await host.StartAsync(TestContext.Current.CancellationToken);
        
        await Task.WhenAll(
            worker1.ExecuteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
            worker2.ExecuteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.True(worker1.ExecuteCalled, "TestBackgroundWorker.ExecuteAsync should be called");
        Assert.True(worker2.ExecuteCalled, "AnotherBackgroundWorker.ExecuteAsync should be called");

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HostedService_ResolvedViaIHostedService_ExecutesCorrectly()
    {
        using var host = new Syringe()
            .UsingReflection()
            .ForHost()
            .BuildHost();

        var hostedServices = host.Services.GetServices<IHostedService>().ToList();
        Assert.Equal(2, hostedServices.Count);
        
        var worker1 = host.Services.GetRequiredService<TestBackgroundWorker>();
        var worker2 = host.Services.GetRequiredService<AnotherBackgroundWorker>();

        await host.StartAsync(TestContext.Current.CancellationToken);
        
        await Task.WhenAll(
            worker1.ExecuteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
            worker2.ExecuteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.True(worker1.ExecuteCalled, "TestBackgroundWorker should have executed");
        Assert.True(worker2.ExecuteCalled, "AnotherBackgroundWorker should have executed");

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HostedService_WithDoNotAutoRegister_DoesNotExecute()
    {
        using var host = new Syringe()
            .UsingReflection()
            .ForHost()
            .BuildHost();

        var excluded = host.Services.GetService<ExcludedBackgroundWorker>();
        Assert.Null(excluded);

        var hostedServices = host.Services.GetServices<IHostedService>().ToList();
        Assert.DoesNotContain(hostedServices, hs => hs is ExcludedBackgroundWorker);

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
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
