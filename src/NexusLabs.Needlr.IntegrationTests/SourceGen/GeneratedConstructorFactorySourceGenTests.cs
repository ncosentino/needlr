using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.IntegrationTests.Generated;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Executable behavioral tests proving <c>[GenerateFactory]</c> combined with a
/// generated constructor (<c>[GenerateConstructor]</c> or a positive field-level
/// constructor guard trigger) produces a factory whose <c>Create</c> method and
/// <c>Func&lt;...&gt;</c> delegate correctly call the generated constructor with
/// injected and runtime fields bound to the right parameters -- including when field
/// declaration order interleaves injectable and runtime fields, which would bind
/// incorrectly (or fail to compile) under positional argument binding.
/// </summary>
public sealed class GeneratedConstructorFactorySourceGenTests
{
    [Fact]
    public void Factory_GeneratedConstructor_InterfaceCreatesInstanceWithInjectedAndRuntimeFields()
    {
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var factory = serviceProvider.GetRequiredService<IGcFactoryReportBuilderFactory>();

        var instance = factory.Create("acme-template");

        Assert.NotNull(instance.Repository);
        Assert.IsType<GcFactoryRepository>(instance.Repository);
        Assert.Equal("acme-template", instance.TemplateName);
    }

    [Fact]
    public void Factory_GeneratedConstructor_FuncCreatesInstanceWithInjectedAndRuntimeFields()
    {
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var create = serviceProvider.GetRequiredService<Func<string, GcFactoryReportBuilder>>();

        var instance = create("acme-template");

        Assert.NotNull(instance.Repository);
        Assert.Equal("acme-template", instance.TemplateName);
    }

    [Fact]
    public void Factory_GeneratedConstructor_ConcreteTypeIsNotDirectlyRegistered()
    {
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var service = serviceProvider.GetService<GcFactoryReportBuilder>();

        Assert.Null(service);
    }

    [Fact]
    public void Factory_GeneratedConstructor_InterleavedFieldOrder_BindsInjectedAndRuntimeFieldsCorrectly()
    {
        // GcFactoryInterleavedBuilder declares its runtime (string) field BEFORE its
        // injectable field, so the generated constructor's parameter order is
        // (templateName, repository) -- the opposite of the factory's
        // injectable-then-runtime argument grouping. This proves the generated call
        // site binds by parameter name rather than assuming that grouping order.
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var factory = serviceProvider.GetRequiredService<IGcFactoryInterleavedBuilderFactory>();

        var instance = factory.Create("interleaved-template");

        Assert.NotNull(instance.Repository);
        Assert.Equal("interleaved-template", instance.TemplateName);
    }

    [Fact]
    public void Factory_FieldTriggeredGeneration_CreatesInstanceWithGuardedRuntimeField()
    {
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var factory = serviceProvider.GetRequiredService<IGcFactoryTenantReportBuilderFactory>();

        var instance = factory.Create("acme");

        Assert.NotNull(instance.Repository);
        Assert.Equal("acme", instance.TenantName);

        var ex = Assert.Throws<ArgumentException>(() => factory.Create("   "));
        Assert.Equal("tenantName", ex.ParamName);
    }
}

public interface IGcFactoryRepository
{
}

public sealed class GcFactoryRepository : IGcFactoryRepository
{
}

[GenerateFactory]
[GenerateConstructor]
public partial class GcFactoryReportBuilder
{
    private readonly IGcFactoryRepository _repository;
    private readonly string _templateName;

    public IGcFactoryRepository Repository => _repository;

    public string TemplateName => _templateName;
}

[GenerateFactory]
[GenerateConstructor]
public partial class GcFactoryInterleavedBuilder
{
    private readonly string _templateName;
    private readonly IGcFactoryRepository _repository;

    public IGcFactoryRepository Repository => _repository;

    public string TemplateName => _templateName;
}

[GenerateFactory]
public partial class GcFactoryTenantReportBuilder
{
    private readonly IGcFactoryRepository _repository;

    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
    private readonly string _tenantName;

    public IGcFactoryRepository Repository => _repository;

    public string TenantName => _tenantName;
}
