using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Formats.Json;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;

using MeaiReportingExperimentApp;

using NexusLabs.Needlr.AgentFramework.Evaluation;
using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;
using NexusLabs.Needlr.AgentFramework.Evaluation.Reporting;

Console.WriteLine("=== Needlr Experiment Runner + MEAI Reporting ===");
Console.WriteLine();
Console.WriteLine("This credential-free run demonstrates:");
Console.WriteLine("  - one MEAI ScenarioRun per statistical trial");
Console.WriteLine("  - the ScenarioRun chat client used by the experiment task");
Console.WriteLine("  - cached case/trial replay across separate Needlr run IDs");
Console.WriteLine("  - fresh-per-run cache isolation with retry-stable identity");
Console.WriteLine("  - result-store persistence as provider publication");
Console.WriteLine("  - official MEAI JSON report generation from the configured store");
Console.WriteLine();

var storageRoot = Path.Combine(
    Path.GetTempPath(),
    "needlr-meai-reporting-example");
if (Directory.Exists(storageRoot))
{
    Directory.Delete(storageRoot, recursive: true);
}

Directory.CreateDirectory(storageRoot);
var chatClient = new ReportingExampleChatClient();

await RunAsync(
    "replay-run-a",
    MeaiReportingResponseReuseMode.CaseAndTrialReplay);
var callsAfterFirstReplay = chatClient.CallCount;
await RunAsync(
    "replay-run-b",
    MeaiReportingResponseReuseMode.CaseAndTrialReplay);
var callsAfterSecondReplay = chatClient.CallCount;
await RunAsync(
    "fresh-run",
    MeaiReportingResponseReuseMode.FreshPerRun);
var callsAfterFreshRun = chatClient.CallCount;

if (callsAfterFirstReplay != 2
    || callsAfterSecondReplay != 2
    || callsAfterFreshRun != 4)
{
    throw new InvalidOperationException(
        "The example did not observe the expected subject and judge cache behavior.");
}

var resultStore = new DiskBasedResultStore(storageRoot);
var storedResults = new List<ScenarioRunResult>();
await foreach (var result in resultStore.ReadResultsAsync())
{
    storedResults.Add(result);
}

var reportPath = Path.Combine(storageRoot, "report.json");
await new JsonReportWriter(reportPath).WriteReportAsync(storedResults);

Console.WriteLine($"Uncached calls after first replay run:  {callsAfterFirstReplay}");
Console.WriteLine($"Uncached calls after second replay run: {callsAfterSecondReplay}");
Console.WriteLine($"Uncached calls after fresh run:         {callsAfterFreshRun}");
Console.WriteLine($"Stored ScenarioRun results:             {storedResults.Count}");
Console.WriteLine($"Official MEAI JSON report:              {reportPath}");
Console.WriteLine();
Console.WriteLine("All checks passed.");
return 0;

async Task RunAsync(
    string runId,
    MeaiReportingResponseReuseMode responseReuseMode)
{
    var configuration = DiskBasedReportingConfiguration.Create(
        storageRoot,
        [new TaskCompletionEvaluator()],
        new ChatConfiguration(chatClient),
        enableResponseCaching: true,
        executionName: runId);
    var definition = new ExperimentDefinition<
        ReportingExampleCase,
        ReportingExampleOutput>
    {
        Name = "meai-reporting-example",
        CaseSource = new LocalExperimentCaseSource<ReportingExampleCase>(
            "local",
            [
                new ExperimentCase<ReportingExampleCase>
                {
                    Id = "refund.eligibility",
                    Value = new ReportingExampleCase(
                        "Decide whether the eligible refund should be approved."),
                    Tags = ["credential-free", "reporting"],
                },
            ]),
        Task = async (context, cancellationToken) =>
        {
            var reporting = context.Features
                .GetRequired<MeaiReportingExperimentItem>();
            var messages = new ChatMessage[]
            {
                new(ChatRole.User, context.Case.Value.Prompt),
            };
            var response = await reporting.ChatConfiguration!.ChatClient
                .GetResponseAsync(
                    messages,
                    cancellationToken: cancellationToken);
            return new ReportingExampleOutput(messages, response);
        },
    }.WithMeaiReporting(
        configuration,
        context => new EvaluationInputs(
            context.Output.Messages,
            context.Output.Response),
        new MeaiReportingExperimentOptions<
            ReportingExampleCase,
            ReportingExampleOutput>
        {
            ResponseReuseMode = responseReuseMode,
            IsRequired = true,
        });

    var outcome = await new ExperimentRunner().RunAsync(
        definition,
        new ExperimentRunOptions
        {
            RunId = runId,
            MaxConcurrency = 1,
        });
    var item = outcome.Result.Items.Single();
    var publication = item.Publications.Single();
    Console.WriteLine(
        $"{runId}: quality={item.Status}, " +
        $"publication={publication.Status}, " +
        $"metrics={item.Metrics.Count}");
}
