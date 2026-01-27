using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

public sealed class FactoryGeneratorTests
{
    #region Detection Tests

    [Fact]
    public void Generator_WithGenerateFactoryAttribute_DetectsAttribute()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IDependency { }
    public class Dependency : IDependency { }

    [GenerateFactory]
    public class MyService
    {
        public MyService(IDependency dep, string connectionString) { }
    }
}";

        var generatedCode = RunGenerator(source);

        // Should generate factory-related code
        Assert.Contains("IMyServiceFactory", generatedCode);
    }

    [Fact]
    public void Generator_WithGenerateFactoryAttribute_DoesNotRegisterTypeDirectly()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IDependency { }

    [GenerateFactory]
    public class MyService
    {
        public MyService(IDependency dep, string connectionString) { }
    }
}";

        var generatedCode = RunGenerator(source);

        // Type should NOT be in injectable types (it's factory-only)
        Assert.DoesNotContain("new InjectableTypeInfo(typeof(global::TestApp.MyService)", generatedCode);
    }

    #endregion

    #region Constructor Parameter Partitioning Tests

    [Fact]
    public void Generator_WithMixedParams_PartitionsInjectableAndRuntime()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IDependency { }

    [GenerateFactory]
    public class MyService
    {
        public MyService(IDependency dep, string connectionString) { }
    }
}";

        var generatedCode = RunGenerator(source);

        // Factory Create method should only take the runtime param (string)
        Assert.Contains("Create(string connectionString)", generatedCode);
        // Factory should have IDependency as constructor param (injectable)
        Assert.Contains("IDependency", generatedCode);
    }

    [Fact]
    public void Generator_WithMultipleRuntimeParams_IncludesAllInFactory()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface ILogger { }

    [GenerateFactory]
    public class ReportBuilder
    {
        public ReportBuilder(ILogger logger, string templateName, int maxPages) { }
    }
}";

        var generatedCode = RunGenerator(source);

        // Factory Create method should take both runtime params
        Assert.Contains("Create(string templateName, int maxPages)", generatedCode);
    }

    [Fact]
    public void Generator_WithAllInjectableParams_WarnsAndSkipsFactory()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IDep1 { }
    public interface IDep2 { }

    [GenerateFactory]
    public class MyService
    {
        public MyService(IDep1 dep1, IDep2 dep2) { }
    }
}";

        var generatedCode = RunGenerator(source);

        // Should NOT generate factory interface (all params are injectable)
        Assert.DoesNotContain("IMyServiceFactory", generatedCode);
        // Should register normally instead (using shorthand constructor)
        Assert.Contains("new(typeof(global::TestApp.MyService)", generatedCode);
    }

    #endregion

    #region Func Generation Tests

    [Fact]
    public void Generator_WithModeAll_GeneratesFuncRegistration()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IDependency { }

    [GenerateFactory]
    public class MyService
    {
        public MyService(IDependency dep, string connectionString) { }
    }
}";

        var generatedCode = RunGenerator(source);

        // Should generate Func<string, MyService> registration
        Assert.Contains("Func<string, global::TestApp.MyService>", generatedCode);
        Assert.Contains("AddSingleton<Func<string", generatedCode);
    }

    [Fact]
    public void Generator_WithModeFuncOnly_GeneratesFuncButNoInterface()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IDependency { }

    [GenerateFactory(Mode = FactoryGenerationMode.Func)]
    public class MyService
    {
        public MyService(IDependency dep, string connectionString) { }
    }
}";

        var generatedCode = RunGenerator(source);

        // Should generate Func but NOT interface
        Assert.Contains("Func<string, global::TestApp.MyService>", generatedCode);
        Assert.DoesNotContain("IMyServiceFactory", generatedCode);
    }

    [Fact]
    public void Generator_WithModeInterfaceOnly_GeneratesInterfaceButNoFunc()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IDependency { }

    [GenerateFactory(Mode = FactoryGenerationMode.Interface)]
    public class MyService
    {
        public MyService(IDependency dep, string connectionString) { }
    }
}";

        var generatedCode = RunGenerator(source);

        // Should generate interface but NOT Func
        Assert.Contains("IMyServiceFactory", generatedCode);
        Assert.DoesNotContain("Func<string, global::TestApp.MyService>", generatedCode);
    }

    #endregion

    #region Factory Interface Generation Tests

    [Fact]
    public void Generator_WithModeAll_GeneratesFactoryInterface()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IDependency { }

    [GenerateFactory]
    public class MyService
    {
        public MyService(IDependency dep, string connectionString) { }
    }
}";

        var generatedCode = RunGenerator(source);

        // Should generate interface
        Assert.Contains("public interface IMyServiceFactory", generatedCode);
        Assert.Contains("MyService Create(string connectionString)", generatedCode);
    }

    [Fact]
    public void Generator_WithModeAll_GeneratesFactoryImplementation()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IDependency { }

    [GenerateFactory]
    public class MyService
    {
        public MyService(IDependency dep, string connectionString) { }
    }
}";

        var generatedCode = RunGenerator(source);

        // Should generate implementation class
        Assert.Contains("internal sealed class MyServiceFactory : IMyServiceFactory", generatedCode);
        // Implementation should have injectable deps as constructor params (uses type-derived name)
        Assert.Contains("public MyServiceFactory(global::TestApp.IDependency dependency)", generatedCode);
    }

    [Fact]
    public void Generator_FactoryRegisteredAsSingleton()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IDependency { }

    [GenerateFactory]
    public class MyService
    {
        public MyService(IDependency dep, string connectionString) { }
    }
}";

        var generatedCode = RunGenerator(source);

        // Factory should be registered as singleton (using TestAssembly as the generated namespace)
        Assert.Contains("AddSingleton<global::TestAssembly.Generated.IMyServiceFactory, global::TestAssembly.Generated.MyServiceFactory>", generatedCode);
    }

    #endregion

    #region Multiple Constructor Tests

    [Fact]
    public void Generator_WithMultipleConstructors_GeneratesMultipleCreateOverloads()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IDependency { }

    [GenerateFactory]
    public class MyService
    {
        public MyService(IDependency dep, string connectionString) { }
        public MyService(IDependency dep, string connectionString, int timeout) { }
    }
}";

        var generatedCode = RunGenerator(source);

        // Should generate both Create overloads
        Assert.Contains("Create(string connectionString)", generatedCode);
        Assert.Contains("Create(string connectionString, int timeout)", generatedCode);
    }

    [Fact]
    public void Generator_WithMultipleConstructors_GeneratesMultipleFuncs()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IDependency { }

    [GenerateFactory]
    public class MyService
    {
        public MyService(IDependency dep, string connectionString) { }
        public MyService(IDependency dep, string connectionString, int timeout) { }
    }
}";

        var generatedCode = RunGenerator(source);

        // Should generate both Func types
        Assert.Contains("Func<string, global::TestApp.MyService>", generatedCode);
        Assert.Contains("Func<string, int, global::TestApp.MyService>", generatedCode);
    }

    #endregion

    #region Interface Registration Tests

    [Fact]
    public void Generator_WithImplementedInterface_GeneratesFuncForInterface()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IDependency { }
    public interface IMyService { }

    [GenerateFactory]
    public class MyService : IMyService
    {
        public MyService(IDependency dep, string connectionString) { }
    }
}";

        var generatedCode = RunGenerator(source);

        // Should generate Func for both concrete and interface
        Assert.Contains("Func<string, global::TestApp.MyService>", generatedCode);
        Assert.Contains("Func<string, global::TestApp.IMyService>", generatedCode);
    }

    #endregion

    private static string RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = Basic.Reference.Assemblies.Net100.References.All
            .Concat(new[]
            {
                MetadataReference.CreateFromFile(typeof(GenerateTypeRegistryAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(GenerateFactoryAttribute).Assembly.Location),
            })
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TypeRegistryGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        var generatedTrees = outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .OrderBy(t => t.FilePath)
            .ToList();

        if (generatedTrees.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n\n", generatedTrees.Select(t => t.GetText().ToString()));
    }
}
