using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Integration tests for [RegisterClosedOverImplementationsOf] using source-generated discovery.
/// The generator runs at build time over this assembly; these tests build a real Syringe service
/// provider and verify that one closed composition is registered per discovered definition, exposed
/// as the facade, with constructor dependencies resolved from DI.
/// </summary>
public sealed class RegisterClosedOverImplementationsOfSourceGenTests
{
    private static IServiceProvider BuildProvider() =>
        new Syringe()
            .UsingSourceGen()
            .UsingPostPluginRegistrationCallback(services =>
                services.AddSingleton(typeof(IShapeStore<>), typeof(DefaultShapeStore<>)))
            .BuildServiceProvider();

    [Fact]
    public void Composition_RegistersOneClosedCompositionPerDiscoveredDefinition()
    {
        var provider = BuildProvider();

        var shapes = provider.GetServices<IShape>().ToList();

        Assert.Equal(2, shapes.Count);
        Assert.Contains(shapes, s => s.Name == "circle");
        Assert.Contains(shapes, s => s.Name == "square");
    }

    [Fact]
    public void Composition_ResolvesConstructorDependenciesClosedOverTypeArgument()
    {
        var provider = BuildProvider();

        var shapes = provider.GetServices<IShape>().ToList();

        // Each facade is a closed composition (not a definition), and resolution succeeding proves each
        // composition received its definition and store closed over the same type argument.
        Assert.All(shapes, s => Assert.Equal(typeof(ShapeCore<>), s.GetType().GetGenericTypeDefinition()));
    }

    [Fact]
    public void Composition_EnumerationRoutesByDiscriminatorWithoutKnowingTypeArgument()
    {
        var provider = BuildProvider();

        var router = new ShapeRouter(provider.GetServices<IShape>());

        Assert.Equal("square", router.Resolve("square").Name);
    }
}

/// <summary>Definition contract auto-discovered and registered by Needlr per closed type argument.</summary>
public interface IShapeDefinition<TData>
    where TData : class
{
    string Name { get; }
}

/// <summary>Per-type-argument store, registered as an open generic by a plugin/callback.</summary>
public interface IShapeStore<TData>
    where TData : class
{
}

/// <summary>Default open-generic store implementation.</summary>
public sealed class DefaultShapeStore<TData> : IShapeStore<TData>
    where TData : class
{
}

/// <summary>Non-generic facade the rest of the system consumes as IEnumerable&lt;IShape&gt;.</summary>
public interface IShape
{
    string Name { get; }
}

/// <summary>Marker data type for the circle definition.</summary>
public sealed class CircleData
{
}

/// <summary>Marker data type for the square definition.</summary>
public sealed class SquareData
{
}

/// <summary>Concrete, unattributed definition auto-registered as IShapeDefinition&lt;CircleData&gt;.</summary>
public sealed class CircleDefinition : IShapeDefinition<CircleData>
{
    public string Name => "circle";
}

/// <summary>Concrete, unattributed definition auto-registered as IShapeDefinition&lt;SquareData&gt;.</summary>
public sealed class SquareDefinition : IShapeDefinition<SquareData>
{
    public string Name => "square";
}

/// <summary>
/// Reusable composition closed per discovered TData and exposed as IShape. Composed from a definition
/// and a per-type store with no inheritance. Not auto-registered (open generic); the generator closes
/// and registers it once per discovered IShapeDefinition&lt;&gt; implementation.
/// </summary>
[RegisterClosedOverImplementationsOf(typeof(IShapeDefinition<>), As = typeof(IShape))]
public sealed class ShapeCore<TData> : IShape
    where TData : class
{
    private readonly IShapeDefinition<TData> _definition;

    public ShapeCore(IShapeDefinition<TData> definition, IShapeStore<TData> store)
    {
        _definition = definition;
        _ = store;
    }

    public string Name => _definition.Name;
}

/// <summary>Consumer that routes over the facade enumeration without knowing any TData.</summary>
public sealed class ShapeRouter
{
    private readonly IEnumerable<IShape> _shapes;

    public ShapeRouter(IEnumerable<IShape> shapes)
    {
        _shapes = shapes;
    }

    public IShape Resolve(string name) => _shapes.Single(s => s.Name == name);
}
