using NexusLabs.Needlr.Generators;

// Enable source generation for this assembly
// Filter to only benchmark test types to avoid scanning system assemblies
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = ["NexusLabs.Needlr.Benchmarks"])]
