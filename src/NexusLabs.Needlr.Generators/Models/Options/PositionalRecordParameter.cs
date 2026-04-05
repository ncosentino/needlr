namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// A parameter in a positional record's primary constructor.
/// </summary>
internal readonly struct PositionalRecordParameter
{
    public PositionalRecordParameter(string name, string typeName)
    {
        Name = name;
        TypeName = typeName;
    }

    public string Name { get; }
    public string TypeName { get; }
}
