using NexusLabs.Needlr.Generators;

// NOTE: we can certainly use this approach and have the host load from itself AND the plugin namespaces,
// but this particular example is setup to show issues when you have internal types across assemblies.
// Because some types are internal in the plugin assembly, we let the plugin assembly do generation for itself.
// We try to put some guard rails in place to avoid common pitfalls, but ultimately users need to be aware
// of the limitations of source generation across assembly boundaries!
//[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = ["AotSourceGenConsoleApp", "AotSourceGenConsolePlugin"])]
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = ["AotSourceGenConsoleApp"])]
