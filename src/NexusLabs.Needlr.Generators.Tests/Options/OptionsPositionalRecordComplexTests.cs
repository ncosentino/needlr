using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NexusLabs.Needlr.Catalog;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Compilation tests for complex positional-record options binding.
/// </summary>
public sealed class OptionsPositionalRecordComplexTests
{
    private const string ComplexSource = """
        #nullable enable

        using System.Collections.Generic;
        using NexusLabs.Needlr.Generators;

        [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestApp" })]

        namespace TestApp
        {
            public enum Mode
            {
                First,
                Second
            }

            public sealed class ChildOptions
            {
                public string Name { get; set; } = "";
                public int Count { get; set; } = 7;
                public bool Enabled { get; set; } = true;
                public Mode Mode { get; set; } = Mode.First;
                public int? OptionalCount { get; set; }
                public double Ratio { get; set; } = 1.5;
            }

            [Options("Complex")]
            public partial record ComplexOptions(
                string Name,
                string? OptionalName,
                int Count,
                int? OptionalCount,
                bool Enabled,
                Mode Mode,
                double Ratio,
                double? OptionalRatio,
                ChildOptions Child,
                string[] Names,
                int[] Numbers,
                List<string> Tags,
                List<int> Ports,
                Dictionary<string, string> Labels,
                Dictionary<string, int> Limits,
                ChildOptions[] Children,
                List<ChildOptions> ChildList,
                Dictionary<string, ChildOptions> ChildMap);
        }
        """;

    [Fact]
    public void Generator_AotComplexPositionalRecord_CompilesGeneratedSource()
    {
        var errors = CreateComplexRunner()
            .RunGeneratorCompilationErrors(new TypeRegistryGenerator());

        var optionsErrors = errors
            .Where(error =>
                error.Location.SourceTree?.FilePath.EndsWith("TypeRegistry.g.cs") == true ||
                error.Location.SourceTree?.FilePath.Contains("OptionsConstructors") == true)
            .ToList();

        Assert.Empty(optionsErrors);
    }

    [Fact]
    public void Generator_AotComplexPositionalRecord_BindsExactValuesThroughIOptions()
    {
        var options = ResolveComplexOptions(new Dictionary<string, string?>
        {
            ["Complex:Name"] = "service",
            ["Complex:OptionalName"] = "optional",
            ["Complex:Count"] = "42",
            ["Complex:OptionalCount"] = "24",
            ["Complex:Enabled"] = "true",
            ["Complex:Mode"] = "Second",
            ["Complex:Ratio"] = "3.25",
            ["Complex:OptionalRatio"] = "2.5",
            ["Complex:Child:Name"] = "root",
            ["Complex:Child:Count"] = "9",
            ["Complex:Child:Enabled"] = "false",
            ["Complex:Child:Mode"] = "Second",
            ["Complex:Child:OptionalCount"] = "11",
            ["Complex:Child:Ratio"] = "4.5",
            ["Complex:Names:0"] = "alpha",
            ["Complex:Names:1"] = "beta",
            ["Complex:Numbers:0"] = "10",
            ["Complex:Numbers:1"] = "20",
            ["Complex:Tags:0"] = "blue",
            ["Complex:Tags:1"] = "green",
            ["Complex:Ports:0"] = "80",
            ["Complex:Ports:1"] = "443",
            ["Complex:Labels:primary"] = "one",
            ["Complex:Limits:requests"] = "100",
            ["Complex:Children:0:Name"] = "array",
            ["Complex:Children:0:Count"] = "1",
            ["Complex:ChildList:0:Name"] = "list",
            ["Complex:ChildList:0:Enabled"] = "false",
            ["Complex:ChildMap:east:Name"] = "dictionary",
            ["Complex:ChildMap:east:Ratio"] = "6.5",
        });

        Assert.Equal("service", GetProperty<string>(options, "Name"));
        Assert.Equal("optional", GetProperty<string>(options, "OptionalName"));
        Assert.Equal(42, GetProperty<int>(options, "Count"));
        Assert.Equal(24, GetProperty<int?>(options, "OptionalCount"));
        Assert.True(GetProperty<bool>(options, "Enabled"), "Expected valid boolean configuration to bind");
        Assert.Equal("Second", GetProperty(options, "Mode")?.ToString());
        Assert.Equal(3.25, GetProperty<double>(options, "Ratio"));
        Assert.Equal(2.5, GetProperty<double?>(options, "OptionalRatio"));

        var child = GetProperty(options, "Child")!;
        Assert.Equal("root", GetProperty<string>(child, "Name"));
        Assert.Equal(9, GetProperty<int>(child, "Count"));
        Assert.False(GetProperty<bool>(child, "Enabled"), "Expected nested boolean configuration to bind");
        Assert.Equal("Second", GetProperty(child, "Mode")?.ToString());
        Assert.Equal(11, GetProperty<int?>(child, "OptionalCount"));
        Assert.Equal(4.5, GetProperty<double>(child, "Ratio"));

        Assert.Equal(["alpha", "beta"], GetProperty<string[]>(options, "Names"));
        Assert.Equal([10, 20], GetProperty<int[]>(options, "Numbers"));
        Assert.Equal(["blue", "green"], GetProperty<List<string>>(options, "Tags"));
        Assert.Equal([80, 443], GetProperty<List<int>>(options, "Ports"));
        Assert.Equal("one", GetProperty<Dictionary<string, string>>(options, "Labels")["primary"]);
        Assert.Equal(100, GetProperty<Dictionary<string, int>>(options, "Limits")["requests"]);

        var children = GetProperty<Array>(options, "Children");
        Assert.Single(children);
        Assert.Equal("array", GetProperty(children.GetValue(0)!, "Name"));
        Assert.Equal(1, GetProperty<int>(children.GetValue(0)!, "Count"));

        var childList = GetProperty<System.Collections.IList>(options, "ChildList");
        Assert.Single(childList);
        Assert.Equal("list", GetProperty(childList[0]!, "Name"));
        Assert.False(GetProperty<bool>(childList[0]!, "Enabled"), "Expected list child boolean configuration to bind");

        var childMap = GetProperty<System.Collections.IDictionary>(options, "ChildMap");
        Assert.Single(childMap);
        Assert.Equal("dictionary", GetProperty(childMap["east"]!, "Name"));
        Assert.Equal(6.5, GetProperty<double>(childMap["east"]!, "Ratio"));
    }

