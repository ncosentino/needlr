using System;
using System.Collections.Generic;
using System.Linq;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a discovered options type (from [Options]).
/// </summary>
internal readonly struct DiscoveredOptions
{
    public DiscoveredOptions(
        string typeName,
        string sectionName,
        string? name,
        bool validateOnStart,
        string assemblyName,
        string? sourceFilePath = null,
        OptionsValidatorInfo? validatorMethod = null,
        string? validateMethodOverride = null,
        string? validatorTypeName = null,
        PositionalRecordInfo? positionalRecordInfo = null,
        IReadOnlyList<OptionsPropertyInfo>? properties = null)
    {
        TypeName = typeName;
        SectionName = sectionName;
        Name = name;
        ValidateOnStart = validateOnStart;
        AssemblyName = assemblyName;
        SourceFilePath = sourceFilePath;
        ValidatorMethod = validatorMethod;
        ValidateMethodOverride = validateMethodOverride;
        ValidatorTypeName = validatorTypeName;
        PositionalRecordInfo = positionalRecordInfo;
        Properties = properties ?? Array.Empty<OptionsPropertyInfo>();
    }

    /// <summary>Fully qualified type name of the options class.</summary>
    public string TypeName { get; }

    /// <summary>Configuration section name (e.g., "Database").</summary>
    public string SectionName { get; }

    /// <summary>Named options name (e.g., "Primary"), or null for default options.</summary>
    public string? Name { get; }

    /// <summary>Whether to validate options on startup.</summary>
    public bool ValidateOnStart { get; }

    public string AssemblyName { get; }
    public string? SourceFilePath { get; }

    /// <summary>Information about the validation method (discovered or specified).</summary>
    public OptionsValidatorInfo? ValidatorMethod { get; }

    /// <summary>Custom validation method name override from [Options(ValidateMethod = "...")], or null to use convention.</summary>
    public string? ValidateMethodOverride { get; }

    /// <summary>External validator type name from [Options(Validator = typeof(...))], or null to use options class.</summary>
    public string? ValidatorTypeName { get; }

    /// <summary>Information about positional record primary constructor, if applicable.</summary>
    public PositionalRecordInfo? PositionalRecordInfo { get; }

    /// <summary>Bindable properties for AOT code generation.</summary>
    public IReadOnlyList<OptionsPropertyInfo> Properties { get; }

    /// <summary>True if this is a named options registration (not default).</summary>
    public bool IsNamed => Name != null;

    /// <summary>True if this options type has a custom validator method.</summary>
    public bool HasValidatorMethod => ValidatorMethod != null;

    /// <summary>True if an external validator type is specified.</summary>
    public bool HasExternalValidator => ValidatorTypeName != null;

    /// <summary>True if this is a positional record that needs a generated parameterless constructor.</summary>
    public bool NeedsGeneratedConstructor => PositionalRecordInfo?.IsPartial == true;

    /// <summary>True if this is a non-partial positional record (will emit diagnostic).</summary>
    public bool IsNonPartialPositionalRecord => PositionalRecordInfo != null && !PositionalRecordInfo.Value.IsPartial;

    /// <summary>True if this type has any init-only properties (requires factory pattern in AOT).</summary>
    public bool HasInitOnlyProperties => Properties.Any(p => p.HasInitOnlySetter);

    /// <summary>True if this is a positional record (uses constructor binding in AOT).</summary>
    public bool IsPositionalRecord => PositionalRecordInfo != null;

    /// <summary>True if this type requires factory pattern (Options.Create) instead of Configure delegate in AOT.</summary>
    public bool RequiresFactoryPattern => IsPositionalRecord || HasInitOnlyProperties;

    /// <summary>True if any property has DataAnnotation validation attributes.</summary>
    public bool HasDataAnnotations => Properties.Any(p => p.HasDataAnnotations);
}
