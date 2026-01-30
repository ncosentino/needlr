// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Generates IValidateOptions implementations for options classes with validation.
/// </summary>
internal static class OptionsCodeGenerator
{
    internal static string GenerateOptionsValidatorsSource(IReadOnlyList<DiscoveredOptions> optionsWithValidators, string assemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = GeneratorHelpers.SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Generated Options Validators");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using System.Linq;");
        builder.AppendLine();
        builder.AppendLine("using Microsoft.Extensions.Options;");
        builder.AppendLine();
        builder.AppendLine("using NexusLabs.Needlr.Generators;");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();

        // Generate validator class for each options type with a validator method
        foreach (var opt in optionsWithValidators)
        {
            if (!opt.HasValidatorMethod || opt.ValidatorMethod == null)
                continue;

            var shortTypeName = GeneratorHelpers.GetShortTypeName(opt.TypeName);
            var validatorClassName = shortTypeName + "Validator";

            // Determine which type has the validator method
            var validatorTargetType = opt.HasExternalValidator ? opt.ValidatorTypeName! : opt.TypeName;

            builder.AppendLine("/// <summary>");
            builder.AppendLine($"/// Generated validator for <see cref=\"{opt.TypeName}\"/>.");
            if (opt.HasExternalValidator)
            {
                builder.AppendLine($"/// Uses external validator <see cref=\"{validatorTargetType}\"/>.");
            }
            else
            {
                builder.AppendLine("/// Calls the validation method on the options instance.");
            }
            builder.AppendLine("/// </summary>");
            builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
            builder.AppendLine($"public sealed class {validatorClassName} : IValidateOptions<{opt.TypeName}>");
            builder.AppendLine("{");

            if (opt.HasExternalValidator && !opt.ValidatorMethod.Value.IsStatic)
            {
                // External validator needs to be injected for instance methods
                builder.AppendLine($"    private readonly {validatorTargetType} _validator;");
                builder.AppendLine();
                builder.AppendLine($"    public {validatorClassName}({validatorTargetType} validator)");
                builder.AppendLine("    {");
                builder.AppendLine("        _validator = validator;");
                builder.AppendLine("    }");
                builder.AppendLine();
            }

            builder.AppendLine($"    public ValidateOptionsResult Validate(string? name, {opt.TypeName} options)");
            builder.AppendLine("    {");
            builder.AppendLine("        var errors = new List<string>();");

            // Generate the foreach to iterate errors
            string validationCall;
            if (opt.HasExternalValidator)
            {
                if (opt.ValidatorMethod.Value.IsStatic)
                {
                    // Static method on external type: ExternalValidator.ValidateMethod(options)
                    validationCall = $"{validatorTargetType}.{opt.ValidatorMethod.Value.MethodName}(options)";
                }
                else
                {
                    // Instance method on external type: _validator.ValidateMethod(options)
                    validationCall = $"_validator.{opt.ValidatorMethod.Value.MethodName}(options)";
                }
            }
            else if (opt.ValidatorMethod.Value.IsStatic)
            {
                // Static method on options type: OptionsType.ValidateMethod(options)
                validationCall = $"{opt.TypeName}.{opt.ValidatorMethod.Value.MethodName}(options)";
            }
            else
            {
                // Instance method on options type: options.ValidateMethod()
                validationCall = $"options.{opt.ValidatorMethod.Value.MethodName}()";
            }

            builder.AppendLine($"        foreach (var error in {validationCall})");
            builder.AppendLine("        {");
            builder.AppendLine("            // Support both string and ValidationError (ValidationError.ToString() returns formatted message)");
            builder.AppendLine("            var errorMessage = error?.ToString() ?? string.Empty;");
            builder.AppendLine("            if (!string.IsNullOrEmpty(errorMessage))");
            builder.AppendLine("            {");
            builder.AppendLine("                errors.Add(errorMessage);");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        if (errors.Count > 0)");
            builder.AppendLine("        {");
            builder.AppendLine($"            return ValidateOptionsResult.Fail(errors);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        return ValidateOptionsResult.Success;");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// Generates IValidateOptions implementations for options classes with DataAnnotation attributes.
    /// This enables AOT-compatible validation without reflection.
    /// </summary>
    internal static string GenerateDataAnnotationsValidatorsSource(
        IReadOnlyList<DiscoveredOptions> optionsWithDataAnnotations, 
        string assemblyName, 
        BreadcrumbWriter breadcrumbs, 
        string? projectDirectory)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = GeneratorHelpers.SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Generated DataAnnotations Validators");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using System.Text.RegularExpressions;");
        builder.AppendLine();
        builder.AppendLine("using Microsoft.Extensions.Options;");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();

