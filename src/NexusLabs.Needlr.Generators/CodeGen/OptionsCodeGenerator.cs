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

    // -----------------------------------------------------------------------
    // Options registration code generation (moved from TypeRegistryGenerator)
    // -----------------------------------------------------------------------

    internal static void GenerateReflectionOptionsRegistration(StringBuilder builder, IReadOnlyList<DiscoveredOptions> options, string safeAssemblyName, BreadcrumbWriter breadcrumbs)
    {
        // Track external validators to register (avoid duplicates)
        var externalValidatorsToRegister = new HashSet<string>();

        foreach (var opt in options)
        {
            var typeName = opt.TypeName;

            if (opt.ValidateOnStart)
            {
                // Use AddOptions pattern for validation support
                // services.AddOptions<T>().BindConfiguration("Section").ValidateDataAnnotations().ValidateOnStart();
                builder.Append($"        services.AddOptions<{typeName}>");

                if (opt.IsNamed)
                {
                    builder.Append($"(\"{opt.Name}\")");
                }
                else
                {
                    builder.Append("()");
                }

                builder.Append($".BindConfiguration(\"{opt.SectionName}\")");
                builder.Append(".ValidateDataAnnotations()");
                builder.AppendLine(".ValidateOnStart();");

                // Register source-generated DataAnnotations validator if present
                // This runs alongside .ValidateDataAnnotations() - source-gen handles supported attributes,
                // reflection fallback handles unsupported attributes (like [CustomValidation])
                if (opt.HasDataAnnotations)
                {
                    var shortTypeName = GeneratorHelpers.GetShortTypeName(typeName);
                    var dataAnnotationsValidatorClassName = $"global::{safeAssemblyName}.Generated.{shortTypeName}DataAnnotationsValidator";
                    builder.AppendLine($"        services.AddSingleton<global::Microsoft.Extensions.Options.IValidateOptions<{typeName}>, {dataAnnotationsValidatorClassName}>();");
                }

                // If there's a custom validator method, register the generated validator
                if (opt.HasValidatorMethod)
                {
                    var shortTypeName = GeneratorHelpers.GetShortTypeName(typeName);
                    var validatorClassName = $"global::{safeAssemblyName}.Generated.{shortTypeName}Validator";
                    builder.AppendLine($"        services.AddSingleton<global::Microsoft.Extensions.Options.IValidateOptions<{typeName}>, {validatorClassName}>();");

                    // If external validator with instance method, register it too
                    if (opt.HasExternalValidator && opt.ValidatorMethod != null && !opt.ValidatorMethod.Value.IsStatic)
                    {
                        externalValidatorsToRegister.Add(opt.ValidatorTypeName!);
                    }
                }
            }
            else if (opt.IsNamed)
            {
                // Named options: OptionsConfigurationServiceCollectionExtensions.Configure<T>(services, "name", section)
                builder.AppendLine($"        global::Microsoft.Extensions.DependencyInjection.OptionsConfigurationServiceCollectionExtensions.Configure<{typeName}>(services, \"{opt.Name}\", configuration.GetSection(\"{opt.SectionName}\"));");
            }
            else
            {
                // Default options: OptionsConfigurationServiceCollectionExtensions.Configure<T>(services, section)
                builder.AppendLine($"        global::Microsoft.Extensions.DependencyInjection.OptionsConfigurationServiceCollectionExtensions.Configure<{typeName}>(services, configuration.GetSection(\"{opt.SectionName}\"));");
            }
        }

        // Register external validators that have instance methods
        foreach (var validatorType in externalValidatorsToRegister)
        {
            builder.AppendLine($"        services.AddSingleton<{validatorType}>();");
        }
    }

    internal static void GenerateAotOptionsRegistration(StringBuilder builder, IReadOnlyList<DiscoveredOptions> options, string safeAssemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        breadcrumbs.WriteInlineComment(builder, "        ", "AOT-compatible options binding (no reflection)");

        var externalValidatorsToRegister = new HashSet<string>();

        foreach (var opt in options)
        {
            var typeName = opt.TypeName;
            builder.AppendLine();
            builder.AppendLine($"        // Bind {opt.SectionName} section to {GeneratorHelpers.GetShortTypeName(typeName)}");

            // Choose binding pattern based on type characteristics
            if (opt.IsPositionalRecord)
            {
                // Positional records: Use constructor binding with Options.Create
                GeneratePositionalRecordBinding(builder, opt, safeAssemblyName, externalValidatorsToRegister);
            }
            else if (opt.HasInitOnlyProperties)
            {
                // Classes/records with init-only properties: Use object initializer with Options.Create
                GenerateInitOnlyBinding(builder, opt, safeAssemblyName, externalValidatorsToRegister);
            }
            else
            {
                // Regular classes with setters: Use Configure delegate pattern
                GenerateConfigureBinding(builder, opt, safeAssemblyName, externalValidatorsToRegister);
            }
        }

        // Register external validators that have instance methods
        foreach (var validatorType in externalValidatorsToRegister)
        {
            builder.AppendLine($"        services.AddSingleton<{validatorType}>();");
        }
    }

    private static void GenerateConfigureBinding(StringBuilder builder, DiscoveredOptions opt, string safeAssemblyName, HashSet<string> externalValidatorsToRegister)
    {
        var typeName = opt.TypeName;

        if (opt.IsNamed)
        {
            builder.AppendLine($"        services.AddOptions<{typeName}>(\"{opt.Name}\")");
        }
        else
        {
            builder.AppendLine($"        services.AddOptions<{typeName}>()");
        }

        builder.AppendLine("            .Configure<IConfiguration>((options, config) =>");
        builder.AppendLine("            {");
        builder.AppendLine($"                var section = config.GetSection(\"{opt.SectionName}\");");

        // Generate property binding for each property
        var propIndex = 0;
        foreach (var prop in opt.Properties)
        {
            GeneratePropertyBinding(builder, prop, propIndex);
            propIndex++;
        }

        builder.Append("            })");

        // Add validation chain if ValidateOnStart is enabled
        if (opt.ValidateOnStart)
        {
            builder.AppendLine();
            builder.Append("            .ValidateOnStart()");
        }

        builder.AppendLine(";");

        RegisterValidator(builder, opt, safeAssemblyName, externalValidatorsToRegister);
    }

    private static void GenerateInitOnlyBinding(StringBuilder builder, DiscoveredOptions opt, string safeAssemblyName, HashSet<string> externalValidatorsToRegister)
    {
        var typeName = opt.TypeName;

        // Use AddSingleton with IOptions<T> factory pattern for init-only
        builder.AppendLine($"        services.AddSingleton<global::Microsoft.Extensions.Options.IOptions<{typeName}>>(sp =>");
        builder.AppendLine("        {");
        builder.AppendLine($"            var config = sp.GetRequiredService<IConfiguration>();");
        builder.AppendLine($"            var section = config.GetSection(\"{opt.SectionName}\");");

        // Generate parsing variables first
        var propIndex = 0;
        foreach (var prop in opt.Properties)
        {
            GeneratePropertyParseVariable(builder, prop, propIndex);
            propIndex++;
        }

        // Create object with initializer
        builder.AppendLine($"            return global::Microsoft.Extensions.Options.Options.Create(new {typeName}");
        builder.AppendLine("            {");

        propIndex = 0;
        foreach (var prop in opt.Properties)
        {
            var comma = propIndex < opt.Properties.Count - 1 ? "," : "";
            GeneratePropertyInitializer(builder, prop, propIndex, comma);
            propIndex++;
        }

        builder.AppendLine("            });");
        builder.AppendLine("        });");

        // For validation with factory pattern, we need to register the validator separately
        if (opt.ValidateOnStart)
        {
            RegisterValidatorForFactory(builder, opt, safeAssemblyName, externalValidatorsToRegister);
        }
    }

    private static void GeneratePositionalRecordBinding(StringBuilder builder, DiscoveredOptions opt, string safeAssemblyName, HashSet<string> externalValidatorsToRegister)
    {
        var typeName = opt.TypeName;
        var recordInfo = opt.PositionalRecordInfo!.Value;

        // Use AddSingleton with IOptions<T> factory pattern for positional records
        builder.AppendLine($"        services.AddSingleton<global::Microsoft.Extensions.Options.IOptions<{typeName}>>(sp =>");
        builder.AppendLine("        {");
        builder.AppendLine($"            var config = sp.GetRequiredService<IConfiguration>();");
        builder.AppendLine($"            var section = config.GetSection(\"{opt.SectionName}\");");

        // Generate parsing variables for each constructor parameter
        var paramIndex = 0;
        foreach (var param in recordInfo.Parameters)
        {
            GenerateParameterParseVariable(builder, param, paramIndex);
            paramIndex++;
        }

        // Create record with constructor
        builder.Append($"            return global::Microsoft.Extensions.Options.Options.Create(new {typeName}(");

        paramIndex = 0;
        foreach (var param in recordInfo.Parameters)
        {
            if (paramIndex > 0) builder.Append(", ");
            builder.Append($"p{paramIndex}");
            paramIndex++;
        }

        builder.AppendLine("));");
        builder.AppendLine("        });");

        // For validation with factory pattern, we need to register the validator separately
        if (opt.ValidateOnStart)
        {
            RegisterValidatorForFactory(builder, opt, safeAssemblyName, externalValidatorsToRegister);
        }
    }

    private static void GeneratePropertyParseVariable(StringBuilder builder, OptionsPropertyInfo prop, int index)
    {
        var varName = $"p{index}";
        var typeName = prop.TypeName;
        var baseTypeName = GetBaseTypeName(typeName);

        // Handle complex types
        if (prop.ComplexTypeKind != ComplexTypeKind.None)
        {
            GenerateComplexTypeParseVariable(builder, prop, index);
            return;
        }

        // Handle enums
        if (prop.IsEnum && prop.EnumTypeName != null)
        {
            var defaultVal = prop.IsNullable ? "null" : "default";
            builder.AppendLine($"            var {varName} = section[\"{prop.Name}\"] is {{ }} v{index} && global::System.Enum.TryParse<{prop.EnumTypeName}>(v{index}, true, out var e{index}) ? e{index} : {defaultVal};");
            return;
        }

        // Handle primitives
        if (baseTypeName == "string" || baseTypeName == "global::System.String")
        {
            var defaultVal = prop.IsNullable ? "null" : "\"\"";
            builder.AppendLine($"            var {varName} = section[\"{prop.Name}\"] ?? {defaultVal};");
        }
        else if (baseTypeName == "int" || baseTypeName == "global::System.Int32")
        {
            var defaultVal = prop.IsNullable ? "null" : "0";
            builder.AppendLine($"            var {varName} = section[\"{prop.Name}\"] is {{ }} v{index} && int.TryParse(v{index}, out var i{index}) ? i{index} : {defaultVal};");
        }
        else if (baseTypeName == "bool" || baseTypeName == "global::System.Boolean")
        {
            var defaultVal = prop.IsNullable ? "null" : "false";
            builder.AppendLine($"            var {varName} = section[\"{prop.Name}\"] is {{ }} v{index} && bool.TryParse(v{index}, out var b{index}) ? b{index} : {defaultVal};");
        }
        else if (baseTypeName == "double" || baseTypeName == "global::System.Double")
        {
            var defaultVal = prop.IsNullable ? "null" : "0.0";
            builder.AppendLine($"            var {varName} = section[\"{prop.Name}\"] is {{ }} v{index} && double.TryParse(v{index}, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d{index}) ? d{index} : {defaultVal};");
        }
        else
        {
            // Default to default value for unsupported types
            builder.AppendLine($"            var {varName} = default({typeName}); // Unsupported type");
        }
    }

    private static void GenerateComplexTypeParseVariable(StringBuilder builder, OptionsPropertyInfo prop, int index)
    {
        var varName = $"p{index}";
        var sectionVar = $"sec{index}";

        switch (prop.ComplexTypeKind)
        {
            case ComplexTypeKind.NestedObject:
                builder.AppendLine($"            var {sectionVar} = section.GetSection(\"{prop.Name}\");");
                builder.AppendLine($"            var {varName} = new {GetNonNullableTypeName(prop.TypeName)}();");
                if (prop.NestedProperties != null)
                {
                    var nestedIndex = index * 100;
                    foreach (var nested in prop.NestedProperties)
                    {
                        GenerateNestedPropertyAssignment(builder, nested, nestedIndex, varName, sectionVar);
                        nestedIndex++;
                    }
                }
                break;

            case ComplexTypeKind.List:
                var listElemType = prop.ElementTypeName ?? "string";
                builder.AppendLine($"            var {sectionVar} = section.GetSection(\"{prop.Name}\");");
                builder.AppendLine($"            var {varName} = new global::System.Collections.Generic.List<{listElemType}>();");
                builder.AppendLine($"            foreach (var child in {sectionVar}.GetChildren())");
                builder.AppendLine("            {");
                if (prop.NestedProperties != null && prop.NestedProperties.Count > 0)
                {
                    builder.AppendLine($"                var item = new {listElemType}();");
                    var ni = index * 100;
                    foreach (var np in prop.NestedProperties)
                    {
                        GenerateChildPropertyAssignment(builder, np, ni, "item", "child");
                        ni++;
                    }
                    builder.AppendLine($"                {varName}.Add(item);");
                }
                else
                {
                    builder.AppendLine($"                if (child.Value is {{ }} val) {varName}.Add(val);");
                }
                builder.AppendLine("            }");
                break;

            case ComplexTypeKind.Dictionary:
                var dictValType = prop.ElementTypeName ?? "string";
                builder.AppendLine($"            var {sectionVar} = section.GetSection(\"{prop.Name}\");");
                builder.AppendLine($"            var {varName} = new global::System.Collections.Generic.Dictionary<string, {dictValType}>();");
                builder.AppendLine($"            foreach (var child in {sectionVar}.GetChildren())");
                builder.AppendLine("            {");
                if (prop.NestedProperties != null && prop.NestedProperties.Count > 0)
                {
                    builder.AppendLine($"                var item = new {dictValType}();");
                    var ni = index * 100;
                    foreach (var np in prop.NestedProperties)
                    {
                        GenerateChildPropertyAssignment(builder, np, ni, "item", "child");
                        ni++;
                    }
                    builder.AppendLine($"                {varName}[child.Key] = item;");
                }
                else if (dictValType == "int" || dictValType == "global::System.Int32")
                {
                    builder.AppendLine($"                if (child.Value is {{ }} val && int.TryParse(val, out var iv)) {varName}[child.Key] = iv;");
                }
                else
                {
                    builder.AppendLine($"                if (child.Value is {{ }} val) {varName}[child.Key] = val;");
                }
                builder.AppendLine("            }");
                break;

            default:
                builder.AppendLine($"            var {varName} = default({prop.TypeName}); // Complex type");
                break;
        }
    }

    private static void GenerateNestedPropertyAssignment(StringBuilder builder, OptionsPropertyInfo prop, int index, string targetVar, string sectionVar)
    {
        var varName = $"nv{index}";
        var baseTypeName = GetBaseTypeName(prop.TypeName);

        if (baseTypeName == "string" || baseTypeName == "global::System.String")
        {
            builder.AppendLine($"            if ({sectionVar}[\"{prop.Name}\"] is {{ }} {varName}) {targetVar}.{prop.Name} = {varName};");
        }
        else if (baseTypeName == "int" || baseTypeName == "global::System.Int32")
        {
            builder.AppendLine($"            if ({sectionVar}[\"{prop.Name}\"] is {{ }} {varName} && int.TryParse({varName}, out var ni{index})) {targetVar}.{prop.Name} = ni{index};");
        }
        else if (baseTypeName == "bool" || baseTypeName == "global::System.Boolean")
        {
            builder.AppendLine($"            if ({sectionVar}[\"{prop.Name}\"] is {{ }} {varName} && bool.TryParse({varName}, out var nb{index})) {targetVar}.{prop.Name} = nb{index};");
        }
    }

    private static void GenerateChildPropertyAssignment(StringBuilder builder, OptionsPropertyInfo prop, int index, string targetVar, string sectionVar)
    {
        var varName = $"cv{index}";
        var baseTypeName = GetBaseTypeName(prop.TypeName);

        if (baseTypeName == "string" || baseTypeName == "global::System.String")
        {
            builder.AppendLine($"                if ({sectionVar}[\"{prop.Name}\"] is {{ }} {varName}) {targetVar}.{prop.Name} = {varName};");
        }
        else if (baseTypeName == "int" || baseTypeName == "global::System.Int32")
        {
            builder.AppendLine($"                if ({sectionVar}[\"{prop.Name}\"] is {{ }} {varName} && int.TryParse({varName}, out var ci{index})) {targetVar}.{prop.Name} = ci{index};");
        }
    }

    private static void GeneratePropertyInitializer(StringBuilder builder, OptionsPropertyInfo prop, int index, string comma)
    {
        builder.AppendLine($"                {prop.Name} = p{index}{comma}");
    }

    private static void GenerateParameterParseVariable(StringBuilder builder, PositionalRecordParameter param, int index)
    {
        var varName = $"p{index}";
        var typeName = param.TypeName;
        var baseTypeName = GetBaseTypeName(typeName);

        // Check if it's an enum
        // For simplicity, check if it's a known primitive, otherwise assume it could be an enum
        if (baseTypeName == "string" || baseTypeName == "global::System.String")
        {
            builder.AppendLine($"            var {varName} = section[\"{param.Name}\"] ?? \"\";");
        }
        else if (baseTypeName == "int" || baseTypeName == "global::System.Int32")
        {
            builder.AppendLine($"            var {varName} = section[\"{param.Name}\"] is {{ }} v{index} && int.TryParse(v{index}, out var i{index}) ? i{index} : 0;");
        }
        else if (baseTypeName == "bool" || baseTypeName == "global::System.Boolean")
        {
            builder.AppendLine($"            var {varName} = section[\"{param.Name}\"] is {{ }} v{index} && bool.TryParse(v{index}, out var b{index}) && b{index};");
        }
        else if (baseTypeName == "double" || baseTypeName == "global::System.Double")
        {
            builder.AppendLine($"            var {varName} = section[\"{param.Name}\"] is {{ }} v{index} && double.TryParse(v{index}, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d{index}) ? d{index} : 0.0;");
        }
        else
        {
            // Try enum parsing for other types
            builder.AppendLine($"            var {varName} = section[\"{param.Name}\"] is {{ }} v{index} && global::System.Enum.TryParse<{typeName}>(v{index}, true, out var e{index}) ? e{index} : default({typeName});");
        }
    }

    private static void RegisterValidator(StringBuilder builder, DiscoveredOptions opt, string safeAssemblyName, HashSet<string> externalValidatorsToRegister)
    {
        var typeName = opt.TypeName;
        var shortTypeName = GeneratorHelpers.GetShortTypeName(typeName);

        // Register DataAnnotations validator if present
        if (opt.HasDataAnnotations)
        {
            var dataAnnotationsValidatorClassName = $"global::{safeAssemblyName}.Generated.{shortTypeName}DataAnnotationsValidator";
            builder.AppendLine($"        services.AddSingleton<global::Microsoft.Extensions.Options.IValidateOptions<{typeName}>, {dataAnnotationsValidatorClassName}>();");
        }

        if (opt.ValidateOnStart && opt.HasValidatorMethod)
        {
            var validatorClassName = $"global::{safeAssemblyName}.Generated.{shortTypeName}Validator";
            builder.AppendLine($"        services.AddSingleton<global::Microsoft.Extensions.Options.IValidateOptions<{typeName}>, {validatorClassName}>();");

            if (opt.HasExternalValidator && opt.ValidatorMethod != null && !opt.ValidatorMethod.Value.IsStatic)
            {
                externalValidatorsToRegister.Add(opt.ValidatorTypeName!);
            }
        }
    }

    private static void RegisterValidatorForFactory(StringBuilder builder, DiscoveredOptions opt, string safeAssemblyName, HashSet<string> externalValidatorsToRegister)
    {
        var typeName = opt.TypeName;
        var shortTypeName = GeneratorHelpers.GetShortTypeName(typeName);

        // For factory pattern, we need to add OptionsBuilder validation manually
        // Since we're using AddSingleton<IOptions<T>>, we also need to register for IOptionsSnapshot and IOptionsMonitor
        builder.AppendLine($"        services.AddSingleton<global::Microsoft.Extensions.Options.IOptionsSnapshot<{typeName}>>(sp => new global::Microsoft.Extensions.Options.OptionsManager<{typeName}>(sp.GetRequiredService<global::Microsoft.Extensions.Options.IOptionsFactory<{typeName}>>()));");

        // Add startup validation
        builder.AppendLine($"        services.AddOptions<{typeName}>().ValidateOnStart();");

        // Register DataAnnotations validator if present
        if (opt.HasDataAnnotations)
        {
            var dataAnnotationsValidatorClassName = $"global::{safeAssemblyName}.Generated.{shortTypeName}DataAnnotationsValidator";
            builder.AppendLine($"        services.AddSingleton<global::Microsoft.Extensions.Options.IValidateOptions<{typeName}>, {dataAnnotationsValidatorClassName}>();");
        }

        if (opt.HasValidatorMethod)
        {
            var validatorClassName = $"global::{safeAssemblyName}.Generated.{shortTypeName}Validator";
            builder.AppendLine($"        services.AddSingleton<global::Microsoft.Extensions.Options.IValidateOptions<{typeName}>, {validatorClassName}>();");

            if (opt.HasExternalValidator && opt.ValidatorMethod != null && !opt.ValidatorMethod.Value.IsStatic)
            {
                externalValidatorsToRegister.Add(opt.ValidatorTypeName!);
            }
        }
    }

    private static void GeneratePropertyBinding(StringBuilder builder, OptionsPropertyInfo prop, int index, string targetPath = "options")
    {
        // Handle complex types first
        if (prop.ComplexTypeKind != ComplexTypeKind.None)
        {
            GenerateComplexTypeBinding(builder, prop, index, targetPath);
            return;
        }

        var varName = $"v{index}";

        // Determine how to parse the value based on type
        var typeName = prop.TypeName;
        var baseTypeName = GetBaseTypeName(typeName);

        builder.Append($"                if (section[\"{prop.Name}\"] is {{ }} {varName}");

        // Check if it's an enum first
        if (prop.IsEnum && prop.EnumTypeName != null)
        {
            builder.AppendLine($" && global::System.Enum.TryParse<{prop.EnumTypeName}>({varName}, true, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "string" || baseTypeName == "global::System.String")
        {
            // String: direct assignment
            builder.AppendLine($") {targetPath}.{prop.Name} = {varName};");
        }
        else if (baseTypeName == "int" || baseTypeName == "global::System.Int32")
        {
            builder.AppendLine($" && int.TryParse({varName}, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "bool" || baseTypeName == "global::System.Boolean")
        {
            builder.AppendLine($" && bool.TryParse({varName}, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "double" || baseTypeName == "global::System.Double")
        {
            builder.AppendLine($" && double.TryParse({varName}, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "float" || baseTypeName == "global::System.Single")
        {
            builder.AppendLine($" && float.TryParse({varName}, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "decimal" || baseTypeName == "global::System.Decimal")
        {
            builder.AppendLine($" && decimal.TryParse({varName}, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "long" || baseTypeName == "global::System.Int64")
        {
            builder.AppendLine($" && long.TryParse({varName}, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "short" || baseTypeName == "global::System.Int16")
        {
            builder.AppendLine($" && short.TryParse({varName}, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "byte" || baseTypeName == "global::System.Byte")
        {
            builder.AppendLine($" && byte.TryParse({varName}, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "char" || baseTypeName == "global::System.Char")
        {
            builder.AppendLine($" && {varName}.Length == 1) {targetPath}.{prop.Name} = {varName}[0];");
        }
        else if (baseTypeName == "global::System.TimeSpan")
        {
            builder.AppendLine($" && global::System.TimeSpan.TryParse({varName}, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "global::System.DateTime")
        {
            builder.AppendLine($" && global::System.DateTime.TryParse({varName}, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "global::System.DateTimeOffset")
        {
            builder.AppendLine($" && global::System.DateTimeOffset.TryParse({varName}, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "global::System.Guid")
        {
            builder.AppendLine($" && global::System.Guid.TryParse({varName}, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "global::System.Uri")
        {
            builder.AppendLine($" && global::System.Uri.TryCreate({varName}, global::System.UriKind.RelativeOrAbsolute, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else
        {
            // Unsupported type - skip silently (matching ConfigurationBinder behavior)
            builder.AppendLine($") {{ }} // Skipped: {typeName} (not a supported primitive)");
        }
    }

    private static void GenerateComplexTypeBinding(StringBuilder builder, OptionsPropertyInfo prop, int index, string targetPath)
    {
        var sectionVar = $"sec{index}";

        switch (prop.ComplexTypeKind)
        {
            case ComplexTypeKind.NestedObject:
                GenerateNestedObjectBinding(builder, prop, index, targetPath, sectionVar);
                break;

            case ComplexTypeKind.Array:
                GenerateArrayBinding(builder, prop, index, targetPath, sectionVar);
                break;

            case ComplexTypeKind.List:
                GenerateListBinding(builder, prop, index, targetPath, sectionVar);
                break;

            case ComplexTypeKind.Dictionary:
                GenerateDictionaryBinding(builder, prop, index, targetPath, sectionVar);
                break;
        }
    }

    private static void GenerateNestedObjectBinding(StringBuilder builder, OptionsPropertyInfo prop, int index, string targetPath, string sectionVar)
    {
        var nestedPath = $"{targetPath}.{prop.Name}";

        builder.AppendLine($"                // Bind nested object: {prop.Name}");
        builder.AppendLine($"                var {sectionVar} = section.GetSection(\"{prop.Name}\");");

        // Initialize if null (for nullable properties)
        if (prop.IsNullable)
        {
            builder.AppendLine($"                {nestedPath} ??= new {GetNonNullableTypeName(prop.TypeName)}();");
        }

        // Generate bindings for nested properties
        if (prop.NestedProperties != null)
        {
            var nestedIndex = index * 100; // Use offset to avoid variable name collisions
            foreach (var nestedProp in prop.NestedProperties)
            {
                // Temporarily swap section context for nested binding
                GenerateNestedPropertyBinding(builder, nestedProp, nestedIndex, nestedPath, sectionVar);
                nestedIndex++;
            }
        }
    }

    private static void GenerateNestedPropertyBinding(StringBuilder builder, OptionsPropertyInfo prop, int index, string targetPath, string sectionVarName)
    {
        // Handle complex types recursively
        if (prop.ComplexTypeKind != ComplexTypeKind.None)
        {
            var innerSectionVar = $"sec{index}";
            switch (prop.ComplexTypeKind)
            {
                case ComplexTypeKind.NestedObject:
                    builder.AppendLine($"                // Bind nested object: {prop.Name}");
                    builder.AppendLine($"                var {innerSectionVar} = {sectionVarName}.GetSection(\"{prop.Name}\");");
                    if (prop.IsNullable)
                    {
                        builder.AppendLine($"                {targetPath}.{prop.Name} ??= new {GetNonNullableTypeName(prop.TypeName)}();");
                    }
                    if (prop.NestedProperties != null)
                    {
                        var nestedIndex = index * 100;
                        foreach (var nestedProp in prop.NestedProperties)
                        {
                            GenerateNestedPropertyBinding(builder, nestedProp, nestedIndex, $"{targetPath}.{prop.Name}", innerSectionVar);
                            nestedIndex++;
                        }
                    }
                    break;

                case ComplexTypeKind.Array:
                case ComplexTypeKind.List:
                case ComplexTypeKind.Dictionary:
                    // For collections inside nested objects, generate appropriate binding
                    GenerateCollectionBindingInNested(builder, prop, index, targetPath, sectionVarName);
                    break;
            }
            return;
        }

        // Generate primitive binding using the nested section
        var varName = $"v{index}";
        var baseTypeName = GetBaseTypeName(prop.TypeName);

        builder.Append($"                if ({sectionVarName}[\"{prop.Name}\"] is {{ }} {varName}");

        if (prop.IsEnum && prop.EnumTypeName != null)
        {
            builder.AppendLine($" && global::System.Enum.TryParse<{prop.EnumTypeName}>({varName}, true, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "string" || baseTypeName == "global::System.String")
        {
            builder.AppendLine($") {targetPath}.{prop.Name} = {varName};");
        }
        else if (baseTypeName == "int" || baseTypeName == "global::System.Int32")
        {
            builder.AppendLine($" && int.TryParse({varName}, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else if (baseTypeName == "bool" || baseTypeName == "global::System.Boolean")
        {
            builder.AppendLine($" && bool.TryParse({varName}, out var p{index})) {targetPath}.{prop.Name} = p{index};");
        }
        else
        {
            // For other types, generate appropriate TryParse
            builder.AppendLine($") {{ }} // Skipped: {prop.TypeName}");
        }
    }

    private static void GenerateCollectionBindingInNested(StringBuilder builder, OptionsPropertyInfo prop, int index, string targetPath, string sectionVarName)
    {
        var collectionSection = $"colSec{index}";
        builder.AppendLine($"                var {collectionSection} = {sectionVarName}.GetSection(\"{prop.Name}\");");

        switch (prop.ComplexTypeKind)
        {
            case ComplexTypeKind.List:
                GenerateListBindingCore(builder, prop, index, $"{targetPath}.{prop.Name}", collectionSection);
                break;
            case ComplexTypeKind.Array:
                GenerateArrayBindingCore(builder, prop, index, $"{targetPath}.{prop.Name}", collectionSection);
                break;
            case ComplexTypeKind.Dictionary:
                GenerateDictionaryBindingCore(builder, prop, index, $"{targetPath}.{prop.Name}", collectionSection);
                break;
        }
    }

    private static void GenerateArrayBinding(StringBuilder builder, OptionsPropertyInfo prop, int index, string targetPath, string sectionVar)
    {
        builder.AppendLine($"                // Bind array: {prop.Name}");
        builder.AppendLine($"                var {sectionVar} = section.GetSection(\"{prop.Name}\");");
        GenerateArrayBindingCore(builder, prop, index, $"{targetPath}.{prop.Name}", sectionVar);
    }

    private static void GenerateArrayBindingCore(StringBuilder builder, OptionsPropertyInfo prop, int index, string targetPath, string sectionVar)
    {
        var itemsVar = $"items{index}";
        var elementType = prop.ElementTypeName ?? "string";
        var hasNestedProps = prop.NestedProperties != null && prop.NestedProperties.Count > 0;

        builder.AppendLine($"                var {itemsVar} = new global::System.Collections.Generic.List<{elementType}>();");
        builder.AppendLine($"                foreach (var child in {sectionVar}.GetChildren())");
        builder.AppendLine("                {");

        if (hasNestedProps)
        {
            // Complex element type
            var itemVar = $"item{index}";
            builder.AppendLine($"                    var {itemVar} = new {elementType}();");
            var nestedIndex = index * 100;
            foreach (var nestedProp in prop.NestedProperties!)
            {
                GenerateChildPropertyBinding(builder, nestedProp, nestedIndex, itemVar, "child");
                nestedIndex++;
            }
            builder.AppendLine($"                    {itemsVar}.Add({itemVar});");
        }
        else
        {
            // Primitive element type
            GeneratePrimitiveCollectionAdd(builder, elementType, index, itemsVar);
        }

        builder.AppendLine("                }");
        builder.AppendLine($"                {targetPath} = {itemsVar}.ToArray();");
    }

    private static void GenerateListBinding(StringBuilder builder, OptionsPropertyInfo prop, int index, string targetPath, string sectionVar)
    {
        builder.AppendLine($"                // Bind list: {prop.Name}");
        builder.AppendLine($"                var {sectionVar} = section.GetSection(\"{prop.Name}\");");
        GenerateListBindingCore(builder, prop, index, $"{targetPath}.{prop.Name}", sectionVar);
    }

    private static void GenerateListBindingCore(StringBuilder builder, OptionsPropertyInfo prop, int index, string targetPath, string sectionVar)
    {
        var elementType = prop.ElementTypeName ?? "string";
        var hasNestedProps = prop.NestedProperties != null && prop.NestedProperties.Count > 0;

        builder.AppendLine($"                {targetPath}.Clear();");
        builder.AppendLine($"                foreach (var child in {sectionVar}.GetChildren())");
        builder.AppendLine("                {");

        if (hasNestedProps)
        {
            // Complex element type
            var itemVar = $"item{index}";
            builder.AppendLine($"                    var {itemVar} = new {elementType}();");
            var nestedIndex = index * 100;
            foreach (var nestedProp in prop.NestedProperties!)
            {
                GenerateChildPropertyBinding(builder, nestedProp, nestedIndex, itemVar, "child");
                nestedIndex++;
            }
            builder.AppendLine($"                    {targetPath}.Add({itemVar});");
        }
        else
        {
            // Primitive element type
            GeneratePrimitiveListAdd(builder, elementType, index, targetPath);
        }

        builder.AppendLine("                }");
    }

    private static void GenerateDictionaryBinding(StringBuilder builder, OptionsPropertyInfo prop, int index, string targetPath, string sectionVar)
    {
        builder.AppendLine($"                // Bind dictionary: {prop.Name}");
        builder.AppendLine($"                var {sectionVar} = section.GetSection(\"{prop.Name}\");");
        GenerateDictionaryBindingCore(builder, prop, index, $"{targetPath}.{prop.Name}", sectionVar);
    }

    private static void GenerateDictionaryBindingCore(StringBuilder builder, OptionsPropertyInfo prop, int index, string targetPath, string sectionVar)
    {
        var elementType = prop.ElementTypeName ?? "string";
        var hasNestedProps = prop.NestedProperties != null && prop.NestedProperties.Count > 0;

        builder.AppendLine($"                {targetPath}.Clear();");
        builder.AppendLine($"                foreach (var child in {sectionVar}.GetChildren())");
        builder.AppendLine("                {");

        if (hasNestedProps)
        {
            // Complex value type
            var itemVar = $"item{index}";
            builder.AppendLine($"                    var {itemVar} = new {elementType}();");
            var nestedIndex = index * 100;
            foreach (var nestedProp in prop.NestedProperties!)
            {
                GenerateChildPropertyBinding(builder, nestedProp, nestedIndex, itemVar, "child");
                nestedIndex++;
            }
            builder.AppendLine($"                    {targetPath}[child.Key] = {itemVar};");
        }
        else
        {
            // Primitive value type
            GeneratePrimitiveDictionaryAdd(builder, elementType, index, targetPath);
        }

        builder.AppendLine("                }");
    }

    private static void GenerateChildPropertyBinding(StringBuilder builder, OptionsPropertyInfo prop, int index, string targetVar, string sectionVar)
    {
        var varName = $"cv{index}";
        var baseTypeName = GetBaseTypeName(prop.TypeName);

        builder.Append($"                    if ({sectionVar}[\"{prop.Name}\"] is {{ }} {varName}");

        if (prop.IsEnum && prop.EnumTypeName != null)
        {
            builder.AppendLine($" && global::System.Enum.TryParse<{prop.EnumTypeName}>({varName}, true, out var cp{index})) {targetVar}.{prop.Name} = cp{index};");
        }
        else if (baseTypeName == "string" || baseTypeName == "global::System.String")
        {
            builder.AppendLine($") {targetVar}.{prop.Name} = {varName};");
        }
        else if (baseTypeName == "int" || baseTypeName == "global::System.Int32")
        {
            builder.AppendLine($" && int.TryParse({varName}, out var cp{index})) {targetVar}.{prop.Name} = cp{index};");
        }
        else if (baseTypeName == "bool" || baseTypeName == "global::System.Boolean")
        {
            builder.AppendLine($" && bool.TryParse({varName}, out var cp{index})) {targetVar}.{prop.Name} = cp{index};");
        }
        else
        {
            builder.AppendLine($") {{ }} // Skipped: {prop.TypeName}");
        }
    }

    private static void GeneratePrimitiveCollectionAdd(StringBuilder builder, string elementType, int index, string listVar)
    {
        var baseType = GetBaseTypeName(elementType);

        if (baseType == "string" || baseType == "global::System.String")
        {
            builder.AppendLine($"                    if (child.Value is {{ }} val{index}) {listVar}.Add(val{index});");
        }
        else if (baseType == "int" || baseType == "global::System.Int32")
        {
            builder.AppendLine($"                    if (child.Value is {{ }} val{index} && int.TryParse(val{index}, out var p{index})) {listVar}.Add(p{index});");
        }
        else
        {
            builder.AppendLine($"                    // Skipped: unsupported element type {elementType}");
        }
    }

    private static void GeneratePrimitiveListAdd(StringBuilder builder, string elementType, int index, string targetPath)
    {
        var baseType = GetBaseTypeName(elementType);

        if (baseType == "string" || baseType == "global::System.String")
        {
            builder.AppendLine($"                    if (child.Value is {{ }} val{index}) {targetPath}.Add(val{index});");
        }
        else if (baseType == "int" || baseType == "global::System.Int32")
        {
            builder.AppendLine($"                    if (child.Value is {{ }} val{index} && int.TryParse(val{index}, out var p{index})) {targetPath}.Add(p{index});");
        }
        else
        {
            builder.AppendLine($"                    // Skipped: unsupported element type {elementType}");
        }
    }

    private static void GeneratePrimitiveDictionaryAdd(StringBuilder builder, string elementType, int index, string targetPath)
    {
        var baseType = GetBaseTypeName(elementType);

        if (baseType == "string" || baseType == "global::System.String")
        {
            builder.AppendLine($"                    if (child.Value is {{ }} val{index}) {targetPath}[child.Key] = val{index};");
        }
        else if (baseType == "int" || baseType == "global::System.Int32")
        {
            builder.AppendLine($"                    if (child.Value is {{ }} val{index} && int.TryParse(val{index}, out var p{index})) {targetPath}[child.Key] = p{index};");
        }
        else
        {
            builder.AppendLine($"                    // Skipped: unsupported element type {elementType}");
        }
    }

    private static string GetNonNullableTypeName(string typeName)
    {
        if (typeName.EndsWith("?"))
            return typeName.Substring(0, typeName.Length - 1);
        return typeName;
    }

    private static string GetBaseTypeName(string typeName)
    {
        // Handle nullable types like "global::System.Nullable<int>" or "int?"
        if (typeName.StartsWith("global::System.Nullable<") && typeName.EndsWith(">"))
        {
            return typeName.Substring("global::System.Nullable<".Length, typeName.Length - "global::System.Nullable<".Length - 1);
        }
        if (typeName.EndsWith("?"))
        {
            return typeName.Substring(0, typeName.Length - 1);
        }
        return typeName;
    }
}

