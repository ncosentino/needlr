using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Unit tests for <see cref="AgentFactory"/>, covering attribute wiring, function
/// group scoping, function type scoping, and by-name lookup.
/// </summary>
public class AgentFactoryTests
{
    private static IAgentFactory CreateFactory(
        Func<AgentFrameworkSyringe, Assembly, AgentFrameworkSyringe>? configure = null)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        var assembly = Assembly.GetExecutingAssembly();

        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af =>
            {
                af = af.Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object);
                if (configure != null)
                    af = configure(af, assembly);
                return af;
            })
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();
    }

    private static IServiceProvider CreateServiceProvider(
        Func<AgentFrameworkSyringe, Assembly, AgentFrameworkSyringe>? configure = null)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        var assembly = Assembly.GetExecutingAssembly();

        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af =>
            {
                af = af.Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object);
                if (configure != null)
                    af = configure(af, assembly);
                return af;
            })
            .BuildServiceProvider(config);
    }

    // -------------------------------------------------------------------------
    // Attribute wiring — Instructions
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateAgent_FromAttribute_WithInstructions_ReturnsAgent()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm])
            .AddAgent<FactoryTestAgent>());

        var agent = factory.CreateAgent<FactoryTestAgent>();

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    [Fact]
    public void CreateAgent_FromAttribute_WithoutInstructions_ReturnsAgent()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgent<NoInstructionsFactoryAgent>());

        var agent = factory.CreateAgent<NoInstructionsFactoryAgent>();

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    // -------------------------------------------------------------------------
    // FunctionGroups scoping
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateAgent_WithFunctionGroup_ReturnsAgent()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm])
            .AddAgentFunctionGroupsFromAssemblies([asm]));

        var agent = factory.CreateAgent(opts => opts.FunctionGroups = ["agent-factory-test"]);

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    [Fact]
    public void CreateAgent_WithMultipleFunctionGroups_ReturnsAgent()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm])
            .AddAgentFunctionGroupsFromAssemblies([asm]));

        var agent = factory.CreateAgent(opts =>
            opts.FunctionGroups = ["agent-factory-test", "agent-factory-test-b"]);

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    [Fact]
    public void CreateAgent_WithUnknownFunctionGroup_DoesNotThrow_ReturnsAgent()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm])
            .AddAgentFunctionGroupsFromAssemblies([asm]));

        // Unknown group — silently produces an agent with zero tools
        var agent = factory.CreateAgent(opts => opts.FunctionGroups = ["nonexistent-group"]);

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    // -------------------------------------------------------------------------
    // FunctionTypes scoping
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateAgent_WithEmptyFunctionTypesArray_ReturnsAgent()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm]));

        // Empty array means explicit zero-tool scope — not an error
        var agent = factory.CreateAgent(opts => opts.FunctionTypes = []);

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    [Fact]
    public void CreateAgent_WithExplicitFunctionTypes_ReturnsAgent()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm]));

        var agent = factory.CreateAgent(opts =>
            opts.FunctionTypes = [typeof(FactoryTestFunctions)]);

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    [Fact]
    public void CreateAgent_WithNoScopeSet_AllToolsApplied_ReturnsAgent()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm])
            .AddAgentFunctionGroupsFromAssemblies([asm]));

        // Neither FunctionTypes nor FunctionGroups set — receives all registered tools
        var agent = factory.CreateAgent();

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    // -------------------------------------------------------------------------
    // By-name lookup
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateAgent_ByName_RegisteredType_ReturnsAgent()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm])
            .AddAgent<FactoryTestAgent>());

        var agent = factory.CreateAgent(nameof(FactoryTestAgent));

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    [Fact]
    public void CreateAgent_ByName_UnregisteredType_ThrowsInvalidOperation()
    {
        var factory = CreateFactory();

        Assert.Throws<InvalidOperationException>(() =>
            factory.CreateAgent("NonExistentAgent"));
    }

    // -------------------------------------------------------------------------
    // Missing [NeedlrAiAgent] attribute
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateAgent_FromType_MissingAttribute_ThrowsInvalidOperation()
    {
        var factory = CreateFactory((af, asm) => af
            .AddAgentFunctionsFromAssemblies([asm]));

        Assert.Throws<InvalidOperationException>(() =>
            factory.CreateAgent<UndecoratedFactoryAgent>());
    }

    // Provider path

    [Fact]
    public void BuildFunctions_WhenGeneratedProviderSucceeds_DoesNotFallbackToReflection()
    {
        var stubFn = AIFunctionFactory.Create(() => "stub", name: "Stub");
        IReadOnlyList<AIFunction>? outFunctions = new List<AIFunction> { stubFn }.AsReadOnly();

        var mockProvider = new Mock<IAIFunctionProvider>();
        mockProvider
            .Setup(p => p.TryGetFunctions(typeof(FactoryTestFunctions), It.IsAny<IServiceProvider>(), out outFunctions))
            .Returns(true);

        var serviceProvider = CreateServiceProvider((af, asm) =>
            af.AddAgentFunctions([typeof(FactoryTestFunctions)]));

        using var scope = AgentFrameworkGeneratedBootstrap.BeginTestScope(
            functionTypes: () => [typeof(FactoryTestFunctions)],
            groupTypes: () => new Dictionary<string, IReadOnlyList<Type>>(),
            agentTypes: () => [],
            aiFunctionProvider: mockProvider.Object);

        var factory = serviceProvider.GetRequiredService<IAgentFactory>();
        var agent = factory.CreateAgent(opts => opts.FunctionTypes = [typeof(FactoryTestFunctions)]);

        Assert.NotNull(agent);
        Assert.NotEmpty(mockProvider.Invocations);
    }

    [Fact]
    public void BuildFunctions_WhenGeneratedProviderReturnsFalse_FallsBackToReflection()
    {
        IReadOnlyList<AIFunction>? outFunctions = null;

        var mockProvider = new Mock<IAIFunctionProvider>();
        mockProvider
            .Setup(p => p.TryGetFunctions(It.IsAny<Type>(), It.IsAny<IServiceProvider>(), out outFunctions))
            .Returns(false);

        var serviceProvider = CreateServiceProvider((af, asm) =>
            af.AddAgentFunctions([typeof(FactoryTestFunctions)]));

        using var scope = AgentFrameworkGeneratedBootstrap.BeginTestScope(
            functionTypes: () => [typeof(FactoryTestFunctions)],
            groupTypes: () => new Dictionary<string, IReadOnlyList<Type>>(),
            agentTypes: () => [],
            aiFunctionProvider: mockProvider.Object);

        var factory = serviceProvider.GetRequiredService<IAgentFactory>();
        var agent = factory.CreateAgent(opts => opts.FunctionTypes = [typeof(FactoryTestFunctions)]);

        Assert.NotNull(agent);
        Assert.NotEmpty(mockProvider.Invocations);
    }

    [Fact]
    public void UsingAgentFramework_DoesNotCarry_RequiresDynamicCodeAttribute()
    {
        var methods = typeof(SyringeExtensionsForAgentFramework)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "UsingAgentFramework")
            .ToList();

        Assert.NotEmpty(methods);
        foreach (var method in methods)
            Assert.Null(method.GetCustomAttribute<RequiresDynamicCodeAttribute>());
    }
}

// ---------------------------------------------------------------------------
// Test agents and function classes — at namespace level
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "You are a test agent for AgentFactory tests.")]
public sealed class FactoryTestAgent { }

[NeedlrAiAgent]
public sealed class NoInstructionsFactoryAgent { }

public sealed class UndecoratedFactoryAgent { }

[AgentFunctionGroup("agent-factory-test")]
public sealed class FactoryTestFunctions
{
    [AgentFunction]
    [Description("Returns test data for agent factory tests.")]
    public string GetTestData() => "factory-test";
}

[AgentFunctionGroup("agent-factory-test-b")]
public sealed class FactoryTestFunctionsB
{
    [AgentFunction]
    [Description("Returns secondary test data for agent factory tests.")]
    public string GetSecondaryData() => "factory-test-b";
}