        foreach (var opt in optionsWithDataAnnotations)
        {
            if (!opt.HasDataAnnotations)
                continue;

            var shortTypeName = GeneratorHelpers.GetShortTypeName(opt.TypeName);
            var validatorClassName = shortTypeName + "DataAnnotationsValidator";

            builder.AppendLine("/// <summary>");
            builder.AppendLine($"/// Generated DataAnnotations validator for <see cref=\"{opt.TypeName}\"/>.");
            builder.AppendLine("/// Validates DataAnnotation attributes without reflection (AOT-compatible).");
            builder.AppendLine("/// </summary>");
            builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
            builder.AppendLine($"public sealed class {validatorClassName} : IValidateOptions<{opt.TypeName}>");
            builder.AppendLine("{");
            builder.AppendLine($"    public ValidateOptionsResult Validate(string? name, {opt.TypeName} options)");
            builder.AppendLine("    {");
            builder.AppendLine("        var errors = new List<string>();");
            builder.AppendLine();

            // Generate validation for each property with DataAnnotations
            foreach (var prop in opt.Properties)
            {
                if (!prop.HasDataAnnotations)
                    continue;

                foreach (var annotation in prop.DataAnnotations)
                {
                    GenerateDataAnnotationValidation(builder, prop, annotation);
                }
            }

            builder.AppendLine("        if (errors.Count > 0)");
            builder.AppendLine("        {");
            builder.AppendLine("            return ValidateOptionsResult.Fail(errors);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        return ValidateOptionsResult.Success;");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void GenerateDataAnnotationValidation(StringBuilder builder, OptionsPropertyInfo prop, DataAnnotationInfo annotation)
    {
        var propName = prop.Name;
        var errorMsg = annotation.ErrorMessage;

        switch (annotation.Kind)
        {
            case DataAnnotationKind.Required:
                var requiredError = errorMsg ?? $"The {propName} field is required.";
                if (prop.TypeName.Contains("string"))
                {
                    builder.AppendLine($"        if (string.IsNullOrEmpty(options.{propName}))");
                }
                else
                {
                    builder.AppendLine($"        if (options.{propName} == null)");
                }
                builder.AppendLine("        {");
                builder.AppendLine($"            errors.Add(\"{EscapeString(requiredError)}\");");
                builder.AppendLine("        }");
                builder.AppendLine();
                break;

            case DataAnnotationKind.Range:
                var min = annotation.Minimum;
                var max = annotation.Maximum;
                var rangeError = errorMsg ?? $"The field {propName} must be between {min} and {max}.";
                builder.AppendLine($"        if (options.{propName} < {min} || options.{propName} > {max})");
                builder.AppendLine("        {");
                builder.AppendLine($"            errors.Add(\"{EscapeString(rangeError)}\");");
                builder.AppendLine("        }");
                builder.AppendLine();
                break;

            case DataAnnotationKind.StringLength:
                var maxLen = annotation.Maximum;
                var minLen = annotation.MinimumLength ?? 0;
                var strLenError = errorMsg ?? $"The field {propName} must be a string with a maximum length of {maxLen}.";
                if (minLen > 0)
                {
                    builder.AppendLine($"        if (options.{propName}?.Length < {minLen} || options.{propName}?.Length > {maxLen})");
                }
                else
                {
                    builder.AppendLine($"        if (options.{propName}?.Length > {maxLen})");
                }
                builder.AppendLine("        {");
                builder.AppendLine($"            errors.Add(\"{EscapeString(strLenError)}\");");
                builder.AppendLine("        }");
                builder.AppendLine();
                break;

            case DataAnnotationKind.MinLength:
                var minLenVal = annotation.MinimumLength ?? 0;
                var minLenError = errorMsg ?? $"The field {propName} must have a minimum length of {minLenVal}.";
                builder.AppendLine($"        if (options.{propName}?.Length < {minLenVal})");
                builder.AppendLine("        {");
                builder.AppendLine($"            errors.Add(\"{EscapeString(minLenError)}\");");
                builder.AppendLine("        }");
                builder.AppendLine();
                break;

            case DataAnnotationKind.MaxLength:
                var maxLenVal = annotation.Maximum;
                var maxLenError = errorMsg ?? $"The field {propName} must have a maximum length of {maxLenVal}.";
                builder.AppendLine($"        if (options.{propName}?.Length > {maxLenVal})");
                builder.AppendLine("        {");
                builder.AppendLine($"            errors.Add(\"{EscapeString(maxLenError)}\");");
                builder.AppendLine("        }");
                builder.AppendLine();
                break;

            case DataAnnotationKind.RegularExpression:
                var pattern = annotation.Pattern ?? "";
                var regexError = errorMsg ?? $"The field {propName} must match the regular expression '{pattern}'.";
                // Use verbatim string for pattern
                builder.AppendLine($"        if (options.{propName} != null && !Regex.IsMatch(options.{propName}, @\"{EscapeVerbatimString(pattern)}\"))");
                builder.AppendLine("        {");
                builder.AppendLine($"            errors.Add(\"{EscapeString(regexError)}\");");
                builder.AppendLine("        }");
                builder.AppendLine();
                break;

            case DataAnnotationKind.EmailAddress:
                var emailError = errorMsg ?? $"The {propName} field is not a valid e-mail address.";
                builder.AppendLine($"        if (!string.IsNullOrEmpty(options.{propName}) && !Regex.IsMatch(options.{propName}, @\"^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$\"))");
                builder.AppendLine("        {");
                builder.AppendLine($"            errors.Add(\"{EscapeString(emailError)}\");");
                builder.AppendLine("        }");
                builder.AppendLine();
                break;

            case DataAnnotationKind.Url:
                var urlError = errorMsg ?? $"The {propName} field is not a valid fully-qualified http, https, or ftp URL.";
                builder.AppendLine($"        if (!string.IsNullOrEmpty(options.{propName}) && !global::System.Uri.TryCreate(options.{propName}, global::System.UriKind.Absolute, out _))");
                builder.AppendLine("        {");
                builder.AppendLine($"            errors.Add(\"{EscapeString(urlError)}\");");
                builder.AppendLine("        }");
                builder.AppendLine();
                break;

            case DataAnnotationKind.Phone:
                var phoneError = errorMsg ?? $"The {propName} field is not a valid phone number.";
                builder.AppendLine($"        if (!string.IsNullOrEmpty(options.{propName}) && !Regex.IsMatch(options.{propName}, @\"^[\\d\\s\\-\\+\\(\\)]+$\"))");
                builder.AppendLine("        {");
                builder.AppendLine($"            errors.Add(\"{EscapeString(phoneError)}\");");
                builder.AppendLine("        }");
                builder.AppendLine();
                break;

            case DataAnnotationKind.Unsupported:
                // Skip unsupported - will be handled by analyzer
                break;
        }
    }

    private static string EscapeString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string EscapeVerbatimString(string s)
    {
        return s.Replace("\"", "\"\"");
    }

    /// <summary>
    /// Generates parameterless constructors for partial positional records with [Options].
    /// This enables configuration binding which requires a parameterless constructor.
    /// </summary>
    internal static string GeneratePositionalRecordConstructorsSource(
        IReadOnlyList<DiscoveredOptions> optionsNeedingConstructors, 
        string assemblyName, 
        BreadcrumbWriter breadcrumbs, 
        string? projectDirectory)
    {
        var builder = new StringBuilder();

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Generated Options Constructors");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        // Group by namespace for cleaner output
        var byNamespace = optionsNeedingConstructors
            .Where(o => o.PositionalRecordInfo != null)
            .GroupBy(o => o.PositionalRecordInfo!.Value.ContainingNamespace)
            .OrderBy(g => g.Key);

        foreach (var namespaceGroup in byNamespace)
        {
            var namespaceName = namespaceGroup.Key;
            
            if (!string.IsNullOrEmpty(namespaceName))
            {
                builder.AppendLine($"namespace {namespaceName};");
                builder.AppendLine();
            }

            foreach (var opt in namespaceGroup)
            {
                var info = opt.PositionalRecordInfo!.Value;
                
                builder.AppendLine("/// <summary>");
                builder.AppendLine($"/// Generated parameterless constructor for configuration binding.");
                builder.AppendLine($"/// Chains to primary constructor with default values.");
                builder.AppendLine("/// </summary>");
                builder.AppendLine($"[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
                builder.AppendLine($"public partial record {info.ShortTypeName}");
                builder.AppendLine("{");
                
                // Build the constructor call with default values for each parameter
                var defaultArgs = new List<string>();
                foreach (var param in info.Parameters)
                {
                    var defaultValue = GetDefaultValueForType(param.TypeName);
                    defaultArgs.Add(defaultValue);
                }
                
                var argsString = string.Join(", ", defaultArgs);
                builder.AppendLine($"    public {info.ShortTypeName}() : this({argsString}) {{ }}");
                builder.AppendLine("}");
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets the default value expression for a given type.
    /// </summary>
    private static string GetDefaultValueForType(string fullyQualifiedTypeName)
    {
        // Handle common types with user-friendly defaults
        return fullyQualifiedTypeName switch
        {
            "global::System.String" or "string" => "string.Empty",
            "global::System.Boolean" or "bool" => "default",
            "global::System.Int32" or "int" => "default",
            "global::System.Int64" or "long" => "default",
            "global::System.Int16" or "short" => "default",
            "global::System.Byte" or "byte" => "default",
            "global::System.SByte" or "sbyte" => "default",
            "global::System.UInt32" or "uint" => "default",
            "global::System.UInt64" or "ulong" => "default",
            "global::System.UInt16" or "ushort" => "default",
            "global::System.Single" or "float" => "default",
            "global::System.Double" or "double" => "default",
            "global::System.Decimal" or "decimal" => "default",
            "global::System.Char" or "char" => "default",
            "global::System.DateTime" => "default",
            "global::System.DateTimeOffset" => "default",
            "global::System.TimeSpan" => "default",
            "global::System.Guid" => "default",
            // For nullable types and reference types, use default (which gives null for reference types)
            // For other value types, use default
            _ when fullyQualifiedTypeName.EndsWith("?") => "default",
            _ => "default!"  // Reference types need null-forgiving operator
        };
    }
}

