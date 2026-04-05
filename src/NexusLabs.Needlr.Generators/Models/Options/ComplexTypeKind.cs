namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Identifies the kind of complex type for AOT binding generation.
/// </summary>
internal enum ComplexTypeKind
{
    /// <summary>Not a complex type (primitive, enum, etc).</summary>
    None,
    /// <summary>A nested object with properties to bind.</summary>
    NestedObject,
    /// <summary>An array type (T[]).</summary>
    Array,
    /// <summary>A list type (List&lt;T&gt;, IList&lt;T&gt;, etc).</summary>
    List,
    /// <summary>A dictionary type (Dictionary&lt;string, T&gt;).</summary>
    Dictionary
}
