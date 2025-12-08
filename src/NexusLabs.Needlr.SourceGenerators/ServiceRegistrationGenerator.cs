using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.SourceGenerators;

/// <summary>
/// Source generator for creating compile-time service registration code.
/// This generator will scan types at compile time and generate registration methods,
/// eliminating the need for runtime reflection.
/// </summary>
[Generator]
public sealed class ServiceRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Phase 1: Minimal implementation - just register the generator
        // Full implementation will come in Phase 2
        
        // For now, we'll just emit a diagnostic to prove the generator is running
        context.RegisterPostInitializationOutput(ctx =>
        {
            // No-op for Phase 1
            // In Phase 2, this will generate the actual registration code
        });
    }
}
