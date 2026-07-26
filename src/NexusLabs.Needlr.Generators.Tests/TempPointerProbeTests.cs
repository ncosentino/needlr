using System.Linq;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

public sealed class TempPointerProbeTests
{
    [Fact]
    public void PointerProperty()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public unsafe partial record Request(string Name)
            {
                [RecordConstructorOverloadParameter]
                public int* Pointer { get; init; }
            }
            """;

        var files = GeneratorTestRunner.ForConstructorGeneration()
            .WithSource(source)
            .RunGenerator(new RecordConstructorOverloadGenerator());
        Assert.Fail(string.Join("\n", files.Select(f => f.Content)));
    }

    [Fact]
    public void PointerPrimaryParameter()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public unsafe partial record Request(int* Name)
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }
            }
            """;

        var compilation = GeneratorTestRunner.ForConstructorGeneration()
            .WithSource(source)
            .RunGeneratorCompilation(new RecordConstructorOverloadGenerator());
        Assert.Fail(string.Join("\n", compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).Select(d => d.ToString()).Take(20)));
    }
}
