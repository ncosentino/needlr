namespace NexusLabs.Needlr;

/// <summary>
/// Controls what factory artifacts are generated for types marked with <see cref="GenerateFactoryAttribute"/>.
/// </summary>
[Flags]
public enum FactoryGenerationMode
{
    /// <summary>
    /// Generate Func&lt;TRuntime..., TService&gt; registrations.
    /// </summary>
    Func = 1,

    /// <summary>
    /// Generate I{TypeName}Factory interface and implementation.
    /// </summary>
    Interface = 2,

    /// <summary>
    /// Generate both Func and Interface (default).
    /// </summary>
    All = Func | Interface
}
