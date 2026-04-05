namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information parsed from [GenerateTypeRegistry] attribute.
/// </summary>
internal readonly struct AttributeInfo
{
    public AttributeInfo(string[]? namespacePrefixes, bool includeSelf)
    {
        NamespacePrefixes = namespacePrefixes;
        IncludeSelf = includeSelf;
    }

    public string[]? NamespacePrefixes { get; }
    public bool IncludeSelf { get; }
}
