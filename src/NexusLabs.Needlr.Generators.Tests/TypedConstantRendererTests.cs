using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Unit tests for <see cref="TypedConstantRenderer"/>: the dedicated renderer that
/// turns a compiler-validated <see cref="TypedConstant"/> into the exact C# literal or
/// expression source text forwarded on a generated custom guard method call.
/// </summary>
public sealed class TypedConstantRendererTests
{
    [Fact]
    public void Null_RendersNullLiteral()
    {
        var constant = GetSingleConstructorArgument(
            """
            public sealed class ProbeAttribute : System.Attribute
            {
                public ProbeAttribute(string? value) { }
            }

            [Probe(null)]
            public class Target { }
            """);

        var success = TypedConstantRenderer.TryRender(constant, out var rendered);

        Assert.True(success);
        Assert.Equal("null", rendered);
    }

    [Fact]
    public void Bool_RendersLowercaseKeyword()
    {
        var constant = GetSingleConstructorArgument(
            """
            public sealed class ProbeAttribute : System.Attribute
            {
                public ProbeAttribute(bool value) { }
            }

            [Probe(true)]
            public class Target { }
            """);

        var success = TypedConstantRenderer.TryRender(constant, out var rendered);

        Assert.True(success);
        Assert.Equal("true", rendered);
    }

    [Theory]
    [InlineData("int", "-5", "-5")]
    [InlineData("uint", "5u", "5u")]
    [InlineData("long", "-123456789012L", "-123456789012L")]
    [InlineData("ulong", "9999999999UL", "9999999999UL")]
    [InlineData("sbyte", "-3", "-3")]
    [InlineData("byte", "200", "200")]
    [InlineData("short", "-30000", "-30000")]
    [InlineData("ushort", "40000", "40000")]
    public void IntegralPrimitives_RenderWithCorrectSuffix(string clrType, string literalSource, string expected)
    {
        var constant = GetSingleConstructorArgument(
            $$"""
            public sealed class ProbeAttribute : System.Attribute
            {
                public ProbeAttribute({{clrType}} value) { }
            }

            [Probe({{literalSource}})]
            public class Target { }
            """);

        var success = TypedConstantRenderer.TryRender(constant, out var rendered);

        Assert.True(success);
        Assert.Equal(expected, rendered);
    }

    [Fact]
    public void Char_EscapesQuoteCharacter()
    {
        var constant = GetSingleConstructorArgument(
            """
            public sealed class ProbeAttribute : System.Attribute
            {
                public ProbeAttribute(char value) { }
            }

            [Probe('\'')]
            public class Target { }
            """);

        var success = TypedConstantRenderer.TryRender(constant, out var rendered);

        Assert.True(success);
        Assert.Equal(SymbolDisplay.FormatLiteral('\'', quote: true), rendered);
    }

