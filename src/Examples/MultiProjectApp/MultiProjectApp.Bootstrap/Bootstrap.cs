// This project exists solely as a compile-time anchor.
// Referencing it from an entry point pulls all feature assemblies into the build graph.
// Each feature assembly's [ModuleInitializer] registers its TypeRegistry automatically at runtime.
namespace MultiProjectApp.Bootstrap;
