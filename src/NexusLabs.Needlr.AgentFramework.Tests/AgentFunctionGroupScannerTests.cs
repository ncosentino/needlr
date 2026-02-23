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

public class AgentFunctionGroupScannerTests
{
    [Fact]
    public void AddAgentFunctionGroupsFromAssemblies_TaggedClass_CreatesAgentForGroup()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var factory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentFunctionsFromAssemblies([Assembly.GetExecutingAssembly()])
                .AddAgentFunctionGroupsFromAssemblies([Assembly.GetExecutingAssembly()]))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent(opts => opts.FunctionGroups = ["scanner-tools"]);

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    [Fact]
    public void AddAgentFunctionGroupsFromAssemblies_UnknownGroup_StillCreatesAgentWithNoTools()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var factory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentFunctionsFromAssemblies([Assembly.GetExecutingAssembly()])
                .AddAgentFunctionGroupsFromAssemblies([Assembly.GetExecutingAssembly()]))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent(opts => opts.FunctionGroups = ["nonexistent-group"]);

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    [Fact]
    public void AddAgentFunctionGroupsFromAssemblies_MultiGroupClass_AllGroupsResolvable()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var factory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentFunctionsFromAssemblies([Assembly.GetExecutingAssembly()])
                .AddAgentFunctionGroupsFromAssemblies([Assembly.GetExecutingAssembly()]))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        var agentA = factory.CreateAgent(opts => opts.FunctionGroups = ["scanner-tools"]);
        var agentB = factory.CreateAgent(opts => opts.FunctionGroups = ["scanner-utilities"]);

        Assert.NotNull(agentA);
        Assert.NotNull(agentB);
        Assert.IsAssignableFrom<AIAgent>(agentA);
        Assert.IsAssignableFrom<AIAgent>(agentB);
    }

    [Fact]
    public void AddAgentFunctionGroupsFromGenerated_EmptyGroups_DoesNotThrow()
    {
        var config = new ConfigurationBuilder().Build();

        var syringe = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .AddAgentFunctionGroupsFromGenerated(new Dictionary<string, IReadOnlyList<Type>>()))
            .BuildServiceProvider(config);

        Assert.NotNull(syringe.GetService<IAgentFactory>());
    }

    [AgentFunctionGroup("scanner-tools")]
    public sealed class ScannerToolsFunctions
    {
        [AgentFunction]
        [Description("Returns a scanned result.")]
        public string GetScanResult() => "scanned";
    }

    [AgentFunctionGroup("scanner-tools")]
    [AgentFunctionGroup("scanner-utilities")]
    public sealed class ScannerMultiGroupFunctions
    {
        [AgentFunction]
        [Description("Returns a utility value.")]
        public int GetUtilityValue() => 1;
    }
}
