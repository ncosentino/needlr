using NexusLabs.Needlr.Generators;

// This attribute enables source generation for this assembly and its dependencies.
// The generator will discover and register all injectable types from referenced assemblies.
// We limit to BundleExamplePlugin namespace to avoid picking up system types.
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = ["BundleExamplePlugin"])]
