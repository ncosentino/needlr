using System.ComponentModel;
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
/// Parity tests verifying that the reflection discovery path
/// (<c>AddAgentFunctionGroupsFromAssemblies</c>) and the compile-time generated path
/// (<c>AddAgentFunctionGroupsFromGenerated</c>) produce functionally equivalent results.
/// </summary>
public class FunctionGroupParityTests
{
    // Represents what the source generator would emit for this test assembly.
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<Type>> GeneratedGroups =
        new Dictionary<string, IReadOnlyList<Type>>
        {
            ["parity-research"] = [typeof(ParityResearchFunctions)]
        };

    [Fact]
    public void GroupScoping_ReflectionAndGeneratedPaths_BothCreateAgentForSameGroup()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        var assembly = Assembly.GetExecutingAssembly();

        var reflectionFactory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentFunctionsFromAssemblies([assembly])
                .AddAgentFunctionGroupsFromAssemblies([assembly]))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        var generatedFactory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentFunctionsFromGenerated([typeof(ParityResearchFunctions)])
                .AddAgentFunctionGroupsFromGenerated(GeneratedGroups))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        var reflectionAgent = reflectionFactory.CreateAgent(opts => opts.FunctionGroups = ["parity-research"]);
        var generatedAgent = generatedFactory.CreateAgent(opts => opts.FunctionGroups = ["parity-research"]);

        Assert.NotNull(reflectionAgent);
        Assert.NotNull(generatedAgent);
        Assert.IsAssignableFrom<AIAgent>(reflectionAgent);
        Assert.IsAssignableFrom<AIAgent>(generatedAgent);
    }

    [Fact]
    public void GroupScoping_ReflectionAndGeneratedPaths_BothHandleUnknownGroup()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        var assembly = Assembly.GetExecutingAssembly();

        var reflectionFactory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentFunctionsFromAssemblies([assembly])
                .AddAgentFunctionGroupsFromAssemblies([assembly]))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        var generatedFactory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentFunctionsFromGenerated([typeof(ParityResearchFunctions)])
                .AddAgentFunctionGroupsFromGenerated(GeneratedGroups))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        // An unknown group should produce an agent with zero tools — not throw
        var reflectionAgent = reflectionFactory.CreateAgent(opts => opts.FunctionGroups = ["unknown-group"]);
        var generatedAgent = generatedFactory.CreateAgent(opts => opts.FunctionGroups = ["unknown-group"]);

        Assert.NotNull(reflectionAgent);
        Assert.NotNull(generatedAgent);
    }

    [Fact]
    public void FunctionTypes_ReflectionAndGeneratedPaths_BothCreateValidAgent()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        var assembly = Assembly.GetExecutingAssembly();

        var reflectionFactory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentFunctionsFromAssemblies([assembly]))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        var generatedFactory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentFunctionsFromGenerated([typeof(ParityResearchFunctions)]))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        var reflectionAgent = reflectionFactory.CreateAgent(opts => opts.FunctionTypes = [typeof(ParityResearchFunctions)]);
        var generatedAgent = generatedFactory.CreateAgent(opts => opts.FunctionTypes = [typeof(ParityResearchFunctions)]);

        Assert.NotNull(reflectionAgent);
        Assert.NotNull(generatedAgent);
        Assert.IsAssignableFrom<AIAgent>(reflectionAgent);
        Assert.IsAssignableFrom<AIAgent>(generatedAgent);
    }

    [Fact]
    public void AddAgentFunctionGroupsFromGenerated_MergesWithExistingGroups()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var factory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentFunctionsFromGenerated([typeof(ParityResearchFunctions), typeof(ParityWriterFunctions)])
                .AddAgentFunctionGroupsFromGenerated(new Dictionary<string, IReadOnlyList<Type>>
                {
                    ["parity-research"] = [typeof(ParityResearchFunctions)]
                })
                .AddAgentFunctionGroupsFromGenerated(new Dictionary<string, IReadOnlyList<Type>>
                {
                    ["parity-research"] = [typeof(ParityWriterFunctions)],
                    ["parity-writer"] = [typeof(ParityWriterFunctions)]
                }))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        // Both types were merged into "parity-research", so agent should be created
        var researchAgent = factory.CreateAgent(opts => opts.FunctionGroups = ["parity-research"]);
        var writerAgent = factory.CreateAgent(opts => opts.FunctionGroups = ["parity-writer"]);

        Assert.NotNull(researchAgent);
        Assert.NotNull(writerAgent);
        Assert.IsAssignableFrom<AIAgent>(researchAgent);
        Assert.IsAssignableFrom<AIAgent>(writerAgent);
    }

    [AgentFunctionGroup("parity-research")]
    public sealed class ParityResearchFunctions
    {
        [AgentFunction]
        [Description("Returns research data for parity testing.")]
        public string GetResearchData() => "data";
    }

    public sealed class ParityWriterFunctions
    {
        [AgentFunction]
        [Description("Writes content for parity testing.")]
        public string WriteContent(string input) => $"Written: {input}";
    }
}
