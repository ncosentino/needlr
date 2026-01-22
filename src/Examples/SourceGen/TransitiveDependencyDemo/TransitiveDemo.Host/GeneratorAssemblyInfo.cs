using NexusLabs.Needlr.Generators;

// This assembly hosts the application and triggers source generation.
// It references both FeatureA and FeatureB, but only uses FeatureB types in code.
// 
// IMPORTANT: We use IncludeSelf = false because this Host assembly doesn't define
// any injectable types itself. The feature assemblies each have their own
// [GenerateTypeRegistry] and generate their own type registries.
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TransitiveDemo.Host" })]

// OPTIONAL: Use NeedlrAssemblyOrder to control load order if needed.
// In this case, FeatureA must load before FeatureB because FeatureB's plugin
// depends on ICoreLogger which is registered by FeatureA's plugin.
// 
// The default alphabetical order (FeatureA before FeatureB) happens to work,
// but we demonstrate the attribute here for explicitness.
[assembly: NeedlrAssemblyOrder(First = new[] { "TransitiveDemo.FeatureA" })]
