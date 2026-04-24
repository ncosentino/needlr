using Microsoft.Agents.AI.Workflows;

namespace GeneratorCoexistenceApp.Executors;

/// <summary>
/// MAF-side: <see cref="MessageHandlerAttribute"/> on methods tells MAF's
/// <c>Workflows.Generators</c> source generator to emit route configuration.
/// This class demonstrates MAF's executor programming model — complementary
/// to Needlr's <c>[AgentFunctionGroup]</c> pattern.
/// </summary>
/// <remarks>
/// <para>
/// The MAF generator emits a <c>ConfigureRoutes</c> override that dispatches
/// incoming <see cref="EchoRequest"/> messages to <see cref="HandleEchoAsync"/>.
/// This is a DIFFERENT concern from Needlr's generator: MAF routes typed messages
/// to handler methods, while Needlr discovers and registers <c>AIFunction</c> tools.
/// </para>
/// <para>
/// Both generators run in the same compilation. If they produce conflicting output,
/// this project will fail to build — serving as a living regression test.
/// </para>
/// </remarks>
public partial class EchoExecutor : Executor
{
    public EchoExecutor() : base("echo-executor") { }

    [MessageHandler]
    private ValueTask HandleEchoAsync(
        EchoRequest message,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"  [MAF Executor] Received EchoRequest: \"{message.Text}\"");
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// A simple message type routed by MAF's <c>[MessageHandler]</c> generator.
/// </summary>
public sealed record EchoRequest(string Text);
