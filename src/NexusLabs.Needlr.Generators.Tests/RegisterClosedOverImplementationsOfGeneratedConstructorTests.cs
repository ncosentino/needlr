using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Coverage for combining <c>[RegisterClosedOverImplementationsOf]</c> composition
/// types with a generated constructor (<c>[GenerateConstructor]</c> or a positive
/// field-level constructor guard trigger). <see cref="ComposedRegistrationDiscoveryHelper"/>
/// must resolve the closed composition's dependencies from the same field-derived model
/// <see cref="ConstructorGenerationDiscoveryHelper"/> uses for source emission, since the
/// composition type's constructor emitted by the sibling <c>GeneratedConstructorGenerator</c>
/// pass is otherwise invisible to Roslyn's <c>InstanceConstructors</c> scan within this
/// compilation.
/// </summary>
public sealed class RegisterClosedOverImplementationsOfGeneratedConstructorTests
{
    [Fact]
    public void Composition_WithGenerateConstructor_ResolvesFieldDerivedDependency()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IBarDefinition<TData> where TData : class { }
                public interface IBar { }
                public sealed class OnlyData { }
                public sealed class OnlyBar : IBarDefinition<OnlyData> { }

                [RegisterClosedOverImplementationsOf(typeof(IBarDefinition<>), As = typeof(IBar))]
                [GenerateConstructor]
                public partial class BarCore<TData> : IBar where TData : class
                {
                    private readonly IBarDefinition<TData> _definition;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("AddSingleton<global::TestNamespace.IBar>", generatedCode);
        Assert.Contains("new global::TestNamespace.BarCore<global::TestNamespace.OnlyData>(", generatedCode);
        Assert.Contains("sp.GetRequiredService<global::TestNamespace.IBarDefinition<global::TestNamespace.OnlyData>>()", generatedCode);
    }

    [Fact]
    public void Composition_WithGenerateConstructor_MultipleFields_ResolvesInDeclarationOrder()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IFooDefinition<TData> where TData : class { }
                public interface IFooStore<TData> where TData : class { }
                public interface IFoo { }
                public sealed class OnlyData { }
                public sealed class OnlyFoo : IFooDefinition<OnlyData> { }

                [RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>), As = typeof(IFoo))]
                [GenerateConstructor]
                public partial class FooCore<TData> : IFoo where TData : class
                {
                    private readonly IFooDefinition<TData> _definition;
                    private readonly IFooStore<TData> _store;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        var ctorCallIndex = generatedCode.IndexOf("new global::TestNamespace.FooCore<global::TestNamespace.OnlyData>(", System.StringComparison.Ordinal);
        var definitionArgIndex = generatedCode.IndexOf("sp.GetRequiredService<global::TestNamespace.IFooDefinition<global::TestNamespace.OnlyData>>()", System.StringComparison.Ordinal);
        var storeArgIndex = generatedCode.IndexOf("sp.GetRequiredService<global::TestNamespace.IFooStore<global::TestNamespace.OnlyData>>()", System.StringComparison.Ordinal);

        Assert.True(ctorCallIndex >= 0, "Expected the closed composition constructor call to be generated.");
        Assert.True(definitionArgIndex > ctorCallIndex, "Expected the definition argument to follow the constructor call.");
        Assert.True(storeArgIndex > definitionArgIndex, "Expected the store argument to follow the definition argument, matching field declaration order.");
    }

    [Fact]
    public void Composition_WithFieldTriggeredGeneration_ResolvesFieldDerivedDependency()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IBazDefinition<TData> where TData : class { }
                public interface IBaz { }
                public sealed class OnlyData { }
                public sealed class OnlyBaz : IBazDefinition<OnlyData> { }

                [RegisterClosedOverImplementationsOf(typeof(IBazDefinition<>), As = typeof(IBaz))]
                public partial class BazCore<TData> : IBaz where TData : class
                {
                    [ConstructorGuard(ConstructorGuardKind.NotNull)]
                    private readonly IBazDefinition<TData> _definition;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("new global::TestNamespace.BazCore<global::TestNamespace.OnlyData>(", generatedCode);
        Assert.Contains("sp.GetRequiredService<global::TestNamespace.IBazDefinition<global::TestNamespace.OnlyData>>()", generatedCode);
    }

    private static string RunGenerator(string source)
    {
        return GeneratorTestRunner.ForTypeRegistry()
            .WithReference<RegisterClosedOverImplementationsOfAttribute>()
            .WithReference<GenerateConstructorAttribute>()
            .WithSource(source)
            .RunTypeRegistryGenerator();
    }
}
