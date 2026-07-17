using System.Reflection;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentApiOverloadTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public void PublicApis_ExposeOnlyExplicitNonOptionalOverloads()
    {
        var caseSourceMethods = typeof(IExperimentCaseSource<int>)
            .GetMethods()
            .Where(method => method.Name == nameof(IExperimentCaseSource<int>.LoadAsync))
            .OrderBy(method => method.GetParameters().Length)
            .ToArray();
        Assert.Equal([0, 1], caseSourceMethods.Select(GetParameterCount));
        Assert.All(
            caseSourceMethods,
            method => Assert.True(
                method.IsAbstract,
                "Experiment case source overloads must remain abstract interface declarations."));

        var runnerMethods = typeof(IExperimentRunner)
            .GetMethods()
            .Where(method => method.Name == nameof(IExperimentRunner.RunAsync))
            .OrderBy(method => method.GetParameters().Length)
            .ToArray();
        Assert.Equal([2, 3], runnerMethods.Select(GetParameterCount));
        Assert.All(
            runnerMethods,
            method => Assert.True(
                method.IsAbstract,
                "Experiment runner overloads must remain abstract interface declarations."));

        var runnerConstructors = typeof(ExperimentRunner)
            .GetConstructors()
            .OrderBy(constructor => constructor.GetParameters().Length)
            .ToArray();
        Assert.Equal([0, 1], runnerConstructors.Select(GetParameterCount));
        Assert.Equal(
            typeof(TimeProvider),
            Assert.Single(runnerConstructors[1].GetParameters()).ParameterType);

        var writerConstructors = typeof(ExperimentJsonArtifactWriter)
            .GetConstructors()
            .OrderBy(constructor => constructor.GetParameters().Length)
            .ToArray();
        Assert.Equal([0, 1], writerConstructors.Select(GetParameterCount));
        Assert.Equal(
            typeof(bool),
            Assert.Single(writerConstructors[1].GetParameters()).ParameterType);

        var writerMethods = typeof(ExperimentJsonArtifactWriter)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var serializeMethods = writerMethods
            .Where(method => method.Name == nameof(ExperimentJsonArtifactWriter.Serialize))
            .OrderBy(method => method.GetParameters().Length)
            .ToArray();
        Assert.Equal([1, 2, 3], serializeMethods.Select(GetParameterCount));

        var writeMethods = writerMethods
            .Where(method => method.Name == nameof(ExperimentJsonArtifactWriter.WriteAsync))
            .OrderBy(method => method.GetParameters().Length)
            .ToArray();
        Assert.Equal([2, 3, 4, 5], writeMethods.Select(GetParameterCount));

        var publicParameters = caseSourceMethods
            .Concat(runnerMethods)
            .Concat(serializeMethods)
            .Concat(writeMethods)
            .SelectMany(method => method.GetParameters())
            .Concat(runnerConstructors.SelectMany(constructor => constructor.GetParameters()))
            .Concat(writerConstructors.SelectMany(constructor => constructor.GetParameters()));
        Assert.DoesNotContain(publicParameters, parameter => parameter.IsOptional);
    }

    [Fact]
    public async Task TokenlessOverloads_DelegateWithoutChangingResults()
    {
        var source = new LocalExperimentCaseSource<int>(
            "local",
            [new ExperimentCase<int> { Id = "case-1", Value = 7 }]);

#pragma warning disable xUnit1051 // This test intentionally exercises the tokenless case-source overload.
        var tokenlessSourceResult = await source.LoadAsync();
#pragma warning restore xUnit1051
        var cancellableSourceResult = await source.LoadAsync(_cancellationToken);

        Assert.Equal(cancellableSourceResult.Source, tokenlessSourceResult.Source);
        Assert.Equal(cancellableSourceResult.Cases, tokenlessSourceResult.Cases);

        var definition = new ExperimentDefinition<int, int>
        {
            Name = "tokenless",
            CaseSource = source,
            Task = (context, _) => ValueTask.FromResult(context.Case.Value),
        };

#pragma warning disable xUnit1051 // This test intentionally exercises the tokenless runner overload.
        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "tokenless-run", MaxConcurrency = 1 });
#pragma warning restore xUnit1051

        var item = Assert.Single(outcome.Result.Items);
        Assert.Equal(ExperimentItemStatus.Succeeded, item.Status);
        Assert.Equal(7, item.Output);
    }

    [Fact]
    public async Task ExperimentRunner_TimeProviderConstructorNullUsesSystemProvider()
    {
        var runner = new ExperimentRunner(null);
        var outcome = await runner.RunAsync(
            new ExperimentDefinition<int, int>
            {
                Name = "null-time-provider",
                CaseSource = new LocalExperimentCaseSource<int>(
                    "local",
                    [new ExperimentCase<int> { Id = "case-1", Value = 7 }]),
                Task = (context, _) => ValueTask.FromResult(context.Case.Value),
            },
            new ExperimentRunOptions
            {
                RunId = "null-time-provider-run",
                MaxConcurrency = 1,
            },
            _cancellationToken);

        Assert.Equal(
            ExperimentItemStatus.Succeeded,
            Assert.Single(outcome.Result.Items).Status);
    }

    private static int GetParameterCount(MethodBase method) =>
        method.GetParameters().Length;
}
