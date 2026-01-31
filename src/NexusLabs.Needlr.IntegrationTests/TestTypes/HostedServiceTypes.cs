namespace NexusLabs.Needlr.IntegrationTests;

public sealed class TestWorkerService : Microsoft.Extensions.Hosting.BackgroundService
{
    public bool Started { get; private set; }
    public bool Stopped { get; private set; }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Started = true;
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        Stopped = true;
        return base.StopAsync(cancellationToken);
    }
}

public sealed class AnotherWorkerService : Microsoft.Extensions.Hosting.BackgroundService
{
    public bool Started { get; private set; }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Started = true;
        return Task.CompletedTask;
    }
}

[DoNotAutoRegister]
public sealed class ExcludedWorkerService : Microsoft.Extensions.Hosting.BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

[DecoratorFor<Microsoft.Extensions.Hosting.IHostedService>(Order = 0)]
public sealed class TrackerHostedServiceDecorator(
    Microsoft.Extensions.Hosting.IHostedService _wrapped) :
    Microsoft.Extensions.Hosting.IHostedService
{
    public static int WrapCount { get; set; }
    public Microsoft.Extensions.Hosting.IHostedService Wrapped => _wrapped;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        WrapCount++;
        return _wrapped.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _wrapped.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Second level decorator for testing multi-level decoration.
/// </summary>
[DecoratorFor<Microsoft.Extensions.Hosting.IHostedService>(Order = 1)]
public sealed class LoggingHostedServiceDecorator(
    Microsoft.Extensions.Hosting.IHostedService _wrapped) :
    Microsoft.Extensions.Hosting.IHostedService
{
    public static int WrapCount { get; set; }
    public Microsoft.Extensions.Hosting.IHostedService Wrapped => _wrapped;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        WrapCount++;
        return _wrapped.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _wrapped.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Third level decorator for testing multi-level decoration.
/// </summary>
[DecoratorFor<Microsoft.Extensions.Hosting.IHostedService>(Order = 2)]
public sealed class MetricsHostedServiceDecorator(
    Microsoft.Extensions.Hosting.IHostedService _wrapped) :
    Microsoft.Extensions.Hosting.IHostedService
{
    public static int WrapCount { get; set; }
    public Microsoft.Extensions.Hosting.IHostedService Wrapped => _wrapped;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        WrapCount++;
        return _wrapped.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _wrapped.StopAsync(cancellationToken);
    }
}
