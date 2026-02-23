using System.ComponentModel;
using System.Reflection;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.AgentFramework.Workflows;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for <see cref="AgentFactoryWorkflowExtensions"/>.
/// </summary>
public class BuildHandoffWorkflowExtensionsTests
{
    private IAgentFactory CreateFactory()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentFunctionsFromGenerated([typeof(HandoffTestFunctions)]))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();
    }

    [Fact]
    public void BuildHandoffWorkflow_ParamsOverload_ReturnsWorkflow()
    {
        var factory = CreateFactory();
        var triage = factory.CreateAgent(opts => { opts.Name = "Triage"; opts.FunctionTypes = []; });
        var billing = factory.CreateAgent(opts => { opts.Name = "Billing"; opts.FunctionTypes = []; });
        var tech = factory.CreateAgent(opts => { opts.Name = "Tech"; opts.FunctionTypes = []; });

        var workflow = factory.BuildHandoffWorkflow(triage, billing, tech);

        Assert.NotNull(workflow);
    }

    [Fact]
    public void BuildHandoffWorkflow_ReasonsTuplesOverload_ReturnsWorkflow()
    {
        var factory = CreateFactory();
        var triage = factory.CreateAgent(opts => { opts.Name = "Triage"; opts.FunctionTypes = []; });
        var billing = factory.CreateAgent(opts => { opts.Name = "Billing"; opts.FunctionTypes = []; });
        var tech = factory.CreateAgent(opts => { opts.Name = "Tech"; opts.FunctionTypes = []; });

        var workflow = factory.BuildHandoffWorkflow(
            triage,
            (billing, "For billing and payment questions"),
            (tech, "For technical issues"));

        Assert.NotNull(workflow);
    }

    [Fact]
    public void BuildHandoffWorkflow_ParityWithRawMAFBuilder_ProducesSameStructure()
    {
        var factory = CreateFactory();
        var triage = factory.CreateAgent(opts => { opts.Name = "Triage"; opts.FunctionTypes = []; });
        var billing = factory.CreateAgent(opts => { opts.Name = "Billing"; opts.FunctionTypes = []; });
        var tech = factory.CreateAgent(opts => { opts.Name = "Tech"; opts.FunctionTypes = []; });

        // Needlr ergonomic wrapper
        var needlrWorkflow = factory.BuildHandoffWorkflow(triage, billing, tech);

        // Raw MAF equivalent — same logic, the asymmetric API that Needlr hides
        var rawWorkflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(triage)
            .WithHandoffs(triage, [billing, tech])
            .Build();

        // Both workflows should start with the same executor (the triage agent)
        Assert.Equal(rawWorkflow.StartExecutorId, needlrWorkflow.StartExecutorId);
        // Both should bind the same number of executors (triage + 2 targets)
        Assert.Equal(rawWorkflow.ReflectExecutors().Count, needlrWorkflow.ReflectExecutors().Count);
    }

    [Fact]
    public void BuildHandoffWorkflow_ParityBetweenReasonAndNoReasonOverloads_SameExecutorCount()
    {
        var factory = CreateFactory();
        var triage = factory.CreateAgent(opts => { opts.Name = "Triage"; opts.FunctionTypes = []; });
        var billing = factory.CreateAgent(opts => { opts.Name = "Billing"; opts.FunctionTypes = []; });
        var tech = factory.CreateAgent(opts => { opts.Name = "Tech"; opts.FunctionTypes = []; });

        var paramsWorkflow = factory.BuildHandoffWorkflow(triage, billing, tech);
        var reasonsWorkflow = factory.BuildHandoffWorkflow(
            triage,
            (billing, "For billing"),
            (tech, "For tech"));

        Assert.Equal(paramsWorkflow.StartExecutorId, reasonsWorkflow.StartExecutorId);
        Assert.Equal(paramsWorkflow.ReflectExecutors().Count, reasonsWorkflow.ReflectExecutors().Count);
    }

    [Fact]
    public void BuildHandoffWorkflow_AgentsCreatedViaFunctionGroupsPath_CanParticipateInHandoff()
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        var assembly = Assembly.GetExecutingAssembly();

        // Agents whose functions were discovered via group scanning participate correctly
        var factory = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => af
                .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)
                .AddAgentFunctionsFromAssemblies([assembly])
                .AddAgentFunctionGroupsFromAssemblies([assembly]))
            .BuildServiceProvider(config)
            .GetRequiredService<IAgentFactory>();

        var triage = factory.CreateAgent(opts => { opts.Name = "Triage"; opts.FunctionTypes = []; });
        var specialist = factory.CreateAgent(opts =>
        {
            opts.Name = "Specialist";
            opts.FunctionGroups = ["handoff-test"];
        });

        var workflow = factory.BuildHandoffWorkflow(triage, specialist);

        Assert.NotNull(workflow);
        Assert.IsAssignableFrom<Workflow>(workflow);
    }

    [AgentFunctionGroup("handoff-test")]
    public sealed class HandoffTestFunctions
    {
        [AgentFunction]
        [Description("Returns test data for handoff workflow tests.")]
        public string GetTestData() => "test";
    }
}
