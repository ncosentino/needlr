using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Executable behavioral tests for generated constructors: direct construction with
/// valid and invalid values, exact exception/ParamName assertions, and resolution of
/// the generated constructor's dependencies through a real <see cref="Syringe"/>.
/// </summary>
public sealed class GeneratedConstructorSourceGenTests
{
    [Fact]
    public void BareGenerateConstructor_ConstructsWithProvidedDependency()
    {
        var repository = new GcRepository();

        var service = new GcUserService(repository);

        Assert.Same(repository, service.Repository);
    }

    [Fact]
    public void BareGenerateConstructor_AllowsNullBecauseNoGuardIsConfigured()
    {
        var service = new GcUserService(null!);

        Assert.Null(service.Repository);
    }

    [Fact]
    public void SealedPartialClass_GeneratedConstructorConstructs()
    {
        var repository = new GcRepository();

        var service = new GcSealedUserService(repository);

        Assert.Same(repository, service.Repository);
    }

    [Fact]
    public void NonNullableReferencesMode_NullDependency_ThrowsArgumentNullExceptionWithGeneratedParameterName()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new GcGuardedUserService(null!));

        Assert.Equal("repository", exception.ParamName);
    }

    [Fact]
    public void NonNullableReferencesMode_ValidDependency_Constructs()
    {
        var repository = new GcRepository();

        var service = new GcGuardedUserService(repository);

        Assert.Same(repository, service.Repository);
    }

    [Fact]
    public void FieldTriggeredGeneration_InvalidTenantName_ThrowsArgumentExceptionWithGeneratedParameterName()
    {
        var repository = new GcRepository();

        var exception = Assert.Throws<ArgumentException>(() => new GcTenantService(repository, "   "));

        Assert.Equal("tenantName", exception.ParamName);
    }

    [Fact]
    public void FieldTriggeredGeneration_ValidTenantName_Constructs()
    {
        var repository = new GcRepository();

        var service = new GcTenantService(repository, "acme");

        Assert.Same(repository, service.Repository);
        Assert.Equal("acme", service.TenantName);
    }

    [Fact]
    public void FieldTriggeredGeneration_UnannotatedFieldAllowsNull()
    {
        var service = new GcTenantService(null!, "acme");

        Assert.Null(service.Repository);
    }

    [Fact]
    public void BuiltInNotNull_NullValue_ThrowsArgumentNullExceptionWithGeneratedParameterName()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new GcNotNullGuardService(null!));

        Assert.Equal("repository", exception.ParamName);
    }

    [Fact]
    public void BuiltInNotNullOrEmpty_EmptyValue_ThrowsArgumentExceptionWithGeneratedParameterName()
    {
        var exception = Assert.Throws<ArgumentException>(() => new GcNotNullOrEmptyGuardService(string.Empty));

        Assert.Equal("code", exception.ParamName);
    }

    [Fact]
    public void BuiltInNotNullOrEmpty_NullValue_ThrowsArgumentNullExceptionWithGeneratedParameterName()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new GcNotNullOrEmptyGuardService(null!));

        Assert.Equal("code", exception.ParamName);
    }

    [Fact]
    public void BuiltInNotNullOrEmpty_ValidValue_Constructs()
    {
        var service = new GcNotNullOrEmptyGuardService("ABC");

        Assert.Equal("ABC", service.Code);
    }

    [Fact]
    public void BuiltInNotNullOrWhiteSpace_WhiteSpaceValue_ThrowsArgumentExceptionWithGeneratedParameterName()
    {
        var exception = Assert.Throws<ArgumentException>(() => new GcNotNullOrWhiteSpaceGuardService("   "));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void GuardSuppression_ClassDefaultSuppressedForAnnotatedField_AllowsNull()
    {
        var service = new GcSuppressedGuardService(null!);

        Assert.Null(service.Repository);
    }

    [Fact]
    public void GuardComposition_ClassDefaultAndExplicitGuardBothApply()
    {
        Assert.Throws<ArgumentNullException>(() => new GcComposedGuardService(null!));
        Assert.Throws<ArgumentException>(() => new GcComposedGuardService("   "));

        var service = new GcComposedGuardService("acme");
        Assert.Equal("acme", service.TenantName);
    }

    [Fact]
    public void DirectCustomGuardType_InvalidValue_ThrowsFromCustomGuard()
    {
        var exception = Assert.Throws<ArgumentException>(() => new GcOrderService(Array.Empty<string>()));

        Assert.Equal("orders", exception.ParamName);
    }

    [Fact]
    public void DirectCustomGuardType_ValidValue_Constructs()
    {
        var service = new GcOrderService(new[] { "order-1" });

        Assert.Single(service.Orders);
    }

    [Fact]
    public void NamedCustomGuardMethod_InvalidValue_ThrowsFromSelectedMethod()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new GcRetryPolicy(-1));

        Assert.Equal("retryCount", exception.ParamName);
    }

    [Fact]
    public void NamedCustomGuardMethod_ValidValue_Constructs()
    {
        var policy = new GcRetryPolicy(3);

        Assert.Equal(3, policy.RetryCount);
    }

    [Fact]
    public void AliasGuardAttribute_InvalidValue_ThrowsFromUnderlyingCustomGuard()
    {
        var exception = Assert.Throws<ArgumentException>(() => new GcAliasOrderService(Array.Empty<string>()));

        Assert.Equal("orders", exception.ParamName);
    }

    [Fact]
    public void GeneratedConstructor_ResolvesThroughSyringeWithoutParameterlessActivation()
    {
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var service = serviceProvider.GetRequiredService<GcResolvedUserService>();

        Assert.NotNull(service.Repository);
        Assert.IsType<GcRepository>(service.Repository);
    }

    [Fact]
    public void GeneratedConstructor_SingletonDependencyIsSharedAcrossResolutions()
    {
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var repository = serviceProvider.GetRequiredService<IGcRepository>();
        var service = serviceProvider.GetRequiredService<GcResolvedUserService>();

        Assert.Same(repository, service.Repository);
    }

    [Fact]
    public void GenerateConstructor_ImplementingPluginInterface_IsResolvedByDiButNotDiscoveredAsPlugin()
    {
        // GcPluginWorker implements a plugin-style interface AND has a generated
        // constructor requiring IGcRepository. It must resolve normally through DI...
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var worker = serviceProvider.GetRequiredService<GcPluginWorker>();
        Assert.NotNull(worker.Repository);

        // ...but must never be discoverable via IPluginFactory, which would try to
        // activate it with a parameterless constructor that no longer exists once the
        // generated constructor requiring IGcRepository is emitted.
        var pluginFactory = new NexusLabs.Needlr.Injection.SourceGen.PluginFactories.GeneratedPluginFactory(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);

        var plugins = pluginFactory
            .CreatePluginsFromAssemblies<IGcPlugin>(new[] { GetType().Assembly })
            .ToList();

        Assert.Empty(plugins);
    }
}

