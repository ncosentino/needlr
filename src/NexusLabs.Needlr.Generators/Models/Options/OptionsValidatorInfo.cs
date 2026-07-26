namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a validation method.
/// </summary>
internal readonly struct OptionsValidatorInfo
{
    public OptionsValidatorInfo(
        string methodName,
        bool isStatic,
        bool usesOptionsValidatorInterface)
    {
        MethodName = methodName;
        IsStatic = isStatic;
        UsesOptionsValidatorInterface = usesOptionsValidatorInterface;
    }

    /// <summary>Name of the validator method.</summary>
    public string MethodName { get; }

    /// <summary>True if the method is static.</summary>
    public bool IsStatic { get; }

    /// <summary>
    /// True when validation must be invoked through
    /// <c>IOptionsValidator&lt;TOptions&gt;</c>, such as an explicit interface implementation.
    /// </summary>
    public bool UsesOptionsValidatorInterface { get; }
}
