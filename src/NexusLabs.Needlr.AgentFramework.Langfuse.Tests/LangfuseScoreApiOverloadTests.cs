using System.Reflection;

using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseScoreApiOverloadTests
{
    private static readonly NullabilityInfoContext Nullability = new();

    [Fact]
    public void PublicScoreApis_ExposeOnlyExactExplicitNonOptionalOverloads()
    {
        AssertScoreClientSurface(typeof(ILangfuseScoreClient), expectAbstract: true);
        AssertScenarioSurface(typeof(ILangfuseScenario), expectAbstract: true);

        foreach (var implementationType in new[]
        {
            typeof(LangfuseScoreClient),
            typeof(DisabledLangfuseScoreClient),
        })
        {
            AssertScoreClientSurface(implementationType, expectAbstract: false);
        }

        foreach (var implementationType in new[]
        {
            typeof(LangfuseScenario),
            typeof(DisabledLangfuseScenario),
        })
        {
            AssertScenarioSurface(implementationType, expectAbstract: false);
        }

        AssertRecordLangfuseScoresSurface();
    }

    private static MethodInfo[] GetDeclaredMethods(
        Type type,
        string name,
        BindingFlags methodKind = BindingFlags.Instance) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.DeclaredOnly | methodKind)
            .Where(method => method.Name == name)
            .OrderBy(method => method.GetParameters().Length)
            .ToArray();

    private static void AssertScoreClientSurface(Type type, bool expectAbstract)
    {
        var traceScoreMethods = GetDeclaredMethods(
            type,
            nameof(ILangfuseScoreClient.RecordScoreAsync));
        AssertTypedScoreFamily(
            traceScoreMethods,
            expectAbstract,
            [typeof(string), typeof(string)]);

        var evaluationMethods = GetDeclaredMethods(
            type,
            nameof(ILangfuseScoreClient.RecordEvaluationAsync));
        AssertEvaluationFamily(
            evaluationMethods,
            expectAbstract,
            [typeof(string), typeof(EvaluationResult)]);

        var observationScoreMethods = GetDeclaredMethods(
            type,
            nameof(ILangfuseScoreClient.RecordObservationScoreAsync));
        AssertTypedScoreFamily(
            observationScoreMethods,
            expectAbstract,
            [typeof(string), typeof(string), typeof(string)]);

        var sessionScoreMethods = GetDeclaredMethods(
            type,
            nameof(ILangfuseScoreClient.RecordSessionScoreAsync));
        AssertTypedScoreFamily(
            sessionScoreMethods,
            expectAbstract,
            [typeof(string), typeof(string)]);

        AssertNoOptionalParameters(
            traceScoreMethods
                .Concat(evaluationMethods)
                .Concat(observationScoreMethods)
                .Concat(sessionScoreMethods));
    }

    private static void AssertScenarioSurface(Type type, bool expectAbstract)
    {
        var traceScoreMethods = GetDeclaredMethods(
            type,
            nameof(ILangfuseScenario.RecordScoreAsync));
        AssertTypedScoreFamily(
            traceScoreMethods,
            expectAbstract,
            [typeof(string)]);

        var evaluationMethods = GetDeclaredMethods(
            type,
            nameof(ILangfuseScenario.RecordEvaluationAsync));
        AssertEvaluationFamily(
            evaluationMethods,
            expectAbstract,
            [typeof(EvaluationResult)]);

        var sessionScoreMethods = GetDeclaredMethods(
            type,
            nameof(ILangfuseScenario.RecordSessionScoreAsync));
        AssertTypedScoreFamily(
            sessionScoreMethods,
            expectAbstract,
            [typeof(string)]);

        AssertNoOptionalParameters(
            traceScoreMethods
                .Concat(evaluationMethods)
                .Concat(sessionScoreMethods));
    }

    private static void AssertRecordLangfuseScoresSurface()
    {
        var methods = GetDeclaredMethods(
            typeof(LangfuseEvaluationScoreExtensions),
            nameof(LangfuseEvaluationScoreExtensions.RecordLangfuseScoresAsync),
            BindingFlags.Static);
        Assert.Equal(2, methods.Length);

        AssertMethod(
            methods[0],
            expectAbstract: false,
            typeof(Task),
            typeof(EvaluationResult),
            typeof(ILangfuseScenario));
        AssertMethod(
            methods[1],
            expectAbstract: false,
            typeof(Task),
            typeof(EvaluationResult),
            typeof(ILangfuseScenario),
            typeof(LangfuseEvaluationScoreOptions),
            typeof(CancellationToken));
        Assert.All(methods, method => Assert.True(method.IsStatic));
        AssertNullable(methods[1].GetParameters()[2]);
        AssertNoOptionalParameters(methods);
    }

    private static void AssertTypedScoreFamily(
        MethodInfo[] methods,
        bool expectAbstract,
        Type[] requiredPrefix)
    {
        Assert.Equal(6, methods.Length);
        foreach (var valueType in new[] { typeof(double), typeof(bool), typeof(string) })
        {
            var valueIndex = requiredPrefix.Length;
            var valueMethods = methods
                .Where(method => method.GetParameters()[valueIndex].ParameterType == valueType)
                .OrderBy(method => method.GetParameters().Length)
                .ToArray();
            Assert.Equal(2, valueMethods.Length);

            var requiredParameters = requiredPrefix
                .Append(valueType)
                .ToArray();
            AssertMethod(
                valueMethods[0],
                expectAbstract,
                typeof(Task),
                requiredParameters);

            var canonicalParameters = requiredParameters
                .Append(typeof(LangfuseScoreOptions))
                .Append(typeof(CancellationToken))
                .ToArray();
            AssertMethod(
                valueMethods[1],
                expectAbstract,
                typeof(Task),
                canonicalParameters);
            AssertNullable(valueMethods[1].GetParameters()[requiredParameters.Length]);
        }
    }

    private static void AssertEvaluationFamily(
        MethodInfo[] methods,
        bool expectAbstract,
        Type[] requiredParameters)
    {
        Assert.Equal(2, methods.Length);
        AssertMethod(
            methods[0],
            expectAbstract,
            typeof(Task),
            requiredParameters);

        var canonicalParameters = requiredParameters
            .Append(typeof(LangfuseEvaluationScoreOptions))
            .Append(typeof(CancellationToken))
            .ToArray();
        AssertMethod(
            methods[1],
            expectAbstract,
            typeof(Task),
            canonicalParameters);
        AssertNullable(methods[1].GetParameters()[requiredParameters.Length]);
    }

    private static void AssertMethod(
        MethodInfo method,
        bool expectAbstract,
        Type returnType,
        params Type[] parameterTypes)
    {
        Assert.Equal(expectAbstract, method.IsAbstract);
        Assert.False(method.IsGenericMethod);
        Assert.Equal(returnType, method.ReturnType);
        Assert.Equal(
            parameterTypes,
            method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }

    private static void AssertNullable(ParameterInfo parameter) =>
        Assert.Equal(NullabilityState.Nullable, Nullability.Create(parameter).ReadState);

    private static void AssertNoOptionalParameters(IEnumerable<MethodInfo> methods) =>
        Assert.DoesNotContain(
            methods.SelectMany(method => method.GetParameters()),
            parameter => parameter.IsOptional);
}
