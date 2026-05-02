using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Integration tests that prove internal types and internal interfaces are
/// correctly source-generated AND resolvable from a real DI container.
///
/// These tests exist because the generator regression in commit 83c8bfb94
/// blanket-skipped all internal interfaces — including same-assembly ones.
/// That broke the standard .NET DI pattern where <c>internal class Foo : IFoo</c>
/// with <c>internal interface IFoo</c> is the default for non-public service contracts.
///
/// Generator-level string tests prove the generator EMITS correct code.
/// These integration tests prove the emitted code actually WORKS: the source
/// generator runs at compile time, the TypeRegistry discovers internal types,
/// and the Syringe container resolves them by interface at runtime.
///
/// If any of these tests fail, real-world consumers with internal DI contracts
/// (which is most non-trivial .NET projects) will have broken DI resolution.
/// </summary>
public sealed class InternalTypeSourceGenTests
{
    [Fact]
    public void InternalClass_ImplementingInternalInterface_ResolvableByInterface()
    {
        // This is THE critical regression test. If the source generator correctly
        // discovers internal types and their internal interfaces, Syringe will
        // register InternalAuthConfig as IInternalAuthConfig, and resolution works.
        //
        // If the generator skips the internal interface (the bug), GetService
        // returns null because IInternalAuthConfig was never registered.
        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        var resolved = provider.GetService<IInternalAuthConfig>();

        Assert.NotNull(resolved);
        Assert.IsType<InternalAuthConfig>(resolved);
    }

    [Fact]
    public void InternalClass_ImplementingInternalInterface_ResolvableByConcreteType()
    {
        // Internal concrete types must also be resolvable directly, not just
        // via their interfaces.
        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        var resolved = provider.GetService<InternalAuthConfig>();

        Assert.NotNull(resolved);
    }
    [Fact]
    public void InternalClass_WithInternalDependency_FullDiChainResolves()
    {
        // Proves the full DI chain works: InternalRepository depends on
        // IInternalAuthConfig (internal interface), and both are discovered
        // and registered by the source generator. If either is missing,
        // resolution of InternalRepository fails.
        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        var resolved = provider.GetService<IInternalRepository>();

        Assert.NotNull(resolved);
        Assert.IsType<InternalRepository>(resolved);
        // Verify the dependency was actually injected
        Assert.NotNull(((InternalRepository)resolved!).AuthConfig);
    }
    [Fact]
    public void InternalClass_ImplementingMultipleInternalInterfaces_AllResolvable()
    {
        // A type implementing multiple internal interfaces should be resolvable
        // via each one independently.
        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        var viaReader = provider.GetService<IInternalReader>();
        var viaWriter = provider.GetService<IInternalWriter>();

        Assert.NotNull(viaReader);
        Assert.NotNull(viaWriter);
        Assert.IsType<InternalFileStore>(viaReader);
        Assert.IsType<InternalFileStore>(viaWriter);
    }
    [Fact]
    public void GeneratedMetadata_InternalInterface_PresentInTypeRegistry()
    {
        // Directly inspect the generated TypeRegistry metadata to verify the
        // internal interface appears in the interface list for the type.
        // This is the compile-time evidence that the generator did its job.
        var injectableTypes = NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes();
        var authConfigType = injectableTypes.FirstOrDefault(t => t.Type == typeof(InternalAuthConfig));

        Assert.NotNull(authConfigType.Type);
        Assert.Contains(
            typeof(IInternalAuthConfig),
            authConfigType.Interfaces);
    }
}

/// <summary>
/// Internal interface representing an auth configuration contract.
/// The source generator MUST discover this and register it so that
/// types depending on it via constructor injection can be resolved.
/// </summary>
internal interface IInternalAuthConfig
{
    string EncryptionKey { get; }
}

/// <summary>
/// Internal implementation of <see cref="IInternalAuthConfig"/>.
/// The source generator must register this as both the concrete type
/// AND under <see cref="IInternalAuthConfig"/>.
/// </summary>
internal sealed class InternalAuthConfig : IInternalAuthConfig
{
    public string EncryptionKey => "test-key";
}

/// <summary>
/// Internal interface for a repository that depends on
/// <see cref="IInternalAuthConfig"/> — tests the full DI chain.
/// </summary>
internal interface IInternalRepository
{
    IInternalAuthConfig AuthConfig { get; }
}

/// <summary>
/// Internal repository that takes <see cref="IInternalAuthConfig"/>
/// via constructor injection. If the generator fails to register the
/// auth config interface, this type's resolution blows up at runtime.
/// </summary>
internal sealed class InternalRepository(
    IInternalAuthConfig authConfig) : IInternalRepository
{
    public IInternalAuthConfig AuthConfig { get; } = authConfig;
}

/// <summary>Multiple-interface scenario: internal reader contract.</summary>
internal interface IInternalReader
{
    string Read();
}

/// <summary>Multiple-interface scenario: internal writer contract.</summary>
internal interface IInternalWriter
{
    void Write(string data);
}

/// <summary>
/// Implements two internal interfaces. The source generator must register
/// this type under BOTH <see cref="IInternalReader"/> and
/// <see cref="IInternalWriter"/>.
/// </summary>
internal sealed class InternalFileStore : IInternalReader, IInternalWriter
{
    public string Read() => "data";
    public void Write(string data) { }
}
