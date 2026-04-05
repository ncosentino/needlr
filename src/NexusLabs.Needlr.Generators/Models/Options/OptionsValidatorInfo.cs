namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a validation method.
/// </summary>
internal readonly struct OptionsValidatorInfo
{
    public OptionsValidatorInfo(string methodName, bool isStatic)
    {
        MethodName = methodName;
        IsStatic = isStatic;
    }

    /// <summary>Name of the validator method.</summary>
    public string MethodName { get; }

    /// <summary>True if the method is static.</summary>
    public bool IsStatic { get; }
}
