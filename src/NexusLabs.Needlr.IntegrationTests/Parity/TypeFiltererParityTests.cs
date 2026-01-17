using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.TypeFilterers;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Parity;

/// <summary>
/// Tests that verify parity between reflection-based and generated type filterers.
/// </summary>
public sealed class TypeFiltererParityTests
{
    private readonly ITypeFilterer _reflectionFilterer;
    private readonly ITypeFilterer _generatedFilterer;
    private readonly IReadOnlyList<InjectableTypeInfo> _generatedTypes;

    public TypeFiltererParityTests()
    {
        _reflectionFilterer = new DefaultTypeFilterer();
        _generatedTypes = NexusLabs.Needlr.Generated.TypeRegistry.GetInjectableTypes();
        _generatedFilterer = new GeneratedTypeFilterer(
            NexusLabs.Needlr.Generated.TypeRegistry.GetInjectableTypes);
    }

    [Fact]
    public void GeneratedFilterer_WithTypeProvider_IdentifiesSingletons()
    {
        // Get types that the generated system knows about
        var testTypes = _generatedTypes
            .Where(t => t.Type.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .ToList();

        foreach (var typeInfo in testTypes)
        {
            if (typeInfo.Lifetime == InjectableLifetime.Singleton)
            {
                Assert.True(
                    _generatedFilterer.IsInjectableSingletonType(typeInfo.Type),
                    $"Type {typeInfo.Type.Name} should be identified as singleton by generated filterer");
            }
        }
    }

    [Fact]
    public void GeneratedFilterer_WithTypeProvider_IdentifiesScoped()
    {
        var testTypes = _generatedTypes
            .Where(t => t.Type.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .ToList();

        foreach (var typeInfo in testTypes)
        {
            if (typeInfo.Lifetime == InjectableLifetime.Scoped)
            {
                Assert.True(
                    _generatedFilterer.IsInjectableScopedType(typeInfo.Type),
                    $"Type {typeInfo.Type.Name} should be identified as scoped by generated filterer");
            }
        }
    }

    [Fact]
    public void GeneratedFilterer_WithTypeProvider_IdentifiesTransient()
    {
        var testTypes = _generatedTypes
            .Where(t => t.Type.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .ToList();

        foreach (var typeInfo in testTypes)
        {
            if (typeInfo.Lifetime == InjectableLifetime.Transient)
            {
                Assert.True(
                    _generatedFilterer.IsInjectableTransientType(typeInfo.Type),
                    $"Type {typeInfo.Type.Name} should be identified as transient by generated filterer");
            }
        }
    }

    [Fact]
    public void GeneratedFilterer_WithoutTypeProvider_ReturnsFalseForAll()
    {
        var noOpFilterer = new GeneratedTypeFilterer();

        var testTypes = _generatedTypes
            .Where(t => t.Type.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .ToList();

        foreach (var typeInfo in testTypes)
        {
            Assert.False(noOpFilterer.IsInjectableSingletonType(typeInfo.Type));
            Assert.False(noOpFilterer.IsInjectableScopedType(typeInfo.Type));
            Assert.False(noOpFilterer.IsInjectableTransientType(typeInfo.Type));
        }
    }

    [Fact]
    public void Parity_SingletonDetection_ReflectionAndGeneratedAgree()
    {
        var testTypes = _generatedTypes
            .Where(t => t.Type.Namespace?.StartsWith("NexusLabs.Needlr.IntegrationTests") == true)
            .ToList();

        foreach (var typeInfo in testTypes)
        {
            var reflectionSaysSingleton = _reflectionFilterer.IsInjectableSingletonType(typeInfo.Type);
            var generatedSaysSingleton = typeInfo.Lifetime == InjectableLifetime.Singleton;

            Assert.Equal(reflectionSaysSingleton, generatedSaysSingleton);
        }
    }

    [Fact]
    public void GeneratedFilterer_UnknownType_ReturnsFalse()
    {
        // A type not in the generated registry
        var unknownType = typeof(string);

        Assert.False(_generatedFilterer.IsInjectableSingletonType(unknownType));
        Assert.False(_generatedFilterer.IsInjectableScopedType(unknownType));
        Assert.False(_generatedFilterer.IsInjectableTransientType(unknownType));
    }
}