public interface IGcRepository
{
}

public sealed class GcRepository : IGcRepository
{
}

[GenerateConstructor]
public partial class GcUserService
{
    private readonly IGcRepository _repository;

    public IGcRepository Repository => _repository;
}

[GenerateConstructor]
public sealed partial class GcSealedUserService
{
    private readonly IGcRepository _repository;

    public IGcRepository Repository => _repository;
}

[GenerateConstructor(ConstructorNullGuardMode.NonNullableReferences)]
public partial class GcGuardedUserService
{
    private readonly IGcRepository _repository;

    public IGcRepository Repository => _repository;
}

public partial class GcTenantService
{
    private readonly IGcRepository _repository;

    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
    private readonly string _tenantName;

    public IGcRepository Repository => _repository;

    public string TenantName => _tenantName;
}

[GenerateConstructor]
public partial class GcNotNullGuardService
{
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    private readonly IGcRepository _repository;
}

[GenerateConstructor]
public partial class GcNotNullOrEmptyGuardService
{
    [ConstructorGuard(ConstructorGuardKind.NotNullOrEmpty)]
    private readonly string _code;

    public string Code => _code;
}

[GenerateConstructor]
public partial class GcNotNullOrWhiteSpaceGuardService
{
    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
    private readonly string _name;
}

[GenerateConstructor(ConstructorNullGuardMode.NonNullableReferences)]
public partial class GcSuppressedGuardService
{
    [ConstructorGuard(ConstructorGuardKind.None)]
    private readonly IGcRepository _repository;

    public IGcRepository Repository => _repository;
}

[GenerateConstructor(ConstructorNullGuardMode.NonNullableReferences)]
public partial class GcComposedGuardService
{
    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
    private readonly string _tenantName;

    public string TenantName => _tenantName;
}

public static class GcCollectionNotEmptyGuard
{
    public static void Validate<T>(IReadOnlyCollection<T>? value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (value.Count == 0)
        {
            throw new ArgumentException("Collection must not be empty.", parameterName);
        }
    }
}

[GenerateConstructor]
public partial class GcOrderService
{
    [ConstructorGuard(typeof(GcCollectionNotEmptyGuard))]
    private readonly IReadOnlyCollection<string> _orders;

    public IReadOnlyCollection<string> Orders => _orders;
}

public static class GcNumberGuards
{
    public static void ValidatePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be positive.");
        }
    }
}

[GenerateConstructor]
public partial class GcRetryPolicy
{
    [ConstructorGuard(typeof(GcNumberGuards), nameof(GcNumberGuards.ValidatePositive))]
    private readonly int _retryCount;

    public int RetryCount => _retryCount;
}

[ConstructorGuardDefinition(typeof(GcCollectionNotEmptyGuard))]
[AttributeUsage(AttributeTargets.Field)]
public sealed class GcCollectionNotEmptyAttribute : Attribute
{
}

public partial class GcAliasOrderService
{
    [GcCollectionNotEmpty]
    private readonly IReadOnlyCollection<string> _orders;

    public IReadOnlyCollection<string> Orders => _orders;
}

[GenerateConstructor]
public partial class GcResolvedUserService
{
    private readonly IGcRepository _repository;

    public IGcRepository Repository => _repository;
}

public interface IGcPlugin
{
}

[GenerateConstructor]
public partial class GcPluginWorker : IGcPlugin
{
    private readonly IGcRepository _repository;

    public IGcRepository Repository => _repository;
}
