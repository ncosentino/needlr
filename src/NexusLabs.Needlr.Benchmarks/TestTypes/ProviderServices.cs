// Disable unused parameter warnings - these services exist for DI benchmarking
#pragma warning disable CS9113

using NexusLabs.Needlr.Generators;

namespace NexusLabs.Needlr.Benchmarks.TestTypes;

// ============================================================================
// Services for Provider Benchmarks
// ============================================================================

public interface IProviderTargetService
{
    string GetValue();
}

public interface IProviderDependency1
{
    string Name { get; }
}

public interface IProviderDependency2
{
    string Name { get; }
}

public interface IProviderDependency3
{
    string Name { get; }
}

public sealed class ProviderTargetService(
    IProviderDependency1 dep1,
    IProviderDependency2 dep2,
    IProviderDependency3 dep3) : IProviderTargetService
{
    public string GetValue() => $"{dep1.Name}-{dep2.Name}-{dep3.Name}";
}

public sealed class ProviderDependency1 : IProviderDependency1
{
    public string Name => "Dep1";
}

public sealed class ProviderDependency2 : IProviderDependency2
{
    public string Name => "Dep2";
}

public sealed class ProviderDependency3 : IProviderDependency3
{
    public string Name => "Dep3";
}

// ============================================================================
// Factory-compatible service for provider factory benchmarks
// ============================================================================

public interface IProviderFactoryService
{
    string CreatedBy { get; }
}

/// <summary>
/// Factory service that has at least one injectable parameter.
/// </summary>
[GenerateFactory]
public sealed class ProviderFactoryService(IProviderDependency1 dependency, string createdBy) : IProviderFactoryService
{
    public IProviderDependency1 Dependency { get; } = dependency;
    public string CreatedBy { get; } = createdBy;
}

// ============================================================================
// Provider Definitions - Interface Mode
// ============================================================================

/// <summary>
/// Provider defined via interface mode with a single required property.
/// The generator creates an implementation class.
/// </summary>
[Provider]
public interface ISingleServiceProvider
{
    IProviderTargetService TargetService { get; }
}

/// <summary>
/// Provider defined via interface mode with multiple required properties.
/// </summary>
[Provider]
public interface IMultiPropertyProvider
{
    IProviderDependency1 Dependency1 { get; }
    IProviderDependency2 Dependency2 { get; }
    IProviderDependency3 Dependency3 { get; }
}

// ============================================================================
// Provider Definitions - Shorthand Class Mode
// ============================================================================

/// <summary>
/// Shorthand provider with a single required type.
/// </summary>
[Provider(typeof(IProviderTargetService))]
public partial class SingleShorthandProvider { }

/// <summary>
/// Shorthand provider with multiple required types.
/// </summary>
[Provider(typeof(IProviderDependency1), typeof(IProviderDependency2), typeof(IProviderDependency3))]
public partial class MultiShorthandProvider { }

/// <summary>
/// Shorthand provider with factory.
/// Uses Factories parameter to get a generated factory property.
/// </summary>
[Provider(Factories = new[] { typeof(ProviderFactoryService) })]
public partial class FactoryShorthandBenchmarkProvider { }

// ============================================================================
// Provider Definitions - Interface Mode with Factory
// ============================================================================

/// <summary>
/// Interface provider with factory property.
/// Generator creates implementation with factory access.
/// </summary>
[Provider]
public interface IFactoryInterfaceProvider
{
    NexusLabs.Needlr.Benchmarks.Generated.IProviderFactoryServiceFactory ProviderFactoryServiceFactory { get; }
}

// ============================================================================
// Provider Definitions - With Func<> Properties
// ============================================================================

/// <summary>
/// Provider with Func&lt;T&gt; property for lazy resolution.
/// </summary>
[Provider]
public interface IFuncProvider
{
    Func<IProviderTargetService> TargetServiceFunc { get; }
}

/// <summary>
/// Shorthand provider with both required and factory types.
/// </summary>
[Provider(typeof(IProviderDependency1), Factories = new[] { typeof(ProviderFactoryService) })]
public partial class MixedShorthandProvider { }
