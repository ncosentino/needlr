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

public class AssemblyAgentFunctionScannerTests
{
    [Fact]
    public void AddAgentFunctionsFromAssemblies_WithCurrentAssembly_RegistersIAgentFactory()
    {
        var config = new ConfigurationBuilder().Build();

        var serviceProvider = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af.AddAgentFunctionsFromAssemblies())
            .BuildServiceProvider(config);

        var factory = serviceProvider.GetService<IAgentFactory>();
        Assert.NotNull(factory);
    }

    [Fact]
    public void AddAgentFunctionsFromAssemblies_ExplicitAssembly_CreatesAgentWithFunctions()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();

        var factory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentFunctionsFromAssemblies([Assembly.GetExecutingAssembly()]))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent(opts =>
        {
            opts.FunctionTypes = [typeof(SampleFunctions)];
        });

        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    public sealed class SampleFunctions
    {
        [AgentFunction]
        [Description("Returns a greeting.")]
        public string Greet(string name) => $"Hello, {name}!";
    }

    public static class StaticSampleFunctions
    {
        [AgentFunction]
        [Description("Returns the answer.")]
        public static int GetAnswer() => 42;
    }
}

