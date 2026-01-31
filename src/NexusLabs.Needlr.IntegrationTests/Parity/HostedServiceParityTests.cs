using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

public sealed class HostedServiceParityTests
{
    [Fact]
    public void Parity_BackgroundService_AutoRegistered()
    {
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sgWorker = sourceGenProvider.GetService<TestWorkerService>();
        var refWorker = reflectionProvider.GetService<TestWorkerService>();

        Assert.NotNull(sgWorker);
        Assert.NotNull(refWorker);
        Assert.IsType<TestWorkerService>(sgWorker);
        Assert.IsType<TestWorkerService>(refWorker);
    }

    [Fact]
    public void Parity_AnotherWorker_AutoRegistered()
    {
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sgWorker = sourceGenProvider.GetService<AnotherWorkerService>();
        var refWorker = reflectionProvider.GetService<AnotherWorkerService>();

        Assert.NotNull(sgWorker);
        Assert.NotNull(refWorker);
        Assert.IsType<AnotherWorkerService>(sgWorker);
        Assert.IsType<AnotherWorkerService>(refWorker);
    }

    [Fact]
    public void Parity_DoNotAutoRegister_ExcludedByBoth()
    {
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sgExcluded = sourceGenProvider.GetService<ExcludedWorkerService>();
        var refExcluded = reflectionProvider.GetService<ExcludedWorkerService>();

        Assert.Null(sgExcluded);
        Assert.Null(refExcluded);
    }

    [Fact]
    public void Parity_ConcreteResolution_NotDecoratedByBoth()
    {
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sgWorker = sourceGenProvider.GetRequiredService<TestWorkerService>();
        var refWorker = reflectionProvider.GetRequiredService<TestWorkerService>();

        Assert.IsType<TestWorkerService>(sgWorker);
        Assert.IsType<TestWorkerService>(refWorker);
    }

    [Fact]
    public void Parity_HostedServiceCount_IdenticalBetweenBoth()
    {
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sgHosted = sourceGenProvider.GetServices<IHostedService>().ToList();
        var refHosted = reflectionProvider.GetServices<IHostedService>().ToList();

        // Both must have exactly 2 hosted services
        Assert.Equal(2, sgHosted.Count);
        Assert.Equal(2, refHosted.Count);
        
        // Counts must be identical
        Assert.Equal(sgHosted.Count, refHosted.Count);
    }

    [Fact]
    public void Parity_HostedServiceTypes_IdenticalBetweenBoth()
    {
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sgHosted = sourceGenProvider.GetServices<IHostedService>().ToList();
        var refHosted = reflectionProvider.GetServices<IHostedService>().ToList();

        // Outermost decorator should be MetricsHostedServiceDecorator (Order=2)
        Assert.All(sgHosted, hs => Assert.IsType<MetricsHostedServiceDecorator>(hs));
        Assert.All(refHosted, hs => Assert.IsType<MetricsHostedServiceDecorator>(hs));
        
        // Get innermost wrapped types from source-gen
        var sgWrappedTypes = sgHosted
            .Cast<MetricsHostedServiceDecorator>()
            .Select(GetInnermostService)
            .Select(s => s.GetType())
            .OrderBy(t => t.Name)
            .ToList();
        
        // Get innermost wrapped types from reflection
        var refWrappedTypes = refHosted
            .Cast<MetricsHostedServiceDecorator>()
            .Select(GetInnermostService)
            .Select(s => s.GetType())
            .OrderBy(t => t.Name)
            .ToList();
        
        // Both must contain the same wrapped types
        Assert.Equal(sgWrappedTypes, refWrappedTypes);
        
        // Verify specific types are present in both
        Assert.Contains(typeof(TestWorkerService), sgWrappedTypes);
        Assert.Contains(typeof(AnotherWorkerService), sgWrappedTypes);
        Assert.Contains(typeof(TestWorkerService), refWrappedTypes);
        Assert.Contains(typeof(AnotherWorkerService), refWrappedTypes);
        
        // ExcludedWorkerService must NOT be present in either
        Assert.DoesNotContain(typeof(ExcludedWorkerService), sgWrappedTypes);
        Assert.DoesNotContain(typeof(ExcludedWorkerService), refWrappedTypes);
    }

    private static IHostedService GetInnermostService(MetricsHostedServiceDecorator metrics)
    {
        var logging = (LoggingHostedServiceDecorator)metrics.Wrapped;
        var tracker = (TrackerHostedServiceDecorator)logging.Wrapped;
        return tracker.Wrapped;
    }

