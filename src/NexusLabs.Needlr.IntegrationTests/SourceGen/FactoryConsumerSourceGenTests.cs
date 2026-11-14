using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.IntegrationTests.Generated;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

public sealed class FactoryConsumerSourceGenTests
{
    [Fact]
    public void FactoryConsumer_PrimaryConstructor_ResolvesAndCreatesInvocationOwnedObjects()
    {
        var provider = new Syringe()
            .UsingGeneratedComponents(
                TypeRegistry.GetInjectableTypes,
                TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        var consumer = provider.GetRequiredService<FactoryWidgetConsumer>();
        var first = consumer.Create(1);
        var second = consumer.Create(2);

        Assert.Equal(1, first.Number);
        Assert.Equal(2, second.Number);
        Assert.NotSame(first, second);
        Assert.Same(consumer, provider.GetRequiredService<FactoryWidgetConsumer>());
        Assert.Null(provider.GetService<FactoryWidget>());
        Assert.Same(
            provider.GetRequiredService<IFactoryWidgetFactory>(),
            provider.GetRequiredService<IFactoryWidgetFactory>());
    }

    [Fact]
    public void FactoryConsumer_GeneratedConstructor_ResolvesFactoryFromAssemblyNamespace()
    {
        var provider = new Syringe()
            .UsingGeneratedComponents(
                TypeRegistry.GetInjectableTypes,
                TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        var consumer = provider.GetRequiredService<GeneratedFactoryWidgetConsumer>();

        Assert.Equal(42, consumer.Create(42).Number);
    }
}

public interface IFactoryWidget
{
    int Number { get; }
}

[GenerateConstructor]
[GenerateFactory<IFactoryWidget>(Mode = FactoryGenerationMode.Interface)]
public sealed partial class FactoryWidget : IFactoryWidget
{
    private readonly int _number;

    public int Number => _number;
}

public sealed class FactoryWidgetConsumer(IFactoryWidgetFactory factory)
{
    public IFactoryWidget Create(int number) => factory.Create(number);
}

[GenerateConstructor]
public sealed partial class GeneratedFactoryWidgetConsumer
{
    private readonly global::NexusLabs.Needlr.IntegrationTests.Generated.IFactoryWidgetFactory _factory;

    public IFactoryWidget Create(int number) => _factory.Create(number);
}
