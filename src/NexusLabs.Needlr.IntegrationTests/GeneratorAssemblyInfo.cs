using NexusLabs.Needlr.Generators;

// Enable source generation for this assembly
// Include types from NexusLabs.Needlr namespace (which covers the integration test types)
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = ["NexusLabs.Needlr"])]