    [Fact]
    public void String_EscapesNewlinesQuotesAndBackslashes()
    {
        var constant = GetSingleConstructorArgument(
            """"
            public sealed class ProbeAttribute : System.Attribute
            {
                public ProbeAttribute(string value) { }
            }

            [Probe("line1\nline2\t\"quoted\"\\backslash")]
            public class Target { }
            """");

        var success = TypedConstantRenderer.TryRender(constant, out var rendered);

        Assert.True(success);
        Assert.Equal(SymbolDisplay.FormatLiteral("line1\nline2\t\"quoted\"\\backslash", quote: true), rendered);
    }

    [Fact]
    public void Enum_RendersFullyQualifiedMemberReference()
    {
        var constant = GetSingleConstructorArgument(
            """
            public enum Level { Low, Medium, High }

            public sealed class ProbeAttribute : System.Attribute
            {
                public ProbeAttribute(Level value) { }
            }

            [Probe(Level.High)]
            public class Target { }
            """);

        var success = TypedConstantRenderer.TryRender(constant, out var rendered);

        Assert.True(success);
        Assert.Equal("global::TestApp.Level.High", rendered);
    }

    [Fact]
    public void Enum_WithNoMatchingMember_RendersFullyQualifiedCast()
    {
        var constant = GetSingleConstructorArgument(
            """
            [System.Flags]
            public enum Level { None = 0, Low = 1, High = 2 }

            public sealed class ProbeAttribute : System.Attribute
            {
                public ProbeAttribute(Level value) { }
            }

            [Probe((Level)8)]
            public class Target { }
            """);

        var success = TypedConstantRenderer.TryRender(constant, out var rendered);

        Assert.True(success);
        Assert.Equal("(global::TestApp.Level)8", rendered);
    }

    [Fact]
    public void Type_RendersTypeofWithFullyQualifiedName()
    {
        var constant = GetSingleConstructorArgument(
            """
            public sealed class ProbeAttribute : System.Attribute
            {
                public ProbeAttribute(System.Type value) { }
            }

            [Probe(typeof(int))]
            public class Target { }
            """);

        var success = TypedConstantRenderer.TryRender(constant, out var rendered);

        Assert.True(success);
        Assert.Equal("typeof(int)", rendered);
    }

    [Fact]
    public void Type_WithOpenGeneric_RendersTypeofWithFullyQualifiedOpenGenericName()
    {
        var constant = GetSingleConstructorArgument(
            """
            public sealed class ProbeAttribute : System.Attribute
            {
                public ProbeAttribute(System.Type value) { }
            }

            [Probe(typeof(System.Collections.Generic.List<>))]
            public class Target { }
            """);

        var success = TypedConstantRenderer.TryRender(constant, out var rendered);

        Assert.True(success);
        Assert.Equal("typeof(global::System.Collections.Generic.List<>)", rendered);
    }

    [Fact]
    public void Float_IsUnsupported()
    {
        var constant = GetSingleConstructorArgument(
            """
            public sealed class ProbeAttribute : System.Attribute
            {
                public ProbeAttribute(float value) { }
            }

            [Probe(1.5f)]
            public class Target { }
            """);

        var success = TypedConstantRenderer.TryRender(constant, out var rendered);

        Assert.False(success);
        Assert.Equal(string.Empty, rendered);
    }

    [Fact]
    public void Double_IsUnsupported()
    {
        var constant = GetSingleConstructorArgument(
            """
            public sealed class ProbeAttribute : System.Attribute
            {
                public ProbeAttribute(double value) { }
            }

            [Probe(1.5)]
            public class Target { }
            """);

        var success = TypedConstantRenderer.TryRender(constant, out var rendered);

        Assert.False(success);
        Assert.Equal(string.Empty, rendered);
    }

    [Fact]
    public void Array_IsUnsupported()
    {
        var constant = GetSingleConstructorArgument(
            """
            public sealed class ProbeAttribute : System.Attribute
            {
                public ProbeAttribute(int[] value) { }
            }

            [Probe(new int[] { 1, 2, 3 })]
            public class Target { }
            """);

        var success = TypedConstantRenderer.TryRender(constant, out var rendered);

        Assert.False(success);
        Assert.Equal(string.Empty, rendered);
    }

    private static TypedConstant GetSingleConstructorArgument(string memberSource)
    {
        var source = $$"""
            namespace TestApp
            {
            {{memberSource}}
            }
            """;

        var parseOptions = new CSharpParseOptions();
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var references = Basic.Reference.Assemblies.Net100.References.All;

        var compilation = CSharpCompilation.Create(
            "TypedConstantRendererProbe",
            [tree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, "Probe source failed to compile: " + string.Join("; ", errors.Select(e => e.GetMessage())));

        var targetType = compilation.GetTypeByMetadataName("TestApp.Target");
        Assert.NotNull(targetType);

        var attribute = targetType!.GetAttributes().Single();
        return attribute.ConstructorArguments.Single();
    }
}
