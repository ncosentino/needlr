namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Records that a discovered type argument cannot legally close a composition type because it violates
/// the composition's generic constraints. Surfaced as the <c>NDLRGEN038</c> build diagnostic, and the
/// corresponding registration is skipped rather than emitting code that fails to compile.
/// </summary>
internal readonly struct ComposedConstraintViolation
{
    public ComposedConstraintViolation(
        string compositionTypeName,
        string typeArgumentName,
        string sourceInterfaceName,
        string? sourceFilePath)
    {
        CompositionTypeName = compositionTypeName;
        TypeArgumentName = typeArgumentName;
        SourceInterfaceName = sourceInterfaceName;
        SourceFilePath = sourceFilePath;
    }

    /// <summary>The composition type that could not be closed (e.g., FooCore&lt;TData&gt;).</summary>
    public string CompositionTypeName { get; }

    /// <summary>The offending type argument(s), comma-separated (e.g., StructData).</summary>
    public string TypeArgumentName { get; }

    /// <summary>The source open generic interface whose implementation produced the type argument.</summary>
    public string SourceInterfaceName { get; }

    /// <summary>The source file path of the composition type, if available.</summary>
    public string? SourceFilePath { get; }
}
