using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

public sealed class HostedServiceSourceGenTests
{
    [Fact]
    public void HostedService_BackgroundService_AutoRegistered()
    {
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var worker = serviceProvider.GetService<TestWorkerService>();
        Assert.NotNull(worker);
        Assert.IsType<TestWorkerService>(worker);
    }

    [Fact]
    public void HostedService_AnotherWorker_AutoRegistered()
    {
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var worker = serviceProvider.GetService<AnotherWorkerService>();
        Assert.NotNull(worker);
        Assert.IsType<AnotherWorkerService>(worker);
    }

    [Fact]
    public void HostedService_WithDoNotAutoRegister_NotRegistered()
    {
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var excluded = serviceProvider.GetService<ExcludedWorkerService>();
        Assert.Null(excluded);
    }

    [Fact]
    public void HostedService_ConcreteResolution_NotDecorated()
    {
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var worker = serviceProvider.GetRequiredService<TestWorkerService>();
        Assert.IsType<TestWorkerService>(worker);
    }

    [Fact]
    public void HostedService_GetAllHostedServices_ReturnsExpectedServices()
    {
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();

        // Expect exactly 2 hosted services: TestWorkerService and AnotherWorkerService (wrapped by decorators)
        Assert.Equal(2, hostedServices.Count);
        
        // Outermost decorator should be MetricsHostedServiceDecorator (Order=2)
        Assert.All(hostedServices, hs => Assert.IsType<MetricsHostedServiceDecorator>(hs));
        
        // Verify the specific wrapped services at the innermost level
        var wrappedTypes = hostedServices
            .Cast<MetricsHostedServiceDecorator>()
            .Select(GetInnermostService)
            .Select(s => s.GetType())
            .ToList();
        
        Assert.Contains(typeof(TestWorkerService), wrappedTypes);
        Assert.Contains(typeof(AnotherWorkerService), wrappedTypes);
        
        // ExcludedWorkerService must NOT be present
        Assert.DoesNotContain(typeof(ExcludedWorkerService), wrappedTypes);
    }

    [Fact]
    public void HostedService_DecoratorWrapsSpecificServices()
    {
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        
        // All hosted services should be MetricsHostedServiceDecorator instances (outermost)
        Assert.All(hostedServices, hs => Assert.IsType<MetricsHostedServiceDecorator>(hs));
        
        // Verify the full decorator chain
        foreach (var service in hostedServices.Cast<MetricsHostedServiceDecorator>())
        {
            Assert.IsType<LoggingHostedServiceDecorator>(service.Wrapped);
            var logging = (LoggingHostedServiceDecorator)service.Wrapped;
            Assert.IsType<TrackerHostedServiceDecorator>(logging.Wrapped);
        }
        
        // Verify innermost services are the expected worker services
        var innerServices = hostedServices
            .Cast<MetricsHostedServiceDecorator>()
            .Select(GetInnermostService)
            .ToList();
        
        var testWorker = innerServices.SingleOrDefault(s => s is TestWorkerService);
        Assert.NotNull(testWorker);
        
        var anotherWorker = innerServices.SingleOrDefault(s => s is AnotherWorkerService);
        Assert.NotNull(anotherWorker);
    }

    [Fact]
    public void HostedService_MultiLevelDecorators_AppliedInCorrectOrder()
    {
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        
        Assert.Equal(2, hostedServices.Count);
        
        // Verify decorator chain order for each hosted service
        foreach (var service in hostedServices)
        {
            // Outermost: MetricsHostedServiceDecorator (Order=2)
            Assert.IsType<MetricsHostedServiceDecorator>(service);
            var metrics = (MetricsHostedServiceDecorator)service;
            
            // Middle: LoggingHostedServiceDecorator (Order=1)
            Assert.IsType<LoggingHostedServiceDecorator>(metrics.Wrapped);
            var logging = (LoggingHostedServiceDecorator)metrics.Wrapped;
            
            // Inner: TrackerHostedServiceDecorator (Order=0)
            Assert.IsType<TrackerHostedServiceDecorator>(logging.Wrapped);
            var tracker = (TrackerHostedServiceDecorator)logging.Wrapped;
            
            // Innermost: Actual service
            Assert.True(
                tracker.Wrapped is TestWorkerService or AnotherWorkerService,
                $"Expected TestWorkerService or AnotherWorkerService but got {tracker.Wrapped.GetType().Name}");
        }
    }

    private static IHostedService GetInnermostService(MetricsHostedServiceDecorator metrics)
    {
        var logging = (LoggingHostedServiceDecorator)metrics.Wrapped;
        var tracker = (TrackerHostedServiceDecorator)logging.Wrapped;
        return tracker.Wrapped;
    }
}
