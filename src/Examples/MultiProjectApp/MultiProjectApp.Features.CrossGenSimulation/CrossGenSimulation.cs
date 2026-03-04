using System.Runtime.CompilerServices;

using NexusLabs.Needlr.Generators;

namespace MultiProjectApp.Features.CrossGenSimulation;

/// <summary>
/// Represents a type that was emitted by a second source generator.
/// TypeRegistryGenerator cannot see this type because NeedlrAutoGenerate is disabled for this
/// assembly, simulating what happens when another generator produces types at compile time —
/// those types are invisible to TypeRegistryGenerator because it operates on the original
/// compilation snapshot, before any other generator has emitted code.
/// </summary>
public interface ICrossGeneratedPlugin { }

/// <summary>
/// A plugin type that exists only because a hypothetical second generator emitted it.
/// TypeRegistryGenerator has no knowledge of this type. It reaches the Needlr registry
/// exclusively through <see cref="CrossGenSimulationRegistrations"/>.
/// </summary>
public sealed record SimulatedGeneratorPlugin : ICrossGeneratedPlugin { }

/// <summary>
/// Simulates the module initializer that a second source generator would emit to register its
/// types with Needlr at runtime, bypassing the Roslyn generator isolation boundary.
/// </summary>
internal static class CrossGenSimulationRegistrations
{
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Initialize()
#pragma warning restore CA2255
    {
        NeedlrSourceGenBootstrap.RegisterPlugins(static () =>
        [
            new PluginTypeInfo(
                typeof(SimulatedGeneratorPlugin),
                [typeof(ICrossGeneratedPlugin)],
                static () => new SimulatedGeneratorPlugin(),
                [])
        ]);
    }
}
