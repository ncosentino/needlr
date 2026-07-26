using System.Linq;
using System.Text.RegularExpressions;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Shared harness for <c>[RegisterClosedOverImplementationsOf]</c> constraint scenarios. Each case
/// supplies only the types under test; the harness provides the assembly marker, the open generic
/// source interfaces and the facade, then runs the type registry generator once for code and once
/// for diagnostics.
/// </summary>
internal static class ClosedOverConstraintCaseRunner
{
    private const string ConstraintViolationDiagnosticId = "NDLRGEN038";

    /// <summary>
    /// Runs the generator over <paramref name="namespaceBody"/> placed inside the shared
    /// <c>TestNamespace</c> harness.
    /// </summary>
    public static ClosedOverConstraintCase Run(string namespaceBody)
    {
        var source = $$"""
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface ICaseDefinition<TData> { }

                public interface IPairCaseDefinition<TKey, TValue> { }

                public interface ICase { }

            {{namespaceBody}}
            }
            """;

        var generatedCode = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        var diagnostics = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        return new ClosedOverConstraintCase(generatedCode, diagnostics);
    }

    /// <summary>
    /// Asserts that the closed composition — for example <c>CaseCore&lt;global::TestNamespace.RefData&gt;</c>
    /// — was emitted as an activation expression.
    /// </summary>
    public static void AssertClosedOver(ClosedOverConstraintCase result, string closedComposition)
    {
        Assert.Contains($"new global::TestNamespace.{closedComposition}(", result.GeneratedCode);
    }

    /// <summary>
    /// Asserts that no activation expression was emitted for the closed composition.
    /// </summary>
    public static void AssertNotClosedOver(ClosedOverConstraintCase result, string closedComposition)
    {
        Assert.DoesNotContain($"new global::TestNamespace.{closedComposition}(", result.GeneratedCode);
    }

    /// <summary>
    /// Asserts the exact number of activation expressions emitted for a composition, so a scenario
    /// with several discovered implementations cannot silently emit more registrations than expected.
    /// </summary>
    public static void AssertClosedRegistrationCount(
        ClosedOverConstraintCase result,
        string compositionName,
        int expectedCount)
    {
        var actualCount = Regex
            .Matches(result.GeneratedCode, $"new global::TestNamespace.{compositionName}<")
            .Count;

        Assert.Equal(expectedCount, actualCount);
    }

    /// <summary>
    /// Asserts that the generator reported exactly one NDLRGEN038 per supplied fully qualified type
    /// argument list, and no others.
    /// </summary>
    public static void AssertConstraintViolations(
        ClosedOverConstraintCase result,
        params string[] expectedTypeArgumentLists)
    {
        var messages = result.Diagnostics
            .Where(d => d.Id == ConstraintViolationDiagnosticId)
            .Select(d => d.GetMessage())
            .ToList();

        Assert.Equal(expectedTypeArgumentLists.Length, messages.Count);

        foreach (var expectedTypeArgumentList in expectedTypeArgumentLists)
        {
            Assert.Contains(
                messages,
                m => m.Contains($"type argument(s) '{expectedTypeArgumentList}'"));
        }
    }

    /// <summary>
    /// Asserts the generator reported no constraint violation, proving a legal closure is not skipped.
    /// </summary>
    public static void AssertNoConstraintViolations(ClosedOverConstraintCase result)
    {
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == ConstraintViolationDiagnosticId);
    }
}
