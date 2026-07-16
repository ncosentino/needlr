using System.Reflection;
using System.Runtime.CompilerServices;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Reporting.Tests;

public sealed class ReportingAutoRegistrationConventionTests
{
    private const BindingFlags RecordPropertyFlags =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void ConcreteClasses_OptOutOfAutomaticRegistration()
    {
        var unsafeTypes = GetConcreteTypes()
            .Where(type =>
                !IsRecord(type)
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
        typeof(MeaiReportingExperimentItem).Assembly
            .GetTypes()
            .Where(type =>
                type.IsClass
                && !type.IsAbstract
                && !type.IsNested
                && type.Namespace?.StartsWith(
                    "NexusLabs.Needlr.AgentFramework.Evaluation.Reporting",
                    StringComparison.Ordinal) == true
                && !typeof(Delegate).IsAssignableFrom(type)
                && !typeof(Exception).IsAssignableFrom(type));
}