    [Fact]
    public void Generator_AotComplexPositionalRecord_UsesDefaultsWhenSectionIsMissing()
    {
        var options = ResolveComplexOptions(new Dictionary<string, string?>());

        Assert.Equal(string.Empty, GetProperty<string>(options, "Name"));
        Assert.Null(GetProperty(options, "OptionalName"));
        Assert.Equal(0, GetProperty<int>(options, "Count"));
        Assert.Null(GetProperty(options, "OptionalCount"));
        Assert.False(GetProperty<bool>(options, "Enabled"), "Expected missing boolean configuration to use its default");
        Assert.Equal("First", GetProperty(options, "Mode")?.ToString());
        Assert.Equal(0.0, GetProperty<double>(options, "Ratio"));
        Assert.Null(GetProperty(options, "OptionalRatio"));
        var child = GetProperty(options, "Child")!;
        Assert.Equal(string.Empty, GetProperty<string>(child, "Name"));
        Assert.Equal(7, GetProperty<int>(child, "Count"));
        Assert.True(GetProperty<bool>(child, "Enabled"), "Expected missing nested boolean configuration to preserve its initializer");
        Assert.Equal("First", GetProperty(child, "Mode")?.ToString());
        Assert.Null(GetProperty(child, "OptionalCount"));
        Assert.Equal(1.5, GetProperty<double>(child, "Ratio"));
        Assert.Empty(GetProperty<string[]>(options, "Names"));
        Assert.Empty(GetProperty<int[]>(options, "Numbers"));
        Assert.Empty(GetProperty<List<string>>(options, "Tags"));
        Assert.Empty(GetProperty<List<int>>(options, "Ports"));
        Assert.Empty(GetProperty<Dictionary<string, string>>(options, "Labels"));
        Assert.Empty(GetProperty<Dictionary<string, int>>(options, "Limits"));
        Assert.Empty(GetProperty<Array>(options, "Children"));
        Assert.Empty(GetProperty<System.Collections.IList>(options, "ChildList"));
        Assert.Empty(GetProperty<System.Collections.IDictionary>(options, "ChildMap"));
    }

