using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests for GeneratorHelpers utility methods.
/// </summary>
public sealed class GeneratorHelpersTests
{
    [Theory]
    [InlineData("MyClass", "MyClass")]
    [InlineData("global::MyClass", "MyClass")]
    [InlineData("MyNamespace.MyClass", "MyClass")]
    [InlineData("global::MyNamespace.MyClass", "MyClass")]
    [InlineData("Foo.Bar.Baz.MyClass", "MyClass")]
    [InlineData("global::Foo.Bar.Baz.MyClass", "MyClass")]
    public void GetShortTypeName_NonGeneric_ReturnsClassName(string input, string expected)
    {
        var result = GeneratorHelpers.GetShortTypeName(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("IOptions<SmtpOptions>", "IOptions<SmtpOptions>")]
    [InlineData("global::IOptions<SmtpOptions>", "IOptions<SmtpOptions>")]
    [InlineData("Microsoft.Extensions.Options.IOptions<SmtpOptions>", "IOptions<SmtpOptions>")]
    [InlineData("global::Microsoft.Extensions.Options.IOptions<SmtpOptions>", "IOptions<SmtpOptions>")]
    public void GetShortTypeName_GenericWithSimpleArg_ShortensOuterType(string input, string expected)
    {
        var result = GeneratorHelpers.GetShortTypeName(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("IOptions<global::OptionsValidationExample.Options.SmtpOptions>", "IOptions<SmtpOptions>")]
    [InlineData("global::Microsoft.Extensions.Options.IOptions<global::Foo.Bar.SmtpOptions>", "IOptions<SmtpOptions>")]
    [InlineData("Microsoft.Extensions.Logging.ILogger<global::MyApp.Services.EmailService>", "ILogger<EmailService>")]
    public void GetShortTypeName_GenericWithQualifiedArg_ShortensTypeArg(string input, string expected)
    {
        var result = GeneratorHelpers.GetShortTypeName(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Dictionary<string, int>", "Dictionary<string, int>")]
    [InlineData("global::System.Collections.Generic.Dictionary<string, int>", "Dictionary<string, int>")]
    [InlineData("Dictionary<global::Foo.KeyType, global::Bar.ValueType>", "Dictionary<KeyType, ValueType>")]
    public void GetShortTypeName_GenericWithMultipleArgs_ShortensAllArgs(string input, string expected)
    {
        var result = GeneratorHelpers.GetShortTypeName(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("IOptions<ILogger<EmailService>>", "IOptions<ILogger<EmailService>>")]
    [InlineData("global::Microsoft.Extensions.Options.IOptions<global::Microsoft.Extensions.Logging.ILogger<global::MyApp.EmailService>>", "IOptions<ILogger<EmailService>>")]
    public void GetShortTypeName_NestedGenerics_ShortensAllLevels(string input, string expected)
    {
        var result = GeneratorHelpers.GetShortTypeName(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Func<string, IOptions<Config>, int>", "Func<string, IOptions<Config>, int>")]
    [InlineData("global::System.Func<global::System.String, global::Microsoft.Extensions.Options.IOptions<global::MyApp.Config>, global::System.Int32>", "Func<String, IOptions<Config>, Int32>")]
    public void GetShortTypeName_ComplexGenerics_HandlesCorrectly(string input, string expected)
    {
        var result = GeneratorHelpers.GetShortTypeName(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetShortTypeName_EmptyString_ReturnsEmpty()
    {
        var result = GeneratorHelpers.GetShortTypeName("");
        Assert.Equal("", result);
    }

    [Theory]
    [InlineData("MyClass", "MyClass")]
    [InlineData("IOptions<SmtpOptions>", "IOptions_SmtpOptions_")]
    [InlineData("Dictionary<string, int>", "Dictionary_string_ int_")]
    public void GetMermaidNodeId_SanitizesSpecialChars(string input, string expected)
    {
        var result = GeneratorHelpers.GetMermaidNodeId(input);
        Assert.Equal(expected, result);
    }
}
