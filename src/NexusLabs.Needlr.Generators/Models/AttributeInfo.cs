namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information parsed from [GenerateTypeRegistry] attribute.
/// </summary>
internal readonly struct AttributeInfo
{
    public AttributeInfo(string[]? namespacePrefixes, string[]? excludeNamespacePrefixes, bool includeSelf)
    {
        NamespacePrefixes = namespacePrefixes;
        ExcludeNamespacePrefixes = excludeNamespacePrefixes;
        IncludeSelf = includeSelf;
    }

    public string[]? NamespacePrefixes { get; }
    public string[]? ExcludeNamespacePrefixes { get; }
    public bool IncludeSelf { get; }
}
