using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.Bootstrap;

/// <summary>
/// Contract coverage for how <see cref="NeedlrSourceGenBootstrap"/> composes generated
/// registrations: empty state, participant identities, single and multiple registrars,
/// ordering, deduplication, cache invalidation, test scopes, and argument guards.
/// </summary>
[Collection(SourceGenBootstrapCollection.Name)]
public sealed class NeedlrSourceGenBootstrapCompositionTests : IDisposable
{
    public NeedlrSourceGenBootstrapCompositionTests()
    {
        NeedlrSourceGenBootstrap.ClearRegistrationsForTesting();
    }

    public void Dispose()
    {
        NeedlrSourceGenBootstrap.ClearRegistrationsForTesting();
    }

    [Fact]
    public void TryGetProviders_NoRegistrations_ReturnsFalseWithNullOutputs()
    {
        var found = NeedlrSourceGenBootstrap.TryGetProviders(
            out var injectableTypeProvider,
            out var pluginTypeProvider);

        Assert.False(found, "Expected no providers when nothing has been registered");
        Assert.Null(injectableTypeProvider);
        Assert.Null(pluginTypeProvider);
    }

    [Fact]
    public void TryGetProviders_NoRegistrations_WithParticipantOutput_ReturnsFalseWithNullOutputs()
    {
        var found = NeedlrSourceGenBootstrap.TryGetProviders(
            out var injectableTypeProvider,
            out var pluginTypeProvider,
            out var registryParticipantTypes);

        Assert.False(found, "Expected no providers when nothing has been registered");
        Assert.Null(injectableTypeProvider);
        Assert.Null(pluginTypeProvider);
        Assert.Null(registryParticipantTypes);
    }

    [Fact]
    public void TryGetDecoratorApplier_NoRegistrations_ReturnsFalseWithNullApplier()
    {
        var found = NeedlrSourceGenBootstrap.TryGetDecoratorApplier(out var decoratorApplier);

        Assert.False(found, "Expected no decorator applier when nothing has been registered");
        Assert.Null(decoratorApplier);
    }

    [Fact]
    public void TryGetOptionsRegistrar_NoRegistrations_ReturnsFalseWithNullRegistrar()
    {
        var found = NeedlrSourceGenBootstrap.TryGetOptionsRegistrar(out var optionsRegistrar);

        Assert.False(found, "Expected no options registrar when nothing has been registered");
        Assert.Null(optionsRegistrar);
    }

    [Fact]
    public void TryGetExtensionRegistrar_NoRegistrations_ReturnsFalseWithNullRegistrar()
    {
        var found = NeedlrSourceGenBootstrap.TryGetExtensionRegistrar(out var extensionRegistrar);

        Assert.False(found, "Expected no extension registrar when nothing has been registered");
        Assert.Null(extensionRegistrar);
    }

    [Fact]
    public void TryGetProviders_SingleRegistration_ReturnsRegisteredInjectableAndPluginTypes()
    {
        NeedlrSourceGenBootstrap.Register(
            () => [CreateInjectable(typeof(BootstrapTestServiceOne), InjectableLifetime.Singleton)],
            () => [CreatePlugin(typeof(BootstrapTestPluginOne))]);

        var found = NeedlrSourceGenBootstrap.TryGetProviders(
            out var injectableTypeProvider,
            out var pluginTypeProvider);

        Assert.True(found, "Expected providers after a single registration");

        var injectables = injectableTypeProvider().ToList();
        var injectable = Assert.Single(injectables);
        Assert.Equal(typeof(BootstrapTestServiceOne), injectable.Type);
        Assert.Equal(InjectableLifetime.Singleton, injectable.Lifetime);

        var plugins = pluginTypeProvider().ToList();
        var plugin = Assert.Single(plugins);
        Assert.Equal(typeof(BootstrapTestPluginOne), plugin.PluginType);
    }

    [Fact]
    public void TryGetProviders_DistinctTypesAcrossRegistrations_PreservesRegistrationOrder()
    {
        NeedlrSourceGenBootstrap.Register(
            () => [CreateInjectable(typeof(BootstrapTestServiceOne), InjectableLifetime.Singleton)],
            () => [CreatePlugin(typeof(BootstrapTestPluginOne))]);
        NeedlrSourceGenBootstrap.Register(
            () => [CreateInjectable(typeof(BootstrapTestServiceTwo), InjectableLifetime.Transient)],
            () => [CreatePlugin(typeof(BootstrapTestPluginTwo))]);

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetProviders(out var injectableTypeProvider, out var pluginTypeProvider),
            "Expected providers after two registrations");

