using System.Linq;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Behavioral tests for positional-argument forwarding on parameterized custom guard
/// aliases: an application alias attribute usage such as <c>[MinCount(3)]</c>, whose
/// attribute type is meta-annotated with
/// <c>[ConstructorGuardDefinition(typeof(MinCountGuard))]</c>, forwards every one of
/// its own positional constructor arguments -- in declared order -- onto the resolved
/// guard's static validation method call, between the guarded value and the trailing
/// <c>nameof</c> parameter name.
/// </summary>
public sealed class ConstructorGuardAliasArgumentForwardingTests
{
    [Fact]
    public void SingleForwardedArgument_EmitsExactGuardCall()
    {
        var source = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class MinCountGuard
                {
                    public static void Validate(int value, int min, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(MinCountGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class MinCountAttribute : Attribute
                {
                    public MinCountAttribute(int min) { }
                }

                public partial class Basket
                {
                    [MinCount(3)]
                    private readonly int _value;
                }
            }
            """;

        var (generatedCode, errors) = RunGeneratorAndCompile(source);

        Assert.Contains("global::TestApp.MinCountGuard.Validate(value, 3, nameof(value));", generatedCode);
        Assert.Empty(errors);
    }

    [Fact]
    public void MultiplePositionalArguments_AreForwardedInDeclaredOrder()
    {
        var source = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class RangeGuard
                {
                    public static void Validate(int value, int min, int max, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(RangeGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class WithinRangeAttribute : Attribute
                {
                    public WithinRangeAttribute(int min, int max) { }
                }

                public partial class RetryPolicy
                {
                    [WithinRange(3, 10)]
                    private readonly int _value;
                }
            }
            """;

        var (generatedCode, errors) = RunGeneratorAndCompile(source);

        Assert.Contains("global::TestApp.RangeGuard.Validate(value, 3, 10, nameof(value));", generatedCode);
        Assert.Empty(errors);
    }

    [Fact]
    public void StringAndCharArguments_AreEscapedCorrectly()
    {
        var source = """"
            using System;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class LabelGuard
                {
                    public static void Validate(string value, string prefix, char separator, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(LabelGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class LabelAttribute : Attribute
                {
                    public LabelAttribute(string prefix, char separator) { }
                }

                public partial class OrderService
                {
                    [Label("line1\nline2\"quoted\"", ':')]
                    private readonly string _value;
                }
            }
            """";

        var (generatedCode, errors) = RunGeneratorAndCompile(source);

        var expectedString = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral("line1\nline2\"quoted\"", quote: true);
        var expectedChar = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(':', quote: true);

        Assert.Contains($"global::TestApp.LabelGuard.Validate(value, {expectedString}, {expectedChar}, nameof(value));", generatedCode);
        Assert.Empty(errors);
    }

    [Fact]
    public void EnumArgument_RendersFullyQualifiedMemberReference()
    {
        var source = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public enum RiskLevel { Low, Medium, High }

                public static class RiskGuard
                {
                    public static void Validate(string value, RiskLevel level, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(RiskGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class RiskAttribute : Attribute
                {
                    public RiskAttribute(RiskLevel level) { }
                }

                public partial class OrderService
                {
                    [Risk(RiskLevel.High)]
                    private readonly string _value;
                }
            }
            """;

        var (generatedCode, errors) = RunGeneratorAndCompile(source);

        Assert.Contains("global::TestApp.RiskGuard.Validate(value, global::TestApp.RiskLevel.High, nameof(value));", generatedCode);
        Assert.Empty(errors);
    }

    [Fact]
    public void TypeArgument_IncludingOpenGeneric_RendersTypeofFullyQualified()
    {
        var source = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class TypeGuard
                {
                    public static void Validate(object value, Type expected, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(TypeGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class OfTypeAttribute : Attribute
                {
                    public OfTypeAttribute(Type expected) { }
                }

                public partial class Container
                {
                    [OfType(typeof(System.Collections.Generic.List<>))]
                    private readonly object _value;
                }
            }
            """;

        var (generatedCode, errors) = RunGeneratorAndCompile(source);

        Assert.Contains(
            "global::TestApp.TypeGuard.Validate(value, typeof(global::System.Collections.Generic.List<>), nameof(value));",
            generatedCode);
        Assert.Empty(errors);
    }

    [Fact]
    public void NullArgument_RendersNullLiteral()
    {
        var source = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class DefaultableGuard
                {
                    public static void Validate(string value, string? fallback, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(DefaultableGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class DefaultableAttribute : Attribute
                {
                    public DefaultableAttribute(string? fallback) { }
                }

                public partial class OrderService
                {
                    [Defaultable(null)]
                    private readonly string _value;
                }
            }
            """;

        var (generatedCode, errors) = RunGeneratorAndCompile(source);

        Assert.Contains("global::TestApp.DefaultableGuard.Validate(value, null, nameof(value));", generatedCode);
        Assert.Empty(errors);
    }

    [Fact]
    public void IntegralSuffixes_AreRenderedCorrectlyForEachType()
    {
        var source = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class BoundsGuard
                {
                    public static void Validate(long value, uint a, long b, ulong c, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(BoundsGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class BoundsAttribute : Attribute
                {
                    public BoundsAttribute(uint a, long b, ulong c) { }
                }

                public partial class Container
                {
                    [Bounds(7u, 123456789012L, 9999999999UL)]
                    private readonly long _value;
                }
            }
            """;

        var (generatedCode, errors) = RunGeneratorAndCompile(source);

        Assert.Contains("global::TestApp.BoundsGuard.Validate(value, 7u, 123456789012L, 9999999999UL, nameof(value));", generatedCode);
        Assert.Empty(errors);
    }

    [Fact]
    public void OmittedOptionalArgument_ForwardsEffectiveDefaultValue()
    {
        // Regression test locking confirmed Roslyn behavior: an omitted optional alias
        // constructor argument still appears in AttributeData.ConstructorArguments,
        // with the parameter's effective default value baked in as its TypedConstant --
        // it does not disappear from the list. The generator must forward that
        // effective value, not silently drop the argument position.
        var source = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class RetryGuard
                {
                    public static void Validate(int value, int maxAttempts, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(RetryGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class RetryAttribute : Attribute
                {
                    public RetryAttribute(int maxAttempts = 5) { }
                }

                public partial class Container
                {
                    [Retry]
                    private readonly int _value;
                }
            }
            """;

        var (generatedCode, errors) = RunGeneratorAndCompile(source);

        Assert.Contains("global::TestApp.RetryGuard.Validate(value, 5, nameof(value));", generatedCode);
        Assert.Empty(errors);
    }

    [Fact]
    public void ReferencedAssemblyAlias_ForwardsPositionalArgument()
    {
        var librarySource = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace FrameworkLib
            {
                public static class MinCountGuard
                {
                    public static void Validate(int value, int min, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(MinCountGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class MinCountAttribute : Attribute
                {
                    public MinCountAttribute(int min) { }
                }
            }
            """;

        var consumerSource = """
            using FrameworkLib;

            namespace TestApp
            {
                public partial class Basket
                {
                    [MinCount(3)]
                    private readonly int _value;
                }
            }
            """;

        var generatedFiles = GeneratorTestRunner.ForConstructorGeneration()
            .WithCrossAssemblySource("FrameworkLib", librarySource)
            .WithSource(consumerSource)
            .RunGenerator(new GeneratedConstructorGenerator());

        var content = generatedFiles.Length == 0 ? string.Empty : string.Join("\n\n", generatedFiles.Select(f => f.Content));

        Assert.Contains("global::FrameworkLib.MinCountGuard.Validate(value, 3, nameof(value));", content);
    }

    [Fact]
    public void ZeroArgumentAlias_RemainsByteForByteCompatible()
    {
        // Regression test: an alias attribute with no positional constructor
        // arguments at all must still emit the exact historical two-argument call
        // shape -- Guard.Validate(value, nameof(value)) -- with no trailing comma or
        // any other artifact of the forwarding machinery now behind this call.
        var source = """
            using System;
            using System.Collections.Generic;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class CollectionNotEmptyGuard
                {
                    public static void Validate<T>(IReadOnlyCollection<T>? value, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class CollectionNotEmptyAttribute : Attribute { }

                public class Order { }

                public partial class OrderService
                {
                    [CollectionNotEmpty]
                    private readonly IReadOnlyCollection<Order> _orders;
                }
            }
            """;

        var (generatedCode, errors) = RunGeneratorAndCompile(source);

        Assert.Contains("global::TestApp.CollectionNotEmptyGuard.Validate(orders, nameof(orders));", generatedCode);
        Assert.Empty(errors);
    }

    [Fact]
    public void DirectConstructorGuardTypeofUsage_StillForwardsZeroArguments()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class NumberGuards
                {
                    public static void ValidatePositive(int value, string parameterName) { }
                }

                [GenerateConstructor]
                public partial class RetryPolicy
                {
                    [ConstructorGuard(typeof(NumberGuards), nameof(NumberGuards.ValidatePositive))]
                    private readonly int _retryCount;
                }
            }
            """;

        var (generatedCode, errors) = RunGeneratorAndCompile(source);

        Assert.Contains("global::TestApp.NumberGuards.ValidatePositive(retryCount, nameof(retryCount));", generatedCode);
        Assert.Empty(errors);
    }

    [Fact]
    public void UnsupportedFloatingPointArgument_OmitsGuardCallButStillGeneratesConstructor()
    {
        // A double positional argument on the alias is a legal C# attribute usage --
        // TypedConstantRenderer just doesn't support rendering it safely in this
        // slice. Rather than emit a positionally-broken or ambiguous call, the whole
        // guard call is skipped; a dedicated analyzer (out of scope here) is
        // responsible for diagnosing the unsupported argument. The type is still
        // otherwise eligible, so its constructor is generated normally.
        var source = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class ThresholdGuard
                {
                    public static void Validate(double value, double threshold, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(ThresholdGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class ThresholdAttribute : Attribute
                {
                    public ThresholdAttribute(double threshold) { }
                }

                [GenerateConstructor]
                public partial class Container
                {
                    [Threshold(1.5)]
                    private readonly double _value;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public Container(double value)", generatedCode);
        Assert.DoesNotContain("ThresholdGuard", generatedCode);
    }

    [Fact]
    public void UnsupportedArrayArgument_OmitsGuardCallButStillGeneratesConstructor()
    {
        var source = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class TagsGuard
                {
                    public static void Validate(string value, string[] allowed, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(TagsGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class TagsAttribute : Attribute
                {
                    public TagsAttribute(string[] allowed) { }
                }

                [GenerateConstructor]
                public partial class Container
                {
                    [Tags(new[] { "a", "b" })]
                    private readonly string _value;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public Container(string value)", generatedCode);
        Assert.DoesNotContain("TagsGuard", generatedCode);
    }

    [Fact]
    public void NamedArgument_OmitsGuardCallButStillGeneratesConstructor()
    {
        var source = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class LabelGuard
                {
                    public static void Validate(string value, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(LabelGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class LabelAttribute : Attribute
                {
                    public string Prefix { get; set; } = string.Empty;
                }

                [GenerateConstructor]
                public partial class Container
                {
                    [Label(Prefix = "ORD")]
                    private readonly string _value;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public Container(string value)", generatedCode);
        Assert.DoesNotContain("LabelGuard", generatedCode);
    }

    private static string RunGenerator(string source)
    {
        var files = GeneratorTestRunner.ForConstructorGeneration()
            .WithDocumentationMode()
            .WithSource(source)
            .RunGenerator(new GeneratedConstructorGenerator());

        return files.Length == 0
            ? string.Empty
            : string.Join("\n\n", files.Select(f => f.Content));
    }

    private static (string GeneratedCode, System.Collections.Generic.IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic> Errors) RunGeneratorAndCompile(string source)
    {
        var generatedCode = RunGenerator(source);

        var errors = GeneratorTestRunner.ForConstructorGeneration()
            .WithSource(source)
            .RunGeneratorCompilationErrors(new GeneratedConstructorGenerator());

        return (generatedCode, errors);
    }
}
