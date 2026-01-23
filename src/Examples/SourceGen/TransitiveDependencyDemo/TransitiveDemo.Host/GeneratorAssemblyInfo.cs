using NexusLabs.Needlr.Generators;

// This assembly hosts the application and triggers source generation.
// It references both FeatureA and FeatureB, but only uses FeatureB types in code.
// 
// IMPORTANT: We use IncludeSelf = false because this Host assembly doesn't define
// any injectable types itself. The feature assemblies each have their own
// [GenerateTypeRegistry] and generate their own type registries.
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TransitiveDemo.Host" })]

// NOTE: Assembly ordering is now configured at the Syringe level using the
// same expression-based API for both reflection and source-gen. For example:
//
//   new Syringe()
//       .UsingSourceGen()
//       .UsingAssemblyProvider(builder => builder
//           .OrderAssemblies(order => order
//               .By(a => a.Name.StartsWith("TransitiveDemo.FeatureA")))
//           .Build())
//       ...
//
// The default alphabetical order (FeatureA before FeatureB) works for this demo.
