using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Activation coverage for [RegisterClosedOverImplementationsOf]: every closed composition the
/// generator emits must actually resolve through a real provider as the configured facade, with
/// dependencies closed over the same type argument, keyed dependencies bound to the exact key,
/// and the configured lifetime's identity semantics preserved across additive markers.
/// </summary>
public sealed class RegisterClosedOverImplementationsOfActivationTests
{
    private static IServiceProvider BuildProvider() =>
        new Syringe()
            .UsingSourceGen()
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddKeyedScoped(typeof(IGadgetStore<>), "primary", typeof(PrimaryGadgetStore<>));
                services.AddKeyedScoped(typeof(IGadgetStore<>), "fallback", typeof(FallbackGadgetStore<>));
            })
            .BuildServiceProvider();

    [Fact]
    public void Composition_ResolvesEveryClosedCompositionAsTheConfiguredFacade()
    {
        var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var gadgets = scope.ServiceProvider.GetServices<IGadget>().ToList();

        Assert.Equal(2, gadgets.Count);
        Assert.All(gadgets, g => Assert.Equal(typeof(GadgetCore<>), g.GetType().GetGenericTypeDefinition()));
        Assert.Contains(gadgets, g => g.Name == "gear");
        Assert.Contains(gadgets, g => g.Name == "lever");
    }

    [Fact]
    public void Composition_ClosesEveryConstructorDependencyOverTheSameTypeArgument()
    {
        var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var gadgets = scope.ServiceProvider.GetServices<IGadget>().ToList();

        Assert.Equal(2, gadgets.Count);
        Assert.Equal(typeof(GearData), Assert.Single(gadgets, g => g.Name == "gear").DataType);
        Assert.Equal(typeof(LeverData), Assert.Single(gadgets, g => g.Name == "lever").DataType);
        Assert.All(gadgets, g => Assert.Equal(g.DataType, g.StoreDataType));
    }

    [Fact]
    public void Composition_ResolvesKeyedConstructorDependencyWithTheExactKey()
    {
        var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var gadgets = scope.ServiceProvider.GetServices<IGadget>().ToList();

        Assert.Equal(2, gadgets.Count);
        Assert.All(gadgets, g => Assert.Equal("primary", g.StoreKey));
        Assert.Equal(
            "fallback",
            scope.ServiceProvider.GetRequiredKeyedService<IGadgetStore<GearData>>("fallback").Key);
    }

    [Fact]
    public void Composition_ScopedLifetime_SharesInstanceWithinScopeAndNotAcrossScopes()
    {
        var provider = BuildProvider();

        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetServices<IGadget>().Single(g => g.Name == "gear");
        var firstAgain = firstScope.ServiceProvider.GetServices<IGadget>().Single(g => g.Name == "gear");
        var second = secondScope.ServiceProvider.GetServices<IGadget>().Single(g => g.Name == "gear");

        Assert.Same(first, firstAgain);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void Composition_TransientLifetime_ResolvesNewInstanceEveryTime()
    {
        var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var first = scope.ServiceProvider.GetServices<ITransientGadget>().Single(g => g.Name == "gear");
        var second = scope.ServiceProvider.GetServices<ITransientGadget>().Single(g => g.Name == "gear");

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Composition_MultipleMarkers_RegisterAdditivelyUnderEachFacade()
    {
        var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var gadgets = scope.ServiceProvider.GetServices<IGadget>().ToList();
        var transientGadgets = scope.ServiceProvider.GetServices<ITransientGadget>().ToList();

        Assert.Equal(2, gadgets.Count);
        Assert.Equal(2, transientGadgets.Count);
        Assert.NotSame(
            gadgets.Single(g => g.Name == "gear"),
            transientGadgets.Single(g => g.Name == "gear"));
    }
}

/// <summary>Definition contract auto-discovered and registered by Needlr per closed type argument.</summary>
public interface IGadgetDefinition<TData>
    where TData : class
{
    string Name { get; }
}

/// <summary>Per-type-argument store registered under an explicit service key.</summary>
public interface IGadgetStore<TData>
    where TData : class
{
    string Key { get; }

    Type DataType { get; }
}

/// <summary>Store implementation registered under the "primary" key.</summary>
public sealed class PrimaryGadgetStore<TData> : IGadgetStore<TData>
    where TData : class
{
    public string Key => "primary";

    public Type DataType => typeof(TData);
}

/// <summary>Store implementation registered under the "fallback" key.</summary>
public sealed class FallbackGadgetStore<TData> : IGadgetStore<TData>
    where TData : class
{
    public string Key => "fallback";

    public Type DataType => typeof(TData);
}

/// <summary>Scoped facade the rest of the system consumes as IEnumerable&lt;IGadget&gt;.</summary>
public interface IGadget
{
    string Name { get; }

    string StoreKey { get; }

    Type DataType { get; }

    Type StoreDataType { get; }
}

/// <summary>Transient facade proving multiple markers on one composition remain additive.</summary>
public interface ITransientGadget
{
    string Name { get; }
}

/// <summary>Marker data type for the gear definition.</summary>
public sealed class GearData
{
}

/// <summary>Marker data type for the lever definition.</summary>
public sealed class LeverData
{
}

/// <summary>Concrete, unattributed definition auto-registered as IGadgetDefinition&lt;GearData&gt;.</summary>
public sealed class GearDefinition : IGadgetDefinition<GearData>
{
    public string Name => "gear";
}

/// <summary>Concrete, unattributed definition auto-registered as IGadgetDefinition&lt;LeverData&gt;.</summary>
public sealed class LeverDefinition : IGadgetDefinition<LeverData>
{
    public string Name => "lever";
}

/// <summary>
/// Composition closed per discovered TData and registered under two facades with different
/// lifetimes, taking its store from an explicitly keyed registration.
/// </summary>
[RegisterClosedOverImplementationsOf(typeof(IGadgetDefinition<>), As = typeof(IGadget), Lifetime = InjectableLifetime.Scoped)]
[RegisterClosedOverImplementationsOf(typeof(IGadgetDefinition<>), As = typeof(ITransientGadget), Lifetime = InjectableLifetime.Transient)]
public sealed class GadgetCore<TData> : IGadget, ITransientGadget
    where TData : class
{
    private readonly IGadgetDefinition<TData> _definition;
    private readonly IGadgetStore<TData> _store;

    public GadgetCore(
        IGadgetDefinition<TData> definition,
        [FromKeyedServices("primary")] IGadgetStore<TData> store)
    {
        _definition = definition;
        _store = store;
    }

    public string Name => _definition.Name;

    public string StoreKey => _store.Key;

    public Type DataType => typeof(TData);

    public Type StoreDataType => _store.DataType;
}
