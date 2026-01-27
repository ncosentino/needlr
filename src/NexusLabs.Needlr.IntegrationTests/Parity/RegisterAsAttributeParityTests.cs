using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Parity tests that verify [RegisterAs&lt;T&gt;] attribute handling is consistent
/// between reflection and source-generation paths. These tests compare both paths
/// to ensure they produce equivalent results.
/// </summary>
public sealed class RegisterAsAttributeParityTests
{
    [Fact]
    public void SingleRegisterAs_OnlySpecifiedInterfaceResolvable_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act - Get all IRegisterAsReader implementations
        var reflectionReaders = reflectionProvider.GetServices<IRegisterAsReader>().ToList();
        var sourceGenReaders = sourceGenProvider.GetServices<IRegisterAsReader>().ToList();

        // Assert - SingleRegisterAsService should be in the collection
        Assert.Contains(reflectionReaders, r => r is SingleRegisterAsService);
        Assert.Contains(sourceGenReaders, r => r is SingleRegisterAsService);
        
        // And we can verify the actual Read() method works
        var reflectionSingle = reflectionReaders.OfType<SingleRegisterAsService>().Single();
        var sourceGenSingle = sourceGenReaders.OfType<SingleRegisterAsService>().Single();
        Assert.Equal("Read", reflectionSingle.Read());
        Assert.Equal("Read", sourceGenSingle.Read());
    }

    [Fact]
    public void SingleRegisterAs_OtherInterfacesNotRegistered_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act - Resolve ALL implementations of IRegisterAsWriter and IRegisterAsLogger
        // SingleRegisterAsService should NOT appear in these collections
        var reflectionWriters = reflectionProvider.GetServices<IRegisterAsWriter>().ToList();
        var sourceGenWriters = sourceGenProvider.GetServices<IRegisterAsWriter>().ToList();
        var reflectionLoggers = reflectionProvider.GetServices<IRegisterAsLogger>().ToList();
        var sourceGenLoggers = sourceGenProvider.GetServices<IRegisterAsLogger>().ToList();

        // Assert - SingleRegisterAsService should NOT be registered as IRegisterAsWriter or IRegisterAsLogger
        Assert.DoesNotContain(reflectionWriters, w => w is SingleRegisterAsService);
        Assert.DoesNotContain(sourceGenWriters, w => w is SingleRegisterAsService);
        Assert.DoesNotContain(reflectionLoggers, l => l is SingleRegisterAsService);
        Assert.DoesNotContain(sourceGenLoggers, l => l is SingleRegisterAsService);
    }

    [Fact]
    public void SingleRegisterAs_ConcreteTypeStillResolvable_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act - Concrete type should still be resolvable
        var reflectionConcrete = reflectionProvider.GetService<SingleRegisterAsService>();
        var sourceGenConcrete = sourceGenProvider.GetService<SingleRegisterAsService>();

        // Assert
        Assert.NotNull(reflectionConcrete);
        Assert.NotNull(sourceGenConcrete);
    }

    [Fact]
    public void MultipleRegisterAs_AllSpecifiedInterfacesResolvable_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act - Both specified interfaces should be resolvable
        var reflectionReaders = reflectionProvider.GetServices<IRegisterAsReader>().ToList();
        var sourceGenReaders = sourceGenProvider.GetServices<IRegisterAsReader>().ToList();
        var reflectionWriters = reflectionProvider.GetServices<IRegisterAsWriter>().ToList();
        var sourceGenWriters = sourceGenProvider.GetServices<IRegisterAsWriter>().ToList();

        // Assert - MultipleRegisterAsService should be registered as both IRegisterAsReader and IRegisterAsWriter
        Assert.Contains(reflectionReaders, r => r is MultipleRegisterAsService);
        Assert.Contains(sourceGenReaders, r => r is MultipleRegisterAsService);
        Assert.Contains(reflectionWriters, w => w is MultipleRegisterAsService);
        Assert.Contains(sourceGenWriters, w => w is MultipleRegisterAsService);
    }

    [Fact]
    public void MultipleRegisterAs_UnspecifiedInterfaceNotRegistered_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act - IRegisterAsLogger should NOT include MultipleRegisterAsService
        var reflectionLoggers = reflectionProvider.GetServices<IRegisterAsLogger>().ToList();
        var sourceGenLoggers = sourceGenProvider.GetServices<IRegisterAsLogger>().ToList();

        // Assert
        Assert.DoesNotContain(reflectionLoggers, l => l is MultipleRegisterAsService);
        Assert.DoesNotContain(sourceGenLoggers, l => l is MultipleRegisterAsService);
    }

    [Fact]
    public void NoRegisterAs_AllInterfacesRegistered_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act - NoRegisterAsService has no [RegisterAs], so all interfaces should be registered
        var reflectionReaders = reflectionProvider.GetServices<IRegisterAsReader>().ToList();
        var sourceGenReaders = sourceGenProvider.GetServices<IRegisterAsReader>().ToList();
        var reflectionWriters = reflectionProvider.GetServices<IRegisterAsWriter>().ToList();
        var sourceGenWriters = sourceGenProvider.GetServices<IRegisterAsWriter>().ToList();

        // Assert - NoRegisterAsService should be in both collections
        Assert.Contains(reflectionReaders, r => r is NoRegisterAsService);
        Assert.Contains(sourceGenReaders, r => r is NoRegisterAsService);
        Assert.Contains(reflectionWriters, w => w is NoRegisterAsService);
        Assert.Contains(sourceGenWriters, w => w is NoRegisterAsService);
    }

    [Fact]
    public void RegisterAsBaseInterface_RegistersAsBase_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act - Should be resolvable as base interface
        var reflectionBase = reflectionProvider.GetService<IRegisterAsBaseService>();
        var sourceGenBase = sourceGenProvider.GetService<IRegisterAsBaseService>();

        // Assert
        Assert.NotNull(reflectionBase);
        Assert.NotNull(sourceGenBase);
        Assert.IsType<RegisterAsBaseOnlyService>(reflectionBase);
        Assert.IsType<RegisterAsBaseOnlyService>(sourceGenBase);
    }

    [Fact]
    public void RegisterAsBaseInterface_ChildInterfaceNotRegistered_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act - Child interface should NOT have RegisterAsBaseOnlyService
        var reflectionChildren = reflectionProvider.GetServices<IRegisterAsChildService>().ToList();
        var sourceGenChildren = sourceGenProvider.GetServices<IRegisterAsChildService>().ToList();

        // Assert - RegisterAsBaseOnlyService should NOT be registered as IRegisterAsChildService
        Assert.DoesNotContain(reflectionChildren, c => c is RegisterAsBaseOnlyService);
        Assert.DoesNotContain(sourceGenChildren, c => c is RegisterAsBaseOnlyService);
    }

    [Fact]
    public void RegisterAs_SameSingletonInstance_ParityBetweenReflectionAndSourceGen()
    {
        // Arrange - Default lifetime is Singleton
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Act - Resolve via interface and concrete type
        var reflectionViaInterface = reflectionProvider.GetRequiredService<IRegisterAsReader>();
        var reflectionViaConcrete = reflectionProvider.GetRequiredService<SingleRegisterAsService>();
        var sourceGenViaInterface = sourceGenProvider.GetRequiredService<IRegisterAsReader>();
        var sourceGenViaConcrete = sourceGenProvider.GetRequiredService<SingleRegisterAsService>();

        // Assert - Same singleton instance regardless of resolution path
        // Filter to get the SingleRegisterAsService specifically (there might be multiple IRegisterAsReader implementations)
        var reflectionFiltered = reflectionProvider.GetServices<IRegisterAsReader>()
            .OfType<SingleRegisterAsService>().Single();
        var sourceGenFiltered = sourceGenProvider.GetServices<IRegisterAsReader>()
            .OfType<SingleRegisterAsService>().Single();

        Assert.Same(reflectionFiltered, reflectionViaConcrete);
        Assert.Same(sourceGenFiltered, sourceGenViaConcrete);
    }
}
