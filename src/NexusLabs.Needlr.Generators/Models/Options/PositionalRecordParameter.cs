namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// A parameter in a positional record's primary constructor.
/// </summary>
internal readonly struct PositionalRecordParameter
{
    public PositionalRecordParameter(OptionsPropertyInfo property, bool isValueType)
    {
        Property = property;
        IsValueType = isValueType;
    }

    public OptionsPropertyInfo Property { get; }
    public bool IsValueType { get; }
}
