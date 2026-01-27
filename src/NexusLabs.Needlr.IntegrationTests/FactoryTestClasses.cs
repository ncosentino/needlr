namespace NexusLabs.Needlr.IntegrationTests;

// ============================================================================
// Factory Generation Test Types
// These types have mixed injectable and runtime constructor parameters
// and use [GenerateFactory] to have factory interfaces/Func<> generated.
// ============================================================================

/// <summary>
/// A simple dependency that can be auto-injected.
/// </summary>
public interface IFactoryDependency
{
    string Name { get; }
}

/// <summary>
/// Implementation of factory dependency.
/// </summary>
public sealed class FactoryDependency : IFactoryDependency
{
    public string Name => "FactoryDependency";
}

/// <summary>
/// A service that requires both injectable and runtime parameters.
/// The generator should create ISimpleFactoryServiceFactory and Func.
/// </summary>
[GenerateFactory]
public sealed class SimpleFactoryService
{
    public IFactoryDependency Dependency { get; }
    public string ConnectionString { get; }

    public SimpleFactoryService(IFactoryDependency dependency, string connectionString)
    {
        Dependency = dependency;
        ConnectionString = connectionString;
    }
}

/// <summary>
/// A service with multiple runtime parameters.
/// </summary>
[GenerateFactory]
public sealed class MultiParamFactoryService
{
    public IFactoryDependency Dependency { get; }
    public string Host { get; }
    public int Port { get; }

    public MultiParamFactoryService(IFactoryDependency dependency, string host, int port)
    {
        Dependency = dependency;
        Host = host;
        Port = port;
    }
}

/// <summary>
/// Service with Func-only generation mode.
/// </summary>
[GenerateFactory(Mode = FactoryGenerationMode.Func)]
public sealed class FuncOnlyFactoryService
{
    public IFactoryDependency Dependency { get; }
    public Guid RequestId { get; }

    public FuncOnlyFactoryService(IFactoryDependency dependency, Guid requestId)
    {
        Dependency = dependency;
        RequestId = requestId;
    }
}

/// <summary>
/// Service with Interface-only generation mode.
/// </summary>
[GenerateFactory(Mode = FactoryGenerationMode.Interface)]
public sealed class InterfaceOnlyFactoryService
{
    public IFactoryDependency Dependency { get; }
    public DateTime CreatedAt { get; }

    public InterfaceOnlyFactoryService(IFactoryDependency dependency, DateTime createdAt)
    {
        Dependency = dependency;
        CreatedAt = createdAt;
    }
}

/// <summary>
/// Service with multiple constructors - each with runtime params should get a Create() overload.
/// </summary>
[GenerateFactory]
public sealed class MultiConstructorFactoryService
{
    public IFactoryDependency Dependency { get; }
    public string Name { get; }
    public int? Timeout { get; }

    public MultiConstructorFactoryService(IFactoryDependency dependency, string name)
    {
        Dependency = dependency;
        Name = name;
        Timeout = null;
    }

    public MultiConstructorFactoryService(IFactoryDependency dependency, string name, int timeout)
    {
        Dependency = dependency;
        Name = name;
        Timeout = timeout;
    }
}

/// <summary>
/// Interface for testing generic factory attribute.
/// </summary>
public interface IGenericFactoryService
{
    IFactoryDependency Dependency { get; }
    string Config { get; }
}

/// <summary>
/// Service using the generic [GenerateFactory&lt;T&gt;] attribute.
/// Factory Create() and Func will return IGenericFactoryService.
/// </summary>
[GenerateFactory<IGenericFactoryService>]
public sealed class GenericFactoryService : IGenericFactoryService
{
    public IFactoryDependency Dependency { get; }
    public string Config { get; }

    public GenericFactoryService(IFactoryDependency dependency, string config)
    {
        Dependency = dependency;
        Config = config;
    }
}
