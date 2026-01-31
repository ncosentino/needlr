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

        // All should be wrapped by TrackerHostedServiceDecorator
        Assert.All(sgHosted, hs => Assert.IsType<TrackerHostedServiceDecorator>(hs));
        Assert.All(refHosted, hs => Assert.IsType<TrackerHostedServiceDecorator>(hs));
        
        // Get wrapped types from source-gen
        var sgWrappedTypes = sgHosted
            .Cast<TrackerHostedServiceDecorator>()
            .Select(d => d.Wrapped.GetType())
            .OrderBy(t => t.Name)
            .ToList();
        
        // Get wrapped types from reflection
        var refWrappedTypes = refHosted
            .Cast<TrackerHostedServiceDecorator>()
            .Select(d => d.Wrapped.GetType())
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

        // Verify source-gen has TestWorkerService wrapped
        var sgDecorators = sgHosted.Cast<TrackerHostedServiceDecorator>().ToList();
        var sgTestWorker = sgDecorators.SingleOrDefault(d => d.Wrapped is TestWorkerService);
        var sgAnotherWorker = sgDecorators.SingleOrDefault(d => d.Wrapped is AnotherWorkerService);
        Assert.NotNull(sgTestWorker);
        Assert.NotNull(sgAnotherWorker);
        
        // Verify reflection has TestWorkerService wrapped
        var refDecorators = refHosted.Cast<TrackerHostedServiceDecorator>().ToList();
        var refTestWorker = refDecorators.SingleOrDefault(d => d.Wrapped is TestWorkerService);
        var refAnotherWorker = refDecorators.SingleOrDefault(d => d.Wrapped is AnotherWorkerService);
        Assert.NotNull(refTestWorker);
        Assert.NotNull(refAnotherWorker);
    }
}
