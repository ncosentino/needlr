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

        // Expect exactly 2 hosted services: TestWorkerService and AnotherWorkerService (wrapped by decorator)
        Assert.Equal(2, hostedServices.Count);
        
        // All should be wrapped by TrackerHostedServiceDecorator
        Assert.All(hostedServices, hs => Assert.IsType<TrackerHostedServiceDecorator>(hs));
        
        // Verify the specific wrapped services
        var wrappedTypes = hostedServices
            .Cast<TrackerHostedServiceDecorator>()
            .Select(d => d.Wrapped.GetType())
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
        
        // All hosted services should be TrackerHostedServiceDecorator instances
        Assert.All(hostedServices, hs => Assert.IsType<TrackerHostedServiceDecorator>(hs));
        
        // Verify each decorator wraps the expected concrete type
        var decorators = hostedServices.Cast<TrackerHostedServiceDecorator>().ToList();
        
        var testWorkerDecorator = decorators.SingleOrDefault(d => d.Wrapped is TestWorkerService);
        Assert.NotNull(testWorkerDecorator);
        Assert.IsType<TestWorkerService>(testWorkerDecorator.Wrapped);
        
        var anotherWorkerDecorator = decorators.SingleOrDefault(d => d.Wrapped is AnotherWorkerService);
        Assert.NotNull(anotherWorkerDecorator);
        Assert.IsType<AnotherWorkerService>(anotherWorkerDecorator.Wrapped);
    }
}
