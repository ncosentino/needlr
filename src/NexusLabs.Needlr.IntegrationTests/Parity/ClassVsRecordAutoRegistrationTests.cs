using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Contrast tests showing that classes ARE auto-registered while records are NOT.
/// </summary>
public sealed class ClassVsRecordAutoRegistrationTests
{
    [Fact]
    public void Reflection_ClassService_IsAutoRegistered()
    {
        var provider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var service = provider.GetService<IRecordService>();

        Assert.NotNull(service);
        Assert.IsType<ClassServiceImplementation>(service);
    }

    [Fact]
    public void SourceGen_ClassService_IsAutoRegistered()
    {
        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var service = provider.GetService<IRecordService>();

        Assert.NotNull(service);
        Assert.IsType<ClassServiceImplementation>(service);
    }

    [Fact]
    public void Parity_ClassVsRecord_OnlyClassIsAutoRegistered()
    {
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var reflectionServices = reflectionProvider.GetServices<IRecordService>().ToList();
        var sourceGenServices = sourceGenProvider.GetServices<IRecordService>().ToList();

        Assert.Single(reflectionServices);
        Assert.IsType<ClassServiceImplementation>(reflectionServices[0]);

        Assert.Single(sourceGenServices);
        Assert.IsType<ClassServiceImplementation>(sourceGenServices[0]);
    }
}
