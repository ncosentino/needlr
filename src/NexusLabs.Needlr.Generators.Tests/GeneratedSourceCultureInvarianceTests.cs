using System.Globalization;

using NexusLabs.Needlr;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Guards generated source against the ambient culture of the build machine.
/// </summary>
/// <remarks>
/// <para>
/// String interpolation formats numbers using <see cref="CultureInfo.CurrentCulture"/>.
/// Several locales render a negative number with U+2212 MINUS SIGN rather than U+002D
/// HYPHEN-MINUS, producing generated C# that does not compile. Negative <c>Order</c>
/// values are a supported feature, so this is reachable in ordinary use.
/// </para>
/// <para>
/// The whole-output comparison is the real guard. It covers every emitter at once,
/// including emitters added later, which matters because per-call-site correctness
/// cannot be enforced by an analyzer here: <c>CA1305</c> does not flag interpolated
/// strings in this project, and <c>RS1035</c> forbids a generator from touching the
/// ambient culture to fix the problem centrally.
/// </para>
/// <para>
/// These tests deliberately run generation under a hostile culture. Asserting only
/// under a typical developer culture would pass whether or not the defect is present.
/// </para>
/// </remarks>
public sealed class GeneratedSourceCultureInvarianceTests
{
    private const char UnicodeMinusSign = '\u2212';

    private const string Source = """
        using NexusLabs.Needlr;
        using NexusLabs.Needlr.Generators;

        [assembly: GenerateTypeRegistry]

        namespace TestApp
        {
            public interface IService { }

            public sealed class Service : IService { }

            [DecoratorFor<IService>(Order = -100)]
            public sealed class EarlyDecorator : IService
            {
                public EarlyDecorator(IService inner) { }
            }

            [DecoratorFor<IService>(Order = -5)]
            public sealed class LateDecorator : IService
            {
                public LateDecorator(IService inner) { }
            }
        }
        """;

    [Theory]
    [InlineData("sv-SE")]
    [InlineData("fi-FI")]
    [InlineData("lt-LT")]
    public void GeneratedFiles_AreIdenticalUnderAnyCulture(string hostileCulture)
    {
        var reference = RunUnderCulture("en-US", GenerateAll);
        var hostile = RunUnderCulture(hostileCulture, GenerateAll);

        Assert.Equal(reference.Count, hostile.Count);

        foreach (var file in reference)
        {
            Assert.True(
                hostile.ContainsKey(file.Key),
                $"'{file.Key}' was generated under en-US but not under {hostileCulture}.");

            Assert.True(
                string.Equals(file.Value, hostile[file.Key], StringComparison.Ordinal),
                $"'{file.Key}' differs between en-US and {hostileCulture}. Generated source "
                    + "must not depend on the build machine's culture.");
        }
    }

    [Theory]
    [InlineData("sv-SE")]
    [InlineData("fi-FI")]
    [InlineData("lt-LT")]
    public void NegativeNumbers_NeverUseTheUnicodeMinusSign(string hostileCulture)
    {
        var generated = RunUnderCulture(hostileCulture, GenerateAll);

        foreach (var file in generated)
        {
            Assert.False(
                file.Value.IndexOf(UnicodeMinusSign) >= 0,
                $"'{file.Key}' contains U+2212 MINUS SIGN. Emitted numeric literals must use "
                    + "the invariant culture; U+2212 is not valid C#.");
        }

        var catalog = generated.Single(
            f => f.Key.EndsWith("ServiceCatalog.g.cs", StringComparison.Ordinal));
        Assert.Contains(", -100,", catalog.Value, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> GenerateAll()
    {
        var files = GeneratorTestRunner.ForTypeRegistry()
            .WithReference<DecoratorForAttribute<object>>()
            .WithSource(Source)
            .WithDiagnosticsEnabled()
            .WithGraphExportEnabled()
            .RunTypeRegistryGeneratorFiles();

        return files.ToDictionary(f => f.FilePath, f => f.Content, StringComparer.Ordinal);
    }

    private static T RunUnderCulture<T>(string culture, Func<T> action)
    {
        var thread = System.Threading.Thread.CurrentThread;
        var originalCulture = thread.CurrentCulture;
        var originalUiCulture = thread.CurrentUICulture;
        try
        {
            var target = new CultureInfo(culture);
            thread.CurrentCulture = target;
            thread.CurrentUICulture = target;
            return action();
        }
        finally
        {
            thread.CurrentCulture = originalCulture;
            thread.CurrentUICulture = originalUiCulture;
        }
    }
}
