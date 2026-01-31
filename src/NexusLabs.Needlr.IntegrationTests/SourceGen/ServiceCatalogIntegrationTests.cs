using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Catalog;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.IntegrationTests.Generated;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Tests that verify ServiceCatalog is generated and accessible at runtime via DI.
/// These tests prove the catalog can be queried in application code with specific, expected values.
/// </summary>
public sealed class ServiceCatalogIntegrationTests
{
    private static IServiceCatalog GetCatalog()
    {
        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();
        return provider.GetRequiredService<IServiceCatalog>();
    }

    [Fact]
    public void ServiceCatalog_IsResolvableFromDI()
    {
        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();
        
        var catalog = provider.GetRequiredService<IServiceCatalog>();
        
        Assert.NotNull(catalog);
        Assert.IsType<ServiceCatalog>(catalog);
    }

    [Fact]
    public void ServiceCatalog_MultipleResolutions_ReturnsSameInstance()
    {
        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();
        
        var catalog1 = provider.GetRequiredService<IServiceCatalog>();
        var catalog2 = provider.GetRequiredService<IServiceCatalog>();
        
        Assert.Same(catalog1, catalog2);
    }

    [Fact]
    public void ServiceCatalog_AssemblyName_IsCorrect()
    {
        var catalog = GetCatalog();
        
        Assert.Equal("NexusLabs.Needlr.IntegrationTests", catalog.AssemblyName);
    }

