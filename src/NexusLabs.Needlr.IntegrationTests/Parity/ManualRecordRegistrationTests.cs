using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Tests proving that manually registered records still work correctly.
/// </summary>
public sealed class ManualRecordRegistrationTests
{
    [Fact]
    public void Reflection_ManuallyRegisteredRecord_CanBeResolved()
    {
        var record = new RecordServiceImplementation("TestData");

        var provider = new Syringe()
            .UsingReflection()
            .UsingPostPluginRegistrationCallback(services => 
                services.AddSingleton<IRecordService>(record))
            .BuildServiceProvider();

        var resolved = provider.GetRequiredService<IRecordService>();

        Assert.NotNull(resolved);
        Assert.IsType<RecordServiceImplementation>(resolved);
        Assert.Equal("TestData", resolved.GetData());
    }

    [Fact]
    public void SourceGen_ManuallyRegisteredRecord_CanBeResolved()
    {
        var record = new RecordServiceImplementation("TestData");

        var provider = new Syringe()
            .UsingSourceGen()
            .UsingPostPluginRegistrationCallback(services => 
                services.AddSingleton<IRecordService>(record))
            .BuildServiceProvider();

        var resolved = provider.GetRequiredService<IRecordService>();

        Assert.NotNull(resolved);
        Assert.IsType<RecordServiceImplementation>(resolved);
        Assert.Equal("TestData", resolved.GetData());
    }

    [Fact]
    public void Parity_ManuallyRegisteredRecord_BothPathsResolveCorrectly()
    {
        var record = new RecordServiceImplementation("SharedData");

        var reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingPostPluginRegistrationCallback(services => 
                services.AddSingleton<IRecordService>(record))
            .BuildServiceProvider();

        var sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .UsingPostPluginRegistrationCallback(services => 
                services.AddSingleton<IRecordService>(record))
            .BuildServiceProvider();

        var reflectionResolved = reflectionProvider.GetRequiredService<IRecordService>();
        var sourceGenResolved = sourceGenProvider.GetRequiredService<IRecordService>();

        Assert.Same(record, reflectionResolved);
        Assert.Same(record, sourceGenResolved);
    }

    [Fact]
    public void ManuallyRegisteredRecord_WithRequiredMembers_CanBeResolved()
    {
        var services = new ServiceCollection();
        var record = new RecordWithRequiredMembers { Data = "RequiredData" };
        services.AddSingleton(record);

        var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredService<RecordWithRequiredMembers>();

        Assert.NotNull(resolved);
        Assert.Equal("RequiredData", resolved.Data);
    }

    [Fact]
    public void ManuallyRegisteredRecord_AsFactory_CanBeResolved()
    {
        var provider = new Syringe()
            .UsingSourceGen()
            .UsingPostPluginRegistrationCallback(services => 
                services.AddTransient<IRecordService>(_ => new RecordServiceImplementation("FactoryCreated")))
            .BuildServiceProvider();

        var resolved = provider.GetRequiredService<IRecordService>();

        Assert.NotNull(resolved);
        Assert.IsType<RecordServiceImplementation>(resolved);
        Assert.Equal("FactoryCreated", resolved.GetData());
    }
}
