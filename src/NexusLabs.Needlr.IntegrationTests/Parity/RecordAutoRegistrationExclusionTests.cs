using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Parity tests proving that records are NEVER auto-registered as services.
/// Tests both reflection and source-generation paths.
/// </summary>
public sealed class RecordAutoRegistrationExclusionTests
{
    [Fact]
    public void Reflection_RecordImplementingInterface_NotAutoRegistered()
    {
        var provider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var recordAsSelf = provider.GetService<RecordServiceImplementation>();

        Assert.Null(recordAsSelf);
    }

    [Fact]
    public void SourceGen_RecordImplementingInterface_NotAutoRegistered()
    {
        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var recordAsSelf = provider.GetService<RecordServiceImplementation>();

        Assert.Null(recordAsSelf);
    }

    [Fact]
    public void Parity_RecordImplementingInterface_BothExcludeFromAutoRegistration()
    {
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var reflectionRecord = reflectionProvider.GetService<RecordServiceImplementation>();
        var sourceGenRecord = sourceGenProvider.GetService<RecordServiceImplementation>();

        Assert.Null(reflectionRecord);
        Assert.Null(sourceGenRecord);
    }

    [Fact]
    public void Reflection_RecordWithRequiredMembers_NotAutoRegistered()
    {
        var provider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        var record = provider.GetService<RecordWithRequiredMembers>();

        Assert.Null(record);
    }

    [Fact]
    public void SourceGen_RecordWithRequiredMembers_NotAutoRegistered()
    {
        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var record = provider.GetService<RecordWithRequiredMembers>();

        Assert.Null(record);
    }

    [Fact]
    public void Parity_SimpleDataRecord_BothExcludeFromAutoRegistration()
    {
        var reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();
        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var reflectionRecord = reflectionProvider.GetService<SimpleDataRecord>();
        var sourceGenRecord = sourceGenProvider.GetService<SimpleDataRecord>();

        Assert.Null(reflectionRecord);
        Assert.Null(sourceGenRecord);
    }
}
