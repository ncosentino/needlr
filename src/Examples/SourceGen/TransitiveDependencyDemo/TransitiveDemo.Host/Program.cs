// TransitiveDemo.Host - Demonstrating Automatic Assembly Force-Loading
//
// This example demonstrates a critical feature of Needlr's source generation:
// automatic discovery and loading of referenced assemblies with [GenerateTypeRegistry].
//
// THE SCENARIO:
// - Host references FeatureA and FeatureB
// - Host code ONLY uses IFeatureBService (from FeatureB)
// - Host code NEVER directly uses any types from FeatureA
// - FeatureB's plugin depends on ICoreLogger which is registered by FeatureA's plugin
//
// THE PROBLEM (without force-loading):
// - .NET only loads assemblies when code first references their types
// - Since Host never uses FeatureA types, FeatureA.dll would never load
// - FeatureA's module initializer would never run
// - FeatureA's plugin would never register ICoreLogger
// - FeatureB's plugin would fail with "Unable to resolve service for type 'ICoreLogger'"
//
// THE SOLUTION (Needlr's force-loading):
// - Needlr's generator scans all referenced assemblies for [GenerateTypeRegistry]
// - It generates typeof() calls to force those assemblies to load
// - This ensures all module initializers run and all plugins are discovered
// - The [NeedlrAssemblyOrder] attribute controls the order if needed

using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

// Notice: We ONLY import FeatureB types, NEVER FeatureA types!
using TransitiveDemo.FeatureB;

Console.WriteLine("=== TransitiveDemo: Automatic Assembly Force-Loading ===");
Console.WriteLine();
Console.WriteLine("This demo shows that plugins from transitive dependencies are discovered");
Console.WriteLine("even when their types are never directly referenced in code.");
Console.WriteLine();

// Build the service provider using source generation
var provider = new Syringe()
    .UsingSourceGen()
    .BuildServiceProvider();

Console.WriteLine();
Console.WriteLine("=== Service Resolution ===");

// Get the FeatureB service - this works because:
// 1. Needlr force-loaded FeatureA (even though we never use its types)
// 2. FeatureA's plugin registered ICoreLogger
// 3. FeatureB's service was registered with ICoreLogger dependency resolved
var featureBService = provider.GetRequiredService<IFeatureBService>();
featureBService.DoWork();

Console.WriteLine();
Console.WriteLine("=== Success! ===");
Console.WriteLine("All plugins executed successfully, including FeatureA's plugin");
Console.WriteLine("which we never directly referenced in code!");
Console.WriteLine();
Console.WriteLine("Check obj/Generated/ to see the ForceLoadReferencedAssemblies() method");
Console.WriteLine("that makes this possible.");
