using System.Reflection;
using System.Text;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseAutoRegistrationConventionTests
{
    private const BindingFlags RecordPropertyFlags =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void DataContracts_AreRecords()
    {
        Type[] dataContracts =
        [
            typeof(LangfuseDrainHealth),
            typeof(LangfuseEndpoints),
            typeof(LangfuseEvaluationScoreOptions),
            typeof(LangfuseExperimentItemLinkCounts),
            typeof(LangfuseExperimentItemLinkResult),
            typeof(LangfuseExperimentItemOptions),
            typeof(LangfuseExperimentItemResult<>),
            typeof(LangfuseExperimentRunOptions),
            typeof(LangfuseExperimentRunPublicationSnapshot),
            typeof(LangfuseExperimentRunScoreCounts),
            typeof(LangfuseExperimentRunScoreResult),
            typeof(LangfuseHttpOptions),
            typeof(LangfuseMetricsResult),
            typeof(LangfuseOptions),
            typeof(LangfusePublicationFailure),
            typeof(LangfusePublicationHealthSnapshot),
            typeof(LangfusePublicationOperationHealth),
            typeof(LangfuseRetryHealth),
            typeof(LangfuseScoreOptions),
            typeof(LangfuseScoreRecordResult),
            typeof(LangfuseTraceExportHealth),
            typeof(LangfuseTraceExportOptions),
        ];

        Assert.All(dataContracts, AssertRecord);
    }

    [Fact]
    public void ConcreteClasses_OptOutOfAutomaticRegistration()
    {
        var unsafeTypes = typeof(LangfuseOptions).Assembly
            .GetTypes()
            .Where(type =>
                type.IsClass
                && !type.IsAbstract
                && !type.IsNested
                && type.Namespace?.StartsWith(
                    "NexusLabs.Needlr.AgentFramework.Langfuse",
                    StringComparison.Ordinal) == true
                && !typeof(Delegate).IsAssignableFrom(type)
                && !typeof(Exception).IsAssignableFrom(type)
                && !IsRecord(type)
                && !type.IsDefined(typeof(DoNotAutoRegisterAttribute), inherit: true))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(unsafeTypes);
    }

    [Fact]
    public void RecordStringRepresentations_DoNotExposeCredentials()
    {
        const string publicKey = "pk-lf-public-sentinel";
        const string secretKey = "sk-lf-secret-sentinel";
        var options = new LangfuseOptions
        {
            PublicKey = publicKey,
            SecretKey = secretKey,
            Host = "https://langfuse.example.test",
        };
        var authorizationPayload = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}"));
        var endpoints = LangfuseEndpoints.Resolve(options);

        var optionsText = options.ToString();
        var endpointsText = endpoints.ToString();

        Assert.DoesNotContain(publicKey, optionsText, StringComparison.Ordinal);
        Assert.DoesNotContain(secretKey, optionsText, StringComparison.Ordinal);
        Assert.DoesNotContain(publicKey, endpointsText, StringComparison.Ordinal);
        Assert.DoesNotContain(secretKey, endpointsText, StringComparison.Ordinal);
        Assert.DoesNotContain(authorizationPayload, endpointsText, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", endpointsText, StringComparison.Ordinal);
    }

    private static void AssertRecord(Type type) =>
        Assert.True(
            IsRecord(type),
            $"Expected data contract '{type.FullName}' to be declared as a record.");

    private static bool IsRecord(Type type) =>
        type.GetProperty("EqualityContract", RecordPropertyFlags) is not null;
}
