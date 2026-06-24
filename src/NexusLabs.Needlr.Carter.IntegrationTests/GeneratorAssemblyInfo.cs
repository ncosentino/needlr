using NexusLabs.Needlr.Generators;

// Enable source generation for this assembly so the test Carter modules are discovered by
// Needlr's type registry (exactly as a consumer's feature project would via NexusLabs.Needlr.Build).
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = ["NexusLabs.Needlr.Carter.IntegrationTests"])]
