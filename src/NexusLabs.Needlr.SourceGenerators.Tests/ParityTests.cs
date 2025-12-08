using Xunit;

namespace NexusLabs.Needlr.SourceGenerators.Tests;

/// <summary>
/// Tests that verify parity between reflection-based and source-generated registration.
/// These tests will be implemented in Phase 2 when the source generator produces actual output.
/// </summary>
public class ParityTests
{
    [Fact(Skip = "Phase 2: Source generator not yet implemented")]
    public void SourceGenerated_MatchesDefaultTypeRegistrar_ForSimpleClasses()
    {
        // This test will compare:
        // 1. Services registered by DefaultTypeRegistrar
        // 2. Services registered by source-generated code
        // They should be identical
    }

    [Fact(Skip = "Phase 2: Source generator not yet implemented")]
    public void SourceGenerated_MatchesDefaultTypeRegistrar_ForInjectableTypes()
    {
        // This test will verify that types with injectable constructors
        // are registered identically by both approaches
    }

    [Fact(Skip = "Phase 2: Source generator not yet implemented")]
    public void SourceGenerated_RespectsDoNotAutoRegisterAttribute()
    {
        // This test will verify that types with [DoNotAutoRegister]
        // are excluded from source-generated registrations
    }

    [Fact(Skip = "Phase 2: Source generator not yet implemented")]
    public void SourceGenerated_RegistersInterfacesCorrectly()
    {
        // This test will verify that types are registered for all their
        // interfaces, matching the reflection behavior
    }

    [Fact(Skip = "Phase 2: Source generator not yet implemented")]
    public void SourceGenerated_HandlesSingletonLifetimeCorrectly()
    {
        // This test will verify that singleton registrations use factory
        // delegates for interfaces to ensure same instance is returned
    }

    [Fact(Skip = "Phase 2: Source generator not yet implemented")]
    public void SourceGenerated_ExcludesSystemInterfaces()
    {
        // This test will verify that system interfaces are excluded from
        // registration, matching the reflection behavior
    }

    [Fact(Skip = "Phase 2: Source generator not yet implemented")]
    public void SourceGenerated_CompilesWithoutErrors()
    {
        // This test will verify that the generated code compiles successfully
        // using Roslyn CSharp.Testing infrastructure
    }

    [Fact(Skip = "Phase 2: Source generator not yet implemented")]
    public void SourceGenerated_ProducesExpectedOutputStructure()
    {
        // This test will verify the structure of generated code:
        // - Correct namespace
        // - Correct method signature
        // - Proper using statements
        // - Correct service registrations
    }
}