    [Fact]
    public void Generator_AotComplexPositionalRecord_UsesDefaultsAndSkipsInvalidValues()
    {
        var options = ResolveComplexOptions(new Dictionary<string, string?>
        {
            ["Complex:Count"] = "not-an-integer",
            ["Complex:OptionalCount"] = "not-an-integer",
            ["Complex:Enabled"] = "not-a-boolean",
            ["Complex:Mode"] = "not-an-enum",
            ["Complex:Ratio"] = "not-a-double",
            ["Complex:OptionalRatio"] = "not-a-double",
            ["Complex:Child:Count"] = "not-an-integer",
            ["Complex:Child:Enabled"] = "not-a-boolean",
            ["Complex:Child:Mode"] = "not-an-enum",
            ["Complex:Child:OptionalCount"] = "not-an-integer",
            ["Complex:Child:Ratio"] = "not-a-double",
            ["Complex:Numbers:0"] = "invalid",
            ["Complex:Numbers:1"] = "8",
            ["Complex:Ports:0"] = "invalid",
            ["Complex:Ports:1"] = "12",
            ["Complex:Limits:invalid"] = "invalid",
            ["Complex:Limits:valid"] = "15",
        });

        Assert.Equal(0, GetProperty<int>(options, "Count"));
        Assert.Null(GetProperty(options, "OptionalCount"));
        Assert.False(GetProperty<bool>(options, "Enabled"), "Expected invalid boolean configuration to use its default");
        Assert.Equal("First", GetProperty(options, "Mode")?.ToString());
        Assert.Equal(0.0, GetProperty<double>(options, "Ratio"));
        Assert.Null(GetProperty(options, "OptionalRatio"));

        var child = GetProperty(options, "Child")!;
        Assert.Equal(7, GetProperty<int>(child, "Count"));
        Assert.True(GetProperty<bool>(child, "Enabled"), "Expected invalid nested boolean configuration to preserve its initializer");
        Assert.Equal("First", GetProperty(child, "Mode")?.ToString());
        Assert.Null(GetProperty(child, "OptionalCount"));
        Assert.Equal(1.5, GetProperty<double>(child, "Ratio"));

        Assert.Equal([8], GetProperty<int[]>(options, "Numbers"));
        Assert.Equal([12], GetProperty<List<int>>(options, "Ports"));
        var limits = GetProperty<Dictionary<string, int>>(options, "Limits");
        Assert.Single(limits);
        Assert.Equal(15, limits["valid"]);
    }

    [Fact]
    public void Generator_PositionalRecordConstructor_UsesDefaultsByBehaviorCategory()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestApp" })]

            namespace TestApp
            {
                public readonly struct CustomValue;
                public sealed class CustomReference;

                [Options("Defaults")]
                public partial record DefaultOptions(
                    string AliasString,
                    System.String FrameworkString,
                    CustomValue Value,
                    int? NullableValue,
                    CustomReference Reference);
            }
            """;

        var generated = GeneratorTestRunner.ForOptions()
            .WithSource(source)
            .GetFileContaining("OptionsConstructors");

        Assert.Contains(
            "public DefaultOptions() : this(string.Empty, string.Empty, default, default, default!)",
            generated);
    }

    private static GeneratorTestRunner CreateComplexRunner()
    {
        return GeneratorTestRunner.ForOptions()
            .WithReference<IConfiguration>()
            .WithReference<IServiceCollection>()
            .WithReference<ServiceCollection>()
            .WithReference<IOptions<object>>()
            .WithReference<IServiceCatalog>()
            .WithSource(ComplexSource)
            .WithAotMode();
    }

    private static object ResolveComplexOptions(IReadOnlyDictionary<string, string?> values)
    {
        var compilation = CreateComplexRunner()
            .RunGeneratorCompilation(new TypeRegistryGenerator());
        using var assemblyStream = new MemoryStream();
        var emitResult = compilation.Emit(assemblyStream);
        Assert.True(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => diagnostic.ToString())));

        var assembly = System.Reflection.Assembly.Load(assemblyStream.ToArray());
        var configuration = new ConfigurationManager();
        foreach (var pair in values)
        {
            configuration[pair.Key] = pair.Value;
        }

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var registryType = assembly.GetType("TestAssembly.Generated.TypeRegistry")!;
        registryType.GetMethod("RegisterOptions")!.Invoke(null, [services, configuration]);
        using var provider = services.BuildServiceProvider();

        var optionsType = assembly.GetType("TestApp.ComplexOptions")!;
        var optionsServiceType = typeof(IOptions<>).MakeGenericType(optionsType);
        var options = provider.GetRequiredService(optionsServiceType);
        return optionsServiceType.GetProperty("Value")!.GetValue(options)!;
    }

    private static object? GetProperty(object instance, string name)
    {
        return instance.GetType().GetProperty(name)!.GetValue(instance);
    }

    private static T GetProperty<T>(object instance, string name)
    {
        return (T)GetProperty(instance, name)!;
    }
}