    [Fact]
    public void Parity_DecoratorApplied_BothWrapSameServices()
    {
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sgHosted = sourceGenProvider.GetServices<IHostedService>().ToList();
        var refHosted = reflectionProvider.GetServices<IHostedService>().ToList();

        // Outermost decorator should be MetricsHostedServiceDecorator (Order=2)
        Assert.All(sgHosted, hs => Assert.IsType<MetricsHostedServiceDecorator>(hs));
        Assert.All(refHosted, hs => Assert.IsType<MetricsHostedServiceDecorator>(hs));
        
        // Verify the full decorator chain on source-gen
        var sgMetrics = sgHosted.Cast<MetricsHostedServiceDecorator>().ToList();
        foreach (var metric in sgMetrics)
        {
            Assert.IsType<LoggingHostedServiceDecorator>(metric.Wrapped);
            var logging = (LoggingHostedServiceDecorator)metric.Wrapped;
            Assert.IsType<TrackerHostedServiceDecorator>(logging.Wrapped);
        }
        
        // Verify the full decorator chain on reflection
        var refMetrics = refHosted.Cast<MetricsHostedServiceDecorator>().ToList();
        foreach (var metric in refMetrics)
        {
            Assert.IsType<LoggingHostedServiceDecorator>(metric.Wrapped);
            var logging = (LoggingHostedServiceDecorator)metric.Wrapped;
            Assert.IsType<TrackerHostedServiceDecorator>(logging.Wrapped);
        }
    }

    [Fact]
    public void Parity_MultiLevelDecorators_ChainIdenticalBetweenBoth()
    {
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sgHosted = sourceGenProvider.GetServices<IHostedService>().ToList();
        var refHosted = reflectionProvider.GetServices<IHostedService>().ToList();

        Assert.Equal(2, sgHosted.Count);
        Assert.Equal(2, refHosted.Count);

        // Unwrap the full chain for source-gen
        var sgChains = sgHosted.Select(GetDecoratorChain).OrderBy(c => c.InnerType.Name).ToList();
        var refChains = refHosted.Select(GetDecoratorChain).OrderBy(c => c.InnerType.Name).ToList();

        // Verify chain structure is identical
        for (int i = 0; i < sgChains.Count; i++)
        {
            Assert.Equal(sgChains[i].DecoratorTypes, refChains[i].DecoratorTypes);
            Assert.Equal(sgChains[i].InnerType, refChains[i].InnerType);
        }

        // Verify specific inner types
        var sgInnerTypes = sgChains.Select(c => c.InnerType).ToHashSet();
        var refInnerTypes = refChains.Select(c => c.InnerType).ToHashSet();
        
        Assert.Contains(typeof(TestWorkerService), sgInnerTypes);
        Assert.Contains(typeof(AnotherWorkerService), sgInnerTypes);
        Assert.Contains(typeof(TestWorkerService), refInnerTypes);
        Assert.Contains(typeof(AnotherWorkerService), refInnerTypes);
    }

    [Fact]
    public void Parity_MultiLevelDecorators_CorrectOrderApplied()
    {
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sgHosted = sourceGenProvider.GetServices<IHostedService>().First();
        var refHosted = reflectionProvider.GetServices<IHostedService>().First();

        // Expected chain (outermost to innermost):
        // MetricsHostedServiceDecorator (Order=2) -> 
        // LoggingHostedServiceDecorator (Order=1) -> 
        // TrackerHostedServiceDecorator (Order=0) -> 
        // Actual Service

        var sgChain = GetDecoratorChain(sgHosted);
        var refChain = GetDecoratorChain(refHosted);

        var expectedDecoratorOrder = new[]
        {
            typeof(MetricsHostedServiceDecorator),
            typeof(LoggingHostedServiceDecorator),
            typeof(TrackerHostedServiceDecorator)
        };

        Assert.Equal(expectedDecoratorOrder, sgChain.DecoratorTypes);
        Assert.Equal(expectedDecoratorOrder, refChain.DecoratorTypes);
    }

    private static (Type[] DecoratorTypes, Type InnerType) GetDecoratorChain(IHostedService service)
    {
        var decoratorTypes = new List<Type>();
        IHostedService current = service;

        while (current is MetricsHostedServiceDecorator metrics)
        {
            decoratorTypes.Add(typeof(MetricsHostedServiceDecorator));
            current = metrics.Wrapped;
        }

        while (current is LoggingHostedServiceDecorator logging)
        {
            decoratorTypes.Add(typeof(LoggingHostedServiceDecorator));
            current = logging.Wrapped;
        }

        while (current is TrackerHostedServiceDecorator tracker)
        {
            decoratorTypes.Add(typeof(TrackerHostedServiceDecorator));
            current = tracker.Wrapped;
        }

        return (decoratorTypes.ToArray(), current.GetType());
    }
}