        Assert.Equal(
            [typeof(BootstrapTestServiceOne), typeof(BootstrapTestServiceTwo)],
            injectableTypeProvider().Select(i => i.Type).ToArray());
        Assert.Equal(
            [typeof(BootstrapTestPluginOne), typeof(BootstrapTestPluginTwo)],
            pluginTypeProvider().Select(p => p.PluginType).ToArray());
    }

    [Fact]
    public void TryGetProviders_RegistryParticipants_PreservesRegistrationOrderAndDeduplicates()
    {
        NeedlrSourceGenBootstrap.Register(
            typeof(BootstrapTestServiceOne),
            () => [],
            () => []);
        NeedlrSourceGenBootstrap.Register(
            typeof(BootstrapTestServiceTwo),
            () => [],
            () => []);
        NeedlrSourceGenBootstrap.Register(
            typeof(BootstrapTestServiceOne),
            () => [],
            () => []);

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetProviders(out _, out _, out var registryParticipantTypes),
            "Expected providers after registry participants were registered");
        Assert.Equal(
            [typeof(BootstrapTestServiceOne), typeof(BootstrapTestServiceTwo)],
            registryParticipantTypes);
    }

    [Fact]
    public void TryGetProviders_LegacyRegistration_HasNoRegistryParticipants()
    {
        NeedlrSourceGenBootstrap.Register(() => [], () => []);

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetProviders(out _, out _, out var registryParticipantTypes),
            "Expected providers after a legacy registration");
        Assert.Empty(registryParticipantTypes);
    }

    [Fact]
    public void TryGetProviders_DuplicateInjectableTypes_KeepsFirstRegistrationDeterministically()
    {
        NeedlrSourceGenBootstrap.Register(
            () => [CreateInjectable(typeof(BootstrapTestServiceOne), InjectableLifetime.Singleton)],
            () => []);
        NeedlrSourceGenBootstrap.Register(
            () =>
            [
                CreateInjectable(typeof(BootstrapTestServiceOne), InjectableLifetime.Transient),
                CreateInjectable(typeof(BootstrapTestServiceTwo), InjectableLifetime.Scoped)
            ],
            () => []);

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetProviders(out var injectableTypeProvider, out _),
            "Expected providers after two registrations");

        var injectables = injectableTypeProvider().ToList();
        Assert.Equal(2, injectables.Count);
        Assert.Equal(typeof(BootstrapTestServiceOne), injectables[0].Type);
        Assert.Equal(InjectableLifetime.Singleton, injectables[0].Lifetime);
        Assert.Equal(typeof(BootstrapTestServiceTwo), injectables[1].Type);
        Assert.Equal(InjectableLifetime.Scoped, injectables[1].Lifetime);
    }

    [Fact]
    public void TryGetProviders_RegistrationAddedAfterFirstRead_InvalidatesCachedCombination()
    {
        NeedlrSourceGenBootstrap.Register(
            () => [CreateInjectable(typeof(BootstrapTestServiceOne), InjectableLifetime.Singleton)],
            () => []);

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetProviders(out var firstProvider, out _),
            "Expected providers after the first registration");
        Assert.Single(firstProvider());

        NeedlrSourceGenBootstrap.Register(
            () => [CreateInjectable(typeof(BootstrapTestServiceTwo), InjectableLifetime.Singleton)],
            () => []);

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetProviders(out var secondProvider, out _),
            "Expected providers after the second registration");
        Assert.Equal(
            [typeof(BootstrapTestServiceOne), typeof(BootstrapTestServiceTwo)],
            secondProvider().Select(i => i.Type).ToArray());
    }

    [Fact]
    public void TryGetDecoratorApplier_SingleApplier_ReceivesTheSuppliedServiceCollection()
    {
        var services = new ServiceCollection();
        object? observed = null;

        NeedlrSourceGenBootstrap.Register(
            () => [],
            () => [],
            s => observed = s);

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetDecoratorApplier(out var decoratorApplier),
            "Expected a decorator applier after registering one");
        decoratorApplier!(services);

        Assert.Same(services, observed);
    }

    [Fact]
    public void TryGetDecoratorApplier_MultipleAppliers_InvokesInRegistrationOrder()
    {
        var invocations = new List<string>();

        NeedlrSourceGenBootstrap.Register(() => [], () => [], _ => invocations.Add("first"));
        NeedlrSourceGenBootstrap.Register(() => [], () => [], _ => invocations.Add("second"));
        NeedlrSourceGenBootstrap.Register(() => [], () => [], _ => invocations.Add("third"));

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetDecoratorApplier(out var decoratorApplier),
            "Expected a combined decorator applier");
        decoratorApplier!(new ServiceCollection());

        Assert.Equal(["first", "second", "third"], invocations);
    }

    [Fact]
    public void TryGetDecoratorApplier_RegistrationWithoutApplier_ReturnsFalse()
    {
        NeedlrSourceGenBootstrap.Register(
            () => [CreateInjectable(typeof(BootstrapTestServiceOne), InjectableLifetime.Singleton)],
            () => []);

        var found = NeedlrSourceGenBootstrap.TryGetDecoratorApplier(out var decoratorApplier);

        Assert.False(found, "Expected no decorator applier when the registration supplied none");
        Assert.Null(decoratorApplier);
    }

    [Fact]
    public void TryGetOptionsRegistrar_SingleRegistrar_ReceivesTheExactServicesAndConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        object? observedServices = null;
        object? observedConfiguration = null;

        NeedlrSourceGenBootstrap.Register(
            () => [],
            () => [],
            null,
            (s, c) =>
            {
                observedServices = s;
                observedConfiguration = c;
            });

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetOptionsRegistrar(out var optionsRegistrar),
            "Expected an options registrar after registering one");
        optionsRegistrar!(services, configuration);

        Assert.Same(services, observedServices);
        Assert.Same(configuration, observedConfiguration);
    }

    [Fact]
    public void TryGetOptionsRegistrar_MultipleRegistrars_InvokesInRegistrationOrder()
    {
        var invocations = new List<string>();

        NeedlrSourceGenBootstrap.Register(() => [], () => [], null, (_, _) => invocations.Add("first"));
        NeedlrSourceGenBootstrap.Register(() => [], () => [], null, (_, _) => invocations.Add("second"));

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetOptionsRegistrar(out var optionsRegistrar),
            "Expected a combined options registrar");
        optionsRegistrar!(new ServiceCollection(), new ConfigurationBuilder().Build());

        Assert.Equal(["first", "second"], invocations);
    }

    [Fact]
    public void TryGetOptionsRegistrar_RegistrationWithoutRegistrar_ReturnsFalse()
    {
        NeedlrSourceGenBootstrap.Register(() => [], () => [], _ => { });

        var found = NeedlrSourceGenBootstrap.TryGetOptionsRegistrar(out var optionsRegistrar);

        Assert.False(found, "Expected no options registrar when the registration supplied none");
        Assert.Null(optionsRegistrar);
    }

    [Fact]
    public void TryGetExtensionRegistrar_SingleRegistrar_ReceivesTheExactServicesAndConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        object? observedServices = null;
        object? observedConfiguration = null;

        NeedlrSourceGenBootstrap.RegisterExtension((s, c) =>
        {
            observedServices = s;
            observedConfiguration = c;
        });

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetExtensionRegistrar(out var extensionRegistrar),
            "Expected an extension registrar after registering one");
        extensionRegistrar!(services, configuration);

        Assert.Same(services, observedServices);
        Assert.Same(configuration, observedConfiguration);
    }

    [Fact]
    public void TryGetExtensionRegistrar_MultipleRegistrars_InvokesInRegistrationOrder()
    {
        var invocations = new List<string>();

        NeedlrSourceGenBootstrap.RegisterExtension((_, _) => invocations.Add("first"));
        NeedlrSourceGenBootstrap.RegisterExtension((_, _) => invocations.Add("second"));
        NeedlrSourceGenBootstrap.RegisterExtension((_, _) => invocations.Add("third"));

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetExtensionRegistrar(out var extensionRegistrar),
            "Expected a combined extension registrar");
        extensionRegistrar!(new ServiceCollection(), new ConfigurationBuilder().Build());

        Assert.Equal(["first", "second", "third"], invocations);
    }

    [Fact]
    public void TryGetExtensionRegistrar_SnapshotTakenBeforeLaterRegistration_DoesNotInvokeLaterRegistrar()
    {
        var invocations = new List<string>();
        NeedlrSourceGenBootstrap.RegisterExtension((_, _) => invocations.Add("first"));

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetExtensionRegistrar(out var snapshot),
            "Expected an extension registrar after the first registration");

        NeedlrSourceGenBootstrap.RegisterExtension((_, _) => invocations.Add("second"));
        snapshot!(new ServiceCollection(), new ConfigurationBuilder().Build());

        Assert.Equal(["first"], invocations);
    }

    [Fact]
    public void TryGetExtensionRegistrar_IsIndependentOfMainRegistrations()
    {
        NeedlrSourceGenBootstrap.RegisterExtension((_, _) => { });

        Assert.False(
            NeedlrSourceGenBootstrap.TryGetProviders(out _, out _),
            "Expected extension registrars not to satisfy the main provider contract");
        Assert.True(
            NeedlrSourceGenBootstrap.TryGetExtensionRegistrar(out _),
            "Expected the extension registrar to be visible without a main registration");
    }

    [Fact]
    public void ClearRegistrationsForTesting_ClearsExtensionRegistrars()
    {
        NeedlrSourceGenBootstrap.RegisterExtension((_, _) => { });

        NeedlrSourceGenBootstrap.ClearRegistrationsForTesting();

        var found = NeedlrSourceGenBootstrap.TryGetExtensionRegistrar(out var extensionRegistrar);

        Assert.False(found, "Expected extension registrars to be cleared for test isolation");
        Assert.Null(extensionRegistrar);
    }

    [Fact]
    public void ClearRegistrationsForTesting_ClearsMainRegistrations()
    {
        NeedlrSourceGenBootstrap.Register(
            () => [CreateInjectable(typeof(BootstrapTestServiceOne), InjectableLifetime.Singleton)],
            () => [],
            _ => { },
            (_, _) => { });

        NeedlrSourceGenBootstrap.ClearRegistrationsForTesting();

        Assert.False(
            NeedlrSourceGenBootstrap.TryGetProviders(out _, out _),
            "Expected providers to be cleared");
        Assert.False(
            NeedlrSourceGenBootstrap.TryGetDecoratorApplier(out _),
            "Expected decorator appliers to be cleared");
        Assert.False(
            NeedlrSourceGenBootstrap.TryGetOptionsRegistrar(out _),
            "Expected options registrars to be cleared");
    }

    [Fact]
    public void BeginTestScope_OverridesGlobalRegistrationsForTheScopeDuration()
    {
        NeedlrSourceGenBootstrap.Register(
            () => [CreateInjectable(typeof(BootstrapTestServiceOne), InjectableLifetime.Singleton)],
            () => []);

        using (NeedlrSourceGenBootstrap.BeginTestScope(
            () => [CreateInjectable(typeof(BootstrapTestServiceTwo), InjectableLifetime.Singleton)],
            () => []))
        {
            Assert.True(
                NeedlrSourceGenBootstrap.TryGetProviders(out var scopedProvider, out _),
                "Expected the scoped providers to be visible");
            Assert.Equal(
                [typeof(BootstrapTestServiceTwo)],
                scopedProvider().Select(i => i.Type).ToArray());
        }

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetProviders(out var globalProvider, out _),
            "Expected the global providers to be restored");
        Assert.Equal(
            [typeof(BootstrapTestServiceOne)],
            globalProvider().Select(i => i.Type).ToArray());
    }

    [Fact]
    public void BeginTestScope_Nested_RestoresThePriorScopeOnDispose()
    {
        using var outer = NeedlrSourceGenBootstrap.BeginTestScope(
            () => [CreateInjectable(typeof(BootstrapTestServiceOne), InjectableLifetime.Singleton)],
            () => []);

        using (NeedlrSourceGenBootstrap.BeginTestScope(
            () => [CreateInjectable(typeof(BootstrapTestServiceTwo), InjectableLifetime.Singleton)],
            () => []))
        {
            Assert.True(
                NeedlrSourceGenBootstrap.TryGetProviders(out var innerProvider, out _),
                "Expected the inner scope providers to be visible");
            Assert.Equal(
                [typeof(BootstrapTestServiceTwo)],
                innerProvider().Select(i => i.Type).ToArray());
        }

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetProviders(out var outerProvider, out _),
            "Expected the outer scope providers to be restored");
        Assert.Equal(
            [typeof(BootstrapTestServiceOne)],
            outerProvider().Select(i => i.Type).ToArray());
    }

    [Fact]
    public void BeginTestScope_WithNoScopedDecoratorOrOptions_HidesGlobalDecoratorAndOptions()
    {
        NeedlrSourceGenBootstrap.Register(
            () => [],
            () => [],
            _ => { },
            (_, _) => { });

        using var scope = NeedlrSourceGenBootstrap.BeginTestScope(() => [], () => []);

        Assert.False(
            NeedlrSourceGenBootstrap.TryGetDecoratorApplier(out var decoratorApplier),
            "Expected the test scope to shadow the global decorator applier");
        Assert.Null(decoratorApplier);
        Assert.False(
            NeedlrSourceGenBootstrap.TryGetOptionsRegistrar(out var optionsRegistrar),
            "Expected the test scope to shadow the global options registrar");
        Assert.Null(optionsRegistrar);
    }

    [Fact]
    public void BeginTestScope_DoesNotShadowExtensionRegistrars()
    {
        var invocations = new List<string>();
        NeedlrSourceGenBootstrap.RegisterExtension((_, _) => invocations.Add("extension"));

        using var scope = NeedlrSourceGenBootstrap.BeginTestScope(() => [], () => []);

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetExtensionRegistrar(out var extensionRegistrar),
            "Expected extension registrars to remain visible inside a test scope");
        extensionRegistrar!(new ServiceCollection(), new ConfigurationBuilder().Build());

        Assert.Equal(["extension"], invocations);
    }

    [Fact]
    public void Register_NullInjectableTypeProvider_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            NeedlrSourceGenBootstrap.Register(null!, () => []));

        Assert.Equal("injectableTypeProvider", exception.ParamName);
    }

    [Fact]
    public void Register_NullRegistryParticipantType_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            NeedlrSourceGenBootstrap.Register(null!, () => [], () => []));

        Assert.Equal("registryParticipantType", exception.ParamName);
    }

    [Fact]
    public void Register_NullPluginTypeProvider_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            NeedlrSourceGenBootstrap.Register(() => [], null!));

        Assert.Equal("pluginTypeProvider", exception.ParamName);
    }

    [Fact]
    public void RegisterExtension_NullRegistrar_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            NeedlrSourceGenBootstrap.RegisterExtension(null!));

        Assert.Equal("extensionRegistrar", exception.ParamName);
    }

    [Fact]
    public void BeginTestScope_NullInjectableTypeProvider_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            NeedlrSourceGenBootstrap.BeginTestScope(null!, () => []));

        Assert.Equal("injectableTypeProvider", exception.ParamName);
    }

    [Fact]
    public void BeginTestScope_NullPluginTypeProvider_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            NeedlrSourceGenBootstrap.BeginTestScope(() => [], null!));

        Assert.Equal("pluginTypeProvider", exception.ParamName);
    }

    [Fact]
    public void Register_ConcurrentRegistrationsAndReads_ObserveEveryRegisteredType()
    {
        const int RegistrationCount = 64;

        Parallel.For(0, RegistrationCount, i =>
        {
            var injectable = CreateInjectable(MakeUniqueType(i), InjectableLifetime.Singleton);
            NeedlrSourceGenBootstrap.Register(() => [injectable], () => []);
            NeedlrSourceGenBootstrap.TryGetProviders(out _, out _);
        });

        Assert.True(
            NeedlrSourceGenBootstrap.TryGetProviders(out var injectableTypeProvider, out _),
            "Expected providers after concurrent registration");

        var registeredTypes = injectableTypeProvider().Select(i => i.Type).ToHashSet();
        Assert.Equal(RegistrationCount, registeredTypes.Count);
        Assert.Equal(
            Enumerable.Range(0, RegistrationCount).Select(MakeUniqueType).ToHashSet(),
            registeredTypes);
    }

    private static Type MakeUniqueType(int index)
    {
        var argument = typeof(int);
        for (var i = 0; i < index; i++)
        {
            argument = argument.MakeArrayType();
        }

        return typeof(UniqueBootstrapMarker<>).MakeGenericType(argument);
    }

    private static InjectableTypeInfo CreateInjectable(Type type, InjectableLifetime lifetime)
    {
        return new InjectableTypeInfo(type, [], lifetime);
    }

    private static PluginTypeInfo CreatePlugin(Type pluginType)
    {
        return new PluginTypeInfo(
            pluginType,
            [typeof(IBootstrapTestPlugin)],
            () => Activator.CreateInstance(pluginType)!,
            []);
    }
}
