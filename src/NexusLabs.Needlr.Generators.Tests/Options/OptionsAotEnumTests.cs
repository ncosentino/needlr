// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests for AOT-compatible enum binding in options classes.
/// </summary>
public sealed class OptionsAotEnumTests
{
    [Fact]
    public void Generator_EmitsEnumBinding_ForEnumProperty()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public enum LogLevel
                {
                    Debug,
                    Info,
                    Warning,
                    Error
                }

                [Options]
                public class LoggingOptions
                {
                    public LogLevel Level { get; set; } = LogLevel.Info;
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Should not emit NDLRGEN020 for enum properties
        Assert.Empty(diagnostics.Where(d => d.Id == "NDLRGEN020"));

        // Should generate Enum.TryParse binding
        Assert.Contains("Enum.TryParse", generatedCode);
        Assert.Contains("LogLevel", generatedCode);
        Assert.Contains("section[\"Level\"]", generatedCode);
    }

    [Fact]
    public void Generator_EmitsEnumBinding_ForNullableEnumProperty()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public enum Priority
                {
                    Low,
                    Medium,
                    High
                }

                [Options]
                public class TaskOptions
                {
                    public Priority? DefaultPriority { get; set; }
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Should not emit NDLRGEN020 for nullable enum properties
        Assert.Empty(diagnostics.Where(d => d.Id == "NDLRGEN020"));

        // Should generate Enum.TryParse binding for nullable
        Assert.Contains("Enum.TryParse", generatedCode);
        Assert.Contains("Priority", generatedCode);
    }

    [Fact]
    public void Generator_EmitsEnumBinding_ForFlagsEnum()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Flags]
                public enum Permissions
                {
                    None = 0,
                    Read = 1,
                    Write = 2,
                    Execute = 4,
                    All = Read | Write | Execute
                }

                [Options]
                public class SecurityOptions
                {
                    public Permissions UserPermissions { get; set; } = Permissions.Read;
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Flags enums should also work with Enum.TryParse
        Assert.Empty(diagnostics.Where(d => d.Id == "NDLRGEN020"));
        Assert.Contains("Enum.TryParse", generatedCode);
    }

    [Fact]
    public void Generator_EmitsEnumBinding_ForMultipleEnumProperties()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public enum Environment { Development, Staging, Production }
                public enum LogLevel { Debug, Info, Warning, Error }

                [Options]
                public class AppOptions
                {
                    public Environment Env { get; set; } = Environment.Development;
                    public LogLevel MinLogLevel { get; set; } = LogLevel.Info;
                    public string AppName { get; set; } = "";
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        Assert.Empty(diagnostics.Where(d => d.Id == "NDLRGEN020"));
        
        // Should bind all properties
        Assert.Contains("section[\"Env\"]", generatedCode);
        Assert.Contains("section[\"MinLogLevel\"]", generatedCode);
        Assert.Contains("section[\"AppName\"]", generatedCode);
        
        // Should have enum parsing for both
        Assert.Contains("Environment", generatedCode);
        Assert.Contains("LogLevel", generatedCode);
    }

    [Fact]
    public void Generator_EnumBinding_MatchesNonAotBehavior()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public enum Status { Pending, Active, Completed }

                [Options]
                public class WorkflowOptions
                {
                    public Status InitialStatus { get; set; } = Status.Pending;
                }
            }
            """;

        // Both paths should handle enums without errors
        var (_, aotDiagnostics) = RunGeneratorWithAot(source);
        var (_, nonAotDiagnostics) = RunGeneratorWithoutAot(source);

        Assert.Empty(aotDiagnostics.Where(d => d.Id == "NDLRGEN020"));
        Assert.Empty(nonAotDiagnostics.Where(d => d.Id == "NDLRGEN020"));
    }

    private static (string GeneratedCode, ImmutableArray<Diagnostic> Diagnostics) RunGeneratorWithAot(string source)
    {
        var runner = GeneratorTestRunner.ForOptions()
            .WithSource(source)
            .WithAotMode()
            .WithBreadcrumbLevel("Minimal");

        var generatedCode = runner.GetTypeRegistryOutput();
        var diagnostics = runner.RunTypeRegistryGeneratorDiagnostics();
        return (generatedCode, diagnostics.ToImmutableArray());
    }

    private static (string GeneratedCode, ImmutableArray<Diagnostic> Diagnostics) RunGeneratorWithoutAot(string source)
    {
        var runner = GeneratorTestRunner.ForOptions()
            .WithSource(source)
            .WithBreadcrumbLevel("Minimal");

        var generatedCode = runner.GetTypeRegistryOutput();
        var diagnostics = runner.RunTypeRegistryGeneratorDiagnostics();
        return (generatedCode, diagnostics.ToImmutableArray());
    }
}
