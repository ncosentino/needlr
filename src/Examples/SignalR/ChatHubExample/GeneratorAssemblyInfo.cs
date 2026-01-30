using NexusLabs.Needlr.Generators;

// Enable source generation for this assembly.
// This generates a TypeRegistry class at compile-time that contains
// all injectable types, eliminating the need for runtime reflection.
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = ["ChatHubExample"])]
