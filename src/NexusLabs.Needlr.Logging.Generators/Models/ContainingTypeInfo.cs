namespace NexusLabs.Needlr.Logging.Generators.Models;

/// <summary>
/// One link in the chain of partial types that contain a discovered <c>[NeedlrLoggerMessage]</c> method.
/// </summary>
/// <remarks>
/// The chain is ordered outermost to innermost so the generator can re-open each nesting level as a
/// <c>partial</c> declaration.
/// </remarks>
internal readonly struct ContainingTypeInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContainingTypeInfo"/> struct.
    /// </summary>
    /// <param name="keyword">The type keyword (e.g. <c>class</c>, <c>struct</c>, <c>record</c>).</param>
    /// <param name="name">The simple type name.</param>
    /// <param name="typeParameterList">The type parameter list (e.g. <c>&lt;T&gt;</c>), or an empty string when non-generic.</param>
    public ContainingTypeInfo(string keyword, string name, string typeParameterList)
    {
        Keyword = keyword;
        Name = name;
        TypeParameterList = typeParameterList;
    }

    /// <summary>
    /// Gets the type keyword used to re-open the partial declaration.
    /// </summary>
    public string Keyword { get; }

    /// <summary>
    /// Gets the simple type name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type parameter list, or an empty string when the type is non-generic.
    /// </summary>
    public string TypeParameterList { get; }
}
