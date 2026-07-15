using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class EvaluationAutoRegistrationConventionTests
{
    private const BindingFlags RecordPropertyFlags =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void ConcreteNonServiceClasses_OptOutOfAutomaticRegistration()
    {
        var unsafeTypes = GetConcreteTypes()
            .Where(type =>
                !IsRecord(type)
                && !IsIntendedService(type)
                && !type.IsDefined(typeof(DoNotAutoRegisterAttribute), inherit: true))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(unsafeTypes);
    }

    [Fact]
    public void RequiredOrInitOnlyDataStyleClasses_AreRecords()
    {
        var violations = GetConcreteTypes()
            .Where(type => !IsRecord(type) && HasRequiredOrInitOnlyProperties(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static bool IsRecord(Type type) =>
        type.GetProperty("EqualityContract", RecordPropertyFlags) is not null;

    private static bool IsIntendedService(Type type) =>
        typeof(IEvaluator).IsAssignableFrom(type)
        || typeof(IChatClient).IsAssignableFrom(type)
        || typeof(IEvaluationCaptureStore).IsAssignableFrom(type)
        || typeof(IExperimentRunner).IsAssignableFrom(type);

    private static bool HasRequiredOrInitOnlyProperties(Type type) =>
        type.GetProperties(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly)
            .Any(property =>
                property.IsDefined(typeof(RequiredMemberAttribute), inherit: false)
                || property.SetMethod?.ReturnParameter
                    .GetRequiredCustomModifiers()
                    .Contains(typeof(IsExternalInit)) == true);

    private static IEnumerable<Type> GetConcreteTypes() =>
        typeof(EvaluationQualityGate).Assembly
            .GetTypes()
            .Where(type =>
                type.IsClass
                && !type.IsAbstract
                && !type.IsNested
                && type.Namespace?.StartsWith(
                    "NexusLabs.Needlr.AgentFramework.Evaluation",
                    StringComparison.Ordinal) == true
                && !typeof(Delegate).IsAssignableFrom(type)
                && !typeof(Exception).IsAssignableFrom(type));
}