    [Fact]
    public void ServiceCatalog_GeneratedAt_HasValidFormat()
    {
        var catalog = GetCatalog();
        
        Assert.False(string.IsNullOrEmpty(catalog.GeneratedAt));
        // Should be a valid UTC timestamp format: yyyy-MM-dd HH:mm:ss
        Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}", catalog.GeneratedAt);
    }

    [Fact]
    public void ServiceCatalog_HostedServices_ContainsTestWorkerService()
    {
        var catalog = GetCatalog();
        
        var testWorker = catalog.HostedServices
            .SingleOrDefault(h => h.ShortTypeName == "TestWorkerService");
        
        Assert.NotNull(testWorker);
        Assert.Contains("TestWorkerService", testWorker.TypeName);
        Assert.Contains("NexusLabs.Needlr.IntegrationTests", testWorker.AssemblyName);
    }

    [Fact]
    public void ServiceCatalog_HostedServices_ContainsAnotherWorkerService()
    {
        var catalog = GetCatalog();
        
        var anotherWorker = catalog.HostedServices
            .SingleOrDefault(h => h.ShortTypeName == "AnotherWorkerService");
        
        Assert.NotNull(anotherWorker);
        Assert.Contains("AnotherWorkerService", anotherWorker.TypeName);
    }

    [Fact]
    public void ServiceCatalog_HostedServices_ExcludesExcludedWorkerService()
    {
        var catalog = GetCatalog();
        
        var excluded = catalog.HostedServices
            .SingleOrDefault(h => h.ShortTypeName == "ExcludedWorkerService");
        
        Assert.Null(excluded);
    }

    [Fact]
    public void ServiceCatalog_Decorators_ContainsTrackerHostedServiceDecorator()
    {
        var catalog = GetCatalog();
        
        var tracker = catalog.Decorators
            .SingleOrDefault(d => d.ShortDecoratorTypeName == "TrackerHostedServiceDecorator");
        
        Assert.NotNull(tracker);
        Assert.Contains("TrackerHostedServiceDecorator", tracker.DecoratorTypeName);
        Assert.Contains("IHostedService", tracker.ServiceTypeName);
        Assert.Equal(0, tracker.Order);
    }

    [Fact]
    public void ServiceCatalog_Decorators_ContainsLoggingHostedServiceDecorator()
    {
        var catalog = GetCatalog();
        
        var logging = catalog.Decorators
            .SingleOrDefault(d => d.ShortDecoratorTypeName == "LoggingHostedServiceDecorator");
        
        Assert.NotNull(logging);
        Assert.Contains("IHostedService", logging.ServiceTypeName);
        Assert.Equal(1, logging.Order);
    }

    [Fact]
    public void ServiceCatalog_Decorators_ContainsMetricsHostedServiceDecorator()
    {
        var catalog = GetCatalog();
        
        var metrics = catalog.Decorators
            .SingleOrDefault(d => d.ShortDecoratorTypeName == "MetricsHostedServiceDecorator");
        
        Assert.NotNull(metrics);
        Assert.Contains("IHostedService", metrics.ServiceTypeName);
        Assert.Equal(2, metrics.Order);
    }

    [Fact]
    public void ServiceCatalog_Decorators_ContainsDecoratorForTestServiceDecorators()
    {
        var catalog = GetCatalog();
        
        // Three decorators for IDecoratorForTestService with orders 0, 1, 2
        var zeroOrder = catalog.Decorators
            .SingleOrDefault(d => d.ShortDecoratorTypeName == "DecoratorForZeroOrderDecorator");
        var firstOrder = catalog.Decorators
            .SingleOrDefault(d => d.ShortDecoratorTypeName == "DecoratorForFirstDecorator");
        var secondOrder = catalog.Decorators
            .SingleOrDefault(d => d.ShortDecoratorTypeName == "DecoratorForSecondDecorator");
        
        Assert.NotNull(zeroOrder);
        Assert.Equal(0, zeroOrder.Order);
        Assert.Contains("IDecoratorForTestService", zeroOrder.ServiceTypeName);
        
        Assert.NotNull(firstOrder);
        Assert.Equal(1, firstOrder.Order);
        Assert.Contains("IDecoratorForTestService", firstOrder.ServiceTypeName);
        
        Assert.NotNull(secondOrder);
        Assert.Equal(2, secondOrder.Order);
        Assert.Contains("IDecoratorForTestService", secondOrder.ServiceTypeName);
    }

    [Fact]
    public void ServiceCatalog_Services_ContainsDecoratorForTestServiceImpl()
    {
        var catalog = GetCatalog();
        
        var impl = catalog.Services
            .SingleOrDefault(s => s.ShortTypeName == "DecoratorForTestServiceImpl");
        
        Assert.NotNull(impl);
        Assert.True(impl.Interfaces.Any(i => i.Contains("IDecoratorForTestService")), 
            $"Expected IDecoratorForTestService interface, got: {string.Join(", ", impl.Interfaces)}");
        // Parameterless constructor means Singleton by default
        Assert.Equal(ServiceCatalogLifetime.Singleton, impl.Lifetime);
    }

    [Fact]
    public void ServiceCatalog_Services_ContainsSingletonJobService()
    {
        var catalog = GetCatalog();
        
        var singletonJob = catalog.Services
            .SingleOrDefault(s => s.ShortTypeName == "SingletonJobService");
        
        Assert.NotNull(singletonJob);
        Assert.True(singletonJob.Interfaces.Any(i => i.Contains("ITestJob")),
            $"Expected ITestJob interface, got: {string.Join(", ", singletonJob.Interfaces)}");
        // Parameterless constructor means Singleton by default
        Assert.Equal(ServiceCatalogLifetime.Singleton, singletonJob.Lifetime);
    }

    [Fact]
    public void ServiceCatalog_Services_ContainsRegularSingletonService()
    {
        var catalog = GetCatalog();
        
        var regularSingleton = catalog.Services
            .SingleOrDefault(s => s.ShortTypeName == "RegularSingletonService");
        
        Assert.NotNull(regularSingleton);
        // Parameterless constructor means Singleton
        Assert.Equal(ServiceCatalogLifetime.Singleton, regularSingleton.Lifetime);
    }

    [Fact]
    public void ServiceCatalog_Decorators_ExcludesDoNotAutoRegisterTypes()
    {
        var catalog = GetCatalog();
        
        // ManualDecorator and AttributeDecorator have [DoNotAutoRegister]
        var manualDecorator = catalog.Decorators
            .SingleOrDefault(d => d.ShortDecoratorTypeName == "ManualDecorator");
        var attributeDecorator = catalog.Decorators
            .SingleOrDefault(d => d.ShortDecoratorTypeName == "AttributeDecorator");
        
        Assert.Null(manualDecorator);
        Assert.Null(attributeDecorator);
    }

    [Fact]
    public void ServiceCatalog_Services_ExcludesDoNotAutoRegisterTypes()
    {
        var catalog = GetCatalog();
        
        // ManualAndAttributeDecoratorServiceImpl has [DoNotAutoRegister]
        var excluded = catalog.Services
            .SingleOrDefault(s => s.ShortTypeName == "ManualAndAttributeDecoratorServiceImpl");
        
        Assert.Null(excluded);
    }

    [Fact]
    public void ServiceCatalog_Plugins_Collection_IsNotNull()
    {
        var catalog = GetCatalog();
        
        Assert.NotNull(catalog.Plugins);
        // The integration test project has plugins defined
    }

    [Fact]
    public void ServiceCatalog_Options_Collection_IsNotNull()
    {
        var catalog = GetCatalog();
        
        Assert.NotNull(catalog.Options);
    }

    [Fact]
    public void ServiceCatalog_InterceptedServices_Collection_IsNotNull()
    {
        var catalog = GetCatalog();
        
        Assert.NotNull(catalog.InterceptedServices);
    }

    [Fact]
    public void ServiceCatalog_CanQueryServicesImplementingInterface()
    {
        var catalog = GetCatalog();
        
        // Find all services implementing ITestJob (interface names include global:: prefix)
        var jobServices = catalog.Services
            .Where(s => s.Interfaces.Any(i => i.Contains("ITestJob")))
            .ToList();
        
        // Should include SingletonJobService and AnotherSingletonJob
        Assert.True(jobServices.Count >= 2, 
            $"Expected at least 2 ITestJob implementations, found {jobServices.Count}: {string.Join(", ", jobServices.Select(s => s.ShortTypeName))}");
        
        Assert.Contains(jobServices, s => s.ShortTypeName == "SingletonJobService");
        Assert.Contains(jobServices, s => s.ShortTypeName == "AnotherSingletonJob");
    }

    [Fact]
    public void ServiceCatalog_CanQueryDecoratorsByServiceType()
    {
        var catalog = GetCatalog();
        
        // Find all decorators for IHostedService
        var hostedServiceDecorators = catalog.Decorators
            .Where(d => d.ServiceTypeName.Contains("IHostedService"))
            .OrderBy(d => d.Order)
            .ToList();
        
        // Should have 3 decorators in order: Tracker(0), Logging(1), Metrics(2)
        Assert.Equal(3, hostedServiceDecorators.Count);
        Assert.Equal("TrackerHostedServiceDecorator", hostedServiceDecorators[0].ShortDecoratorTypeName);
        Assert.Equal("LoggingHostedServiceDecorator", hostedServiceDecorators[1].ShortDecoratorTypeName);
        Assert.Equal("MetricsHostedServiceDecorator", hostedServiceDecorators[2].ShortDecoratorTypeName);
    }
}

