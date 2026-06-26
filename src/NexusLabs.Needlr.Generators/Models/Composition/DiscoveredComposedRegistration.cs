using System.Collections.Generic;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// A fully resolved composed registration: one closed composition type, exposed as a facade service type,
/// with its constructor dependencies resolved to service-provider expressions. Emitted as a single
/// <c>services.Add{Lifetime}</c> registration.
/// </summary>
internal readonly struct DiscoveredComposedRegistration
{
    public DiscoveredComposedRegistration(
        string facadeTypeName,
        string closedCompositionTypeName,
        IReadOnlyList<string> constructorArguments,
        GeneratorLifetime lifetime,
        string assemblyName,
        string? sourceFilePath)
    {
        FacadeTypeName = facadeTypeName;
        ClosedCompositionTypeName = closedCompositionTypeName;
        ConstructorArguments = constructorArguments;
        Lifetime = lifetime;
        AssemblyName = assemblyName;
        SourceFilePath = sourceFilePath;
    }

    /// <summary>The fully qualified facade service type the closed composition is registered as.</summary>
    public string FacadeTypeName { get; }

    /// <summary>The fully qualified closed composition type to instantiate (e.g., global::N.FooCore&lt;global::N.AlphaData&gt;).</summary>
    public string ClosedCompositionTypeName { get; }

    /// <summary>The constructor argument expressions (e.g., sp.GetRequiredService&lt;...&gt;()), in declaration order.</summary>
    public IReadOnlyList<string> ConstructorArguments { get; }

    /// <summary>The lifetime this registration is given.</summary>
    public GeneratorLifetime Lifetime { get; }

    /// <summary>The name of the assembly that declared the composition type.</summary>
    public string AssemblyName { get; }

    /// <summary>The source file path of the composition type, if available.</summary>
    public string? SourceFilePath { get; }
}
