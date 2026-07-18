using System.Reflection;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseExperimentApiOverloadTests
{
    private static readonly NullabilityInfoContext Nullability = new();

    [Fact]
    public void PublicApis_ExposeOnlyMinimalExplicitNonOptionalOverloads()
    {
        AssertExperimentRunSurface(typeof(ILangfuseExperimentRun), expectAbstract: true);
        AssertBeginExperimentRunSurface(typeof(ILangfuseClient), expectAbstract: true);
        AssertExperimentFactoryExtensionSurfaces();

        foreach (var implementationType in new[]
        {
            typeof(LangfuseExperimentRun),
            typeof(DisabledLangfuseExperimentRun),
        })
        {
            AssertExperimentRunSurface(implementationType, expectAbstract: false);
        }

        foreach (var implementationType in new[]
        {
            typeof(LangfuseClient),
            typeof(DisabledLangfuseClient),
            typeof(LangfuseSession),
            typeof(DisabledLangfuseSession),
        })
        {
            AssertBeginExperimentRunSurface(implementationType, expectAbstract: false);
        }
    }

    [Fact]
    public void ExplicitExperimentFactoryOptions_RejectNull()
    {
        var client = new DisabledLangfuseClient();
        var run = client.BeginExperimentRun("dataset", "run");

        Assert.Throws<ArgumentNullException>(
            () => client.CreateExperimentItemScopeProvider<int, string>(
                run,
                null!));
        Assert.Throws<ArgumentNullException>(
            () => client.CreateLocalExperimentItemScopeProvider<int, string>(
                null!));
        Assert.Throws<ArgumentNullException>(
            () => client.CreateExperimentResultSink<int, string>(
                run,
                null!));
        Assert.Throws<ArgumentNullException>(
            () => client.CreateLocalExperimentResultSink<int, string>(
                null!));
    }

    [Fact]
    public async Task TokenlessExperimentRunOverloads_UseDefaultOptionsAndNoCancellation()
    {
        var run = new DisabledLangfuseClient().BeginExperimentRun("dataset", "run");
        CancellationToken? callbackToken = null;

#pragma warning disable xUnit1051 // This test intentionally exercises the tokenless experiment-run overloads.
        var item = await run.RunItemAsync(
            "item",
            (_, cancellationToken) =>
            {
                callbackToken = cancellationToken;
                return Task.FromResult("value");
            });
        var numeric = await run.RecordScoreAsync("numeric", 0.5);
        var boolean = await run.RecordScoreAsync("boolean", true);
        var categorical = await run.RecordScoreAsync("categorical", "good");
        var evaluation = await run.RecordEvaluationAsync(
            new EvaluationResult(new NumericMetric("quality", 1)));
#pragma warning restore xUnit1051

        Assert.Equal(CancellationToken.None, callbackToken);
        Assert.Equal("value", item.Value);
        Assert.Equal(LangfuseExperimentItemLinkStatus.Disabled, item.Link.Status);
        Assert.All(
            new[] { numeric, boolean, categorical },
            result => Assert.Equal(LangfuseExperimentScoreStatus.Disabled, result.Status));
        Assert.Equal(
            LangfuseExperimentScoreStatus.Disabled,
            Assert.Single(evaluation).Status);
        Assert.Null(run.Description);
        Assert.Null(run.DatasetVersion);
        Assert.Null(run.Metadata);
    }

    private static MethodInfo[] GetDeclaredMethods(
        Type type,
        string name,
        BindingFlags methodKind = BindingFlags.Instance) =>
        type.GetMethods(methodKind | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == name)
            .OrderBy(method => method.GetParameters().Length)
            .ToArray();

    private static void AssertExperimentRunSurface(Type type, bool expectAbstract)
    {
        var runItemMethods = GetDeclaredMethods(
            type,
            nameof(ILangfuseExperimentRun.RunItemAsync));
        Assert.Equal(2, runItemMethods.Length);
        AssertRunItemMethod(runItemMethods[0], expectAbstract, includeOptions: false);
        AssertRunItemMethod(runItemMethods[1], expectAbstract, includeOptions: true);

        var scoreMethods = GetDeclaredMethods(
            type,
            nameof(ILangfuseExperimentRun.RecordScoreAsync));
        Assert.Equal(6, scoreMethods.Length);
        foreach (var valueType in new[] { typeof(double), typeof(bool), typeof(string) })
        {
            var valueMethods = scoreMethods
                .Where(method => method.GetParameters()[1].ParameterType == valueType)
                .ToArray();
            Assert.Equal(2, valueMethods.Length);
            AssertScoreMethod(valueMethods[0], expectAbstract, valueType, includeOptions: false);
            AssertScoreMethod(valueMethods[1], expectAbstract, valueType, includeOptions: true);
        }

        var evaluationMethods = GetDeclaredMethods(
            type,
            nameof(ILangfuseExperimentRun.RecordEvaluationAsync));
        Assert.Equal(2, evaluationMethods.Length);
        AssertEvaluationMethod(evaluationMethods[0], expectAbstract, includeOptions: false);
        AssertEvaluationMethod(evaluationMethods[1], expectAbstract, includeOptions: true);

        AssertNoOptionalParameters(
            runItemMethods.Concat(scoreMethods).Concat(evaluationMethods));
    }

    private static void AssertBeginExperimentRunSurface(Type type, bool expectAbstract)
    {
        var methods = GetDeclaredMethods(
            type,
            nameof(ILangfuseClient.BeginExperimentRun));
        Assert.Equal(2, methods.Length);

        Assert.False(methods[0].IsGenericMethod);
        AssertMethod(methods[0], expectAbstract, typeof(ILangfuseExperimentRun), typeof(string), typeof(string));
        Assert.False(methods[1].IsGenericMethod);
        AssertMethod(
            methods[1],
            expectAbstract,
            typeof(ILangfuseExperimentRun),
            typeof(string),
            typeof(string),
            typeof(LangfuseExperimentRunOptions));
        AssertNullable(methods[1].GetParameters()[2]);
        AssertNoOptionalParameters(methods);
    }

    private static void AssertExperimentFactoryExtensionSurfaces()
    {
        AssertFactoryExtensionPair(
            typeof(LangfuseExperimentItemScopeExtensions),
            nameof(LangfuseExperimentItemScopeExtensions.CreateExperimentItemScopeProvider),
            includeRun: true,
            typeof(LangfuseExperimentItemScopeProvider<,>),
            typeof(LangfuseExperimentItemScopeOptions<>));
        AssertFactoryExtensionPair(
            typeof(LangfuseExperimentItemScopeExtensions),
            nameof(LangfuseExperimentItemScopeExtensions.CreateLocalExperimentItemScopeProvider),
            includeRun: false,
            typeof(LangfuseExperimentItemScopeProvider<,>),
            typeof(LangfuseExperimentItemScopeOptions<>));
        AssertFactoryExtensionPair(
            typeof(LangfuseExperimentResultSinkExtensions),
            nameof(LangfuseExperimentResultSinkExtensions.CreateExperimentResultSink),
            includeRun: true,
            typeof(LangfuseExperimentResultSink<,>),
            typeof(LangfuseExperimentResultSinkOptions<,>));
        AssertFactoryExtensionPair(
            typeof(LangfuseExperimentResultSinkExtensions),
            nameof(LangfuseExperimentResultSinkExtensions.CreateLocalExperimentResultSink),
            includeRun: false,
            typeof(LangfuseExperimentResultSink<,>),
            typeof(LangfuseExperimentResultSinkOptions<,>));
    }

    private static void AssertFactoryExtensionPair(
        Type extensionType,
        string methodName,
        bool includeRun,
        Type returnTypeDefinition,
        Type optionsTypeDefinition)
    {
        var methods = GetDeclaredMethods(
            extensionType,
            methodName,
            BindingFlags.Static);
        Assert.Equal(2, methods.Length);
        Assert.All(methods, method => Assert.True(method.IsGenericMethodDefinition));

        var requiredOnly = methods[0].MakeGenericMethod(typeof(int), typeof(string));
        var explicitOptions = methods[1].MakeGenericMethod(typeof(int), typeof(string));
        var returnType = returnTypeDefinition.MakeGenericType(typeof(int), typeof(string));
        var optionsType = optionsTypeDefinition.GetGenericArguments().Length == 1
            ? optionsTypeDefinition.MakeGenericType(typeof(int))
            : optionsTypeDefinition.MakeGenericType(typeof(int), typeof(string));
        var requiredParameters = includeRun
            ? new[] { typeof(ILangfuseClient), typeof(ILangfuseExperimentRun) }
            : [typeof(ILangfuseClient)];

        AssertMethod(
            requiredOnly,
            expectAbstract: false,
            returnType,
            requiredParameters);
        AssertMethod(
            explicitOptions,
            expectAbstract: false,
            returnType,
            requiredParameters.Append(optionsType).ToArray());
        AssertNotNullable(methods[1].GetParameters()[^1]);
        AssertNoOptionalParameters(methods);
    }

    private static void AssertRunItemMethod(
        MethodInfo method,
        bool expectAbstract,
        bool includeOptions)
    {
        Assert.Equal(expectAbstract, method.IsAbstract);
        Assert.True(method.IsGenericMethodDefinition);
        var genericArgument = Assert.Single(method.GetGenericArguments());
        var callbackType = typeof(Func<,,>).MakeGenericType(
            typeof(ILangfuseScenario),
            typeof(CancellationToken),
            typeof(Task<>).MakeGenericType(genericArgument));
        var returnType = typeof(Task<>).MakeGenericType(
            typeof(LangfuseExperimentItemResult<>).MakeGenericType(genericArgument));
        var expectedParameterTypes = includeOptions
            ? new[]
            {
                typeof(string),
                callbackType,
                typeof(LangfuseExperimentItemOptions),
                typeof(CancellationToken),
            }
            : [typeof(string), callbackType];
        AssertMethod(method, expectAbstract, returnType, expectedParameterTypes);
        if (includeOptions)
        {
            AssertNullable(method.GetParameters()[2]);
        }
    }

    private static void AssertScoreMethod(
        MethodInfo method,
        bool expectAbstract,
        Type valueType,
        bool includeOptions)
    {
        Assert.False(method.IsGenericMethod);
        var expectedParameterTypes = includeOptions
            ? new[]
            {
                typeof(string),
                valueType,
                typeof(LangfuseScoreOptions),
                typeof(CancellationToken),
            }
            : [typeof(string), valueType];
        AssertMethod(
            method,
            expectAbstract,
            typeof(Task<LangfuseExperimentRunScoreResult>),
            expectedParameterTypes);
        if (includeOptions)
        {
            AssertNullable(method.GetParameters()[2]);
        }
    }

    private static void AssertEvaluationMethod(
        MethodInfo method,
        bool expectAbstract,
        bool includeOptions)
    {
        Assert.False(method.IsGenericMethod);
        var expectedParameterTypes = includeOptions
            ? new[]
            {
                typeof(EvaluationResult),
                typeof(LangfuseEvaluationScoreOptions),
                typeof(CancellationToken),
            }
            : [typeof(EvaluationResult)];
        AssertMethod(
            method,
            expectAbstract,
            typeof(Task<IReadOnlyList<LangfuseExperimentRunScoreResult>>),
            expectedParameterTypes);
        if (includeOptions)
        {
            AssertNullable(method.GetParameters()[1]);
        }
    }

    private static void AssertMethod(
        MethodInfo method,
        bool expectAbstract,
        Type returnType,
        params Type[] parameterTypes)
    {
        Assert.Equal(expectAbstract, method.IsAbstract);
        Assert.Equal(returnType, method.ReturnType);
        Assert.Equal(
            parameterTypes,
            method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }

    private static void AssertNullable(ParameterInfo parameter) =>
        Assert.Equal(NullabilityState.Nullable, Nullability.Create(parameter).ReadState);

    private static void AssertNotNullable(ParameterInfo parameter) =>
        Assert.Equal(NullabilityState.NotNull, Nullability.Create(parameter).ReadState);

    private static void AssertNoOptionalParameters(IEnumerable<MethodInfo> methods) =>
        Assert.DoesNotContain(
            methods.SelectMany(method => method.GetParameters()),
            parameter => parameter.IsOptional);
}
