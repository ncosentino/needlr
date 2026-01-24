using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

/// <summary>
/// Tests that verify ConfiguredSyringe cannot be constructed directly from external code.
/// This ensures the compile-time safety guarantee that users must use strategy methods
/// (UsingReflection, UsingSourceGen, UsingAutoConfiguration) to get a ConfiguredSyringe.
/// </summary>
public sealed class ConfiguredSyringeAccessibilityTests
{
    [Fact]
    public async Task DirectConstruction_ProducesCompileError()
    {
        // This code tries to directly construct a ConfiguredSyringe without using a strategy method.
        // It should fail to compile because the constructor is internal.
        var testCode = @"
using NexusLabs.Needlr.Injection;

public class TestClass
{
    public void TestMethod()
    {
        // This should NOT compile - ConfiguredSyringe constructor is internal
        var syringe = new ConfiguredSyringe();
    }
}";

        var diagnostics = await CompileAndGetDiagnosticsAsync(testCode);

        // Should have at least one error about inaccessible/missing constructor
        // CS1729 = 'X' does not contain a constructor that takes N arguments
        // CS0122 = 'X' is inaccessible due to its protection level
        var accessibilityErrors = diagnostics
            .Where(d => d.Id == "CS0122" || d.Id == "CS1729")
            .ToList();

        Assert.NotEmpty(accessibilityErrors);
        // Verify the error mentions ConfiguredSyringe
        Assert.True(
            accessibilityErrors.Any(d => d.GetMessage().Contains("ConfiguredSyringe")),
            $"Expected error about ConfiguredSyringe, got: {string.Join(", ", accessibilityErrors.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public async Task UsingReflection_Compiles()
    {
        // This code uses the proper strategy method and should compile successfully.
        var testCode = @"
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

public class TestClass
{
    public void TestMethod()
    {
        // This should compile - using proper strategy method
        var syringe = new Syringe().UsingReflection();
    }
}";

        var diagnostics = await CompileAndGetDiagnosticsAsync(testCode, includeReflection: true);

        // Should have no errors
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void BuildServiceProvider_NotAvailableOnBaseSyringe()
    {
        // Use reflection to verify Syringe doesn't have BuildServiceProvider
        // This is more reliable than compilation tests for this case
        var syringeType = typeof(Injection.Syringe);
        var buildMethod = syringeType.GetMethod(
            "BuildServiceProvider", 
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        Assert.Null(buildMethod);
    }

    [Fact]
    public void BuildServiceProvider_AvailableOnConfiguredSyringe()
    {
        // Verify ConfiguredSyringe DOES have BuildServiceProvider
        var configuredSyringeType = typeof(Injection.ConfiguredSyringe);
        var buildMethod = configuredSyringeType.GetMethod(
            "BuildServiceProvider", 
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(buildMethod);
    }

    [Fact]
    public void ConfiguredSyringe_HasNoPublicConstructor()
    {
        // Use reflection to verify ConfiguredSyringe has no public constructors
        var configuredSyringeType = typeof(Injection.ConfiguredSyringe);
        var publicConstructors = configuredSyringeType.GetConstructors(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        Assert.Empty(publicConstructors);
    }

    private static async Task<ImmutableArray<Diagnostic>> CompileAndGetDiagnosticsAsync(
        string code, 
        bool includeReflection = false,
        bool includeConfiguration = false)
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Injection.Syringe).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Injection.ConfiguredSyringe).Assembly.Location),
        };

        // Add runtime references needed for compilation
        var runtimeDir = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.Add(MetadataReference.CreateFromFile(System.IO.Path.Combine(runtimeDir, "System.Runtime.dll")));

        if (includeReflection)
        {
            references.Add(MetadataReference.CreateFromFile(typeof(Injection.Reflection.SyringeReflectionExtensions).Assembly.Location));
        }

        if (includeConfiguration)
        {
            references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Configuration.ConfigurationBuilder).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Configuration.IConfiguration).Assembly.Location));
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return await Task.FromResult(compilation.GetDiagnostics());
    }
}
