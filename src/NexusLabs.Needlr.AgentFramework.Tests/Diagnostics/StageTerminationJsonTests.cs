using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Tests.Diagnostics;

/// <summary>
/// Tests for polymorphic JSON round-trip on <see cref="StageTermination"/>. Locks in
/// the wire format: <c>$kind</c> discriminator + case-name discriminator values.
/// Once shipped, this contract is part of the public API surface.
/// </summary>
public sealed class StageTerminationJsonTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
    };

    [Fact]
    public void Completed_RoundTrips()
    {
        StageTermination original = new StageTermination.Completed();
        var json = JsonSerializer.Serialize(original, Options);
        Assert.Contains("\"$kind\":\"Completed\"", json);

        var roundTripped = JsonSerializer.Deserialize<StageTermination>(json, Options);
        Assert.IsType<StageTermination.Completed>(roundTripped);
    }

    [Fact]
    public void NaturalCompletion_RoundTrips()
    {
        StageTermination original = new StageTermination.NaturalCompletion();
        var json = JsonSerializer.Serialize(original, Options);
        Assert.Contains("\"$kind\":\"NaturalCompletion\"", json);

        var roundTripped = JsonSerializer.Deserialize<StageTermination>(json, Options);
        Assert.IsType<StageTermination.NaturalCompletion>(roundTripped);
    }

    [Fact]
    public void CompletedEarlyAfterToolCall_RoundTrips()
    {
        StageTermination original = new StageTermination.CompletedEarlyAfterToolCall();
        var json = JsonSerializer.Serialize(original, Options);
        Assert.Contains("\"$kind\":\"CompletedEarlyAfterToolCall\"", json);

        var roundTripped = JsonSerializer.Deserialize<StageTermination>(json, Options);
        Assert.IsType<StageTermination.CompletedEarlyAfterToolCall>(roundTripped);
    }

    [Fact]
    public void MaxIterationsReached_RoundTrips_PreservesValues()
    {
        StageTermination original = new StageTermination.MaxIterationsReached(Limit: 10, IterationsUsed: 7);
        var json = JsonSerializer.Serialize(original, Options);
        Assert.Contains("\"$kind\":\"MaxIterationsReached\"", json);

        var roundTripped = JsonSerializer.Deserialize<StageTermination>(json, Options);
        var typed = Assert.IsType<StageTermination.MaxIterationsReached>(roundTripped);
        Assert.Equal(10, typed.Limit);
        Assert.Equal(7, typed.IterationsUsed);
    }

    [Fact]
    public void MaxToolCallsReached_RoundTrips_PreservesValues()
    {
        StageTermination original = new StageTermination.MaxToolCallsReached(Limit: 50, ToolCallsUsed: 53);
        var json = JsonSerializer.Serialize(original, Options);
        Assert.Contains("\"$kind\":\"MaxToolCallsReached\"", json);

        var roundTripped = JsonSerializer.Deserialize<StageTermination>(json, Options);
        var typed = Assert.IsType<StageTermination.MaxToolCallsReached>(roundTripped);
        Assert.Equal(50, typed.Limit);
        Assert.Equal(53, typed.ToolCallsUsed);
    }

    [Fact]
    public void BudgetPressure_RoundTrips_PreservesThreshold()
    {
        StageTermination original = new StageTermination.BudgetPressure(Threshold: 0.85);
        var json = JsonSerializer.Serialize(original, Options);
        Assert.Contains("\"$kind\":\"BudgetPressure\"", json);

        var roundTripped = JsonSerializer.Deserialize<StageTermination>(json, Options);
        var typed = Assert.IsType<StageTermination.BudgetPressure>(roundTripped);
        Assert.Equal(0.85, typed.Threshold);
    }

    [Fact]
    public void BudgetPressure_NullThreshold_RoundTrips()
    {
        StageTermination original = new StageTermination.BudgetPressure(Threshold: null);
        var json = JsonSerializer.Serialize(original, Options);

        var roundTripped = JsonSerializer.Deserialize<StageTermination>(json, Options);
        var typed = Assert.IsType<StageTermination.BudgetPressure>(roundTripped);
        Assert.Null(typed.Threshold);
    }

    [Fact]
    public void StallDetected_RoundTrips_PreservesConsecutiveThreshold()
    {
        StageTermination original = new StageTermination.StallDetected(ConsecutiveThreshold: 3);
        var json = JsonSerializer.Serialize(original, Options);
        Assert.Contains("\"$kind\":\"StallDetected\"", json);

        var roundTripped = JsonSerializer.Deserialize<StageTermination>(json, Options);
        var typed = Assert.IsType<StageTermination.StallDetected>(roundTripped);
        Assert.Equal(3, typed.ConsecutiveThreshold);
    }

    [Fact]
    public void Cancelled_RoundTrips()
    {
        StageTermination original = new StageTermination.Cancelled();
        var json = JsonSerializer.Serialize(original, Options);
        Assert.Contains("\"$kind\":\"Cancelled\"", json);

        var roundTripped = JsonSerializer.Deserialize<StageTermination>(json, Options);
        Assert.IsType<StageTermination.Cancelled>(roundTripped);
    }

    /// <summary>
    /// Failed serializes the discriminator but the Exception payload does NOT
    /// round-trip cleanly through System.Text.Json — the framework's default Exception
    /// converter writes the Message but reconstructs as a generic Exception (or fails).
    /// We assert only the discriminator + serialization behavior here; consumers
    /// needing exception fidelity should serialize the message + type name separately.
    /// </summary>
    [Fact]
    public void Failed_SerializesDiscriminator()
    {
        StageTermination original = new StageTermination.Failed(new InvalidOperationException("boom"));
        var json = JsonSerializer.Serialize(original, Options);
        Assert.Contains("\"$kind\":\"Failed\"", json);
    }

    [Fact]
    public void Skipped_RoundTrips_PreservesReason()
    {
        StageTermination original = new StageTermination.Skipped("no work to do");
        var json = JsonSerializer.Serialize(original, Options);
        Assert.Contains("\"$kind\":\"Skipped\"", json);

        var roundTripped = JsonSerializer.Deserialize<StageTermination>(json, Options);
        var typed = Assert.IsType<StageTermination.Skipped>(roundTripped);
        Assert.Equal("no work to do", typed.Reason);
    }

    [Fact]
    public void Skipped_NullReason_RoundTrips()
    {
        StageTermination original = new StageTermination.Skipped();
        var json = JsonSerializer.Serialize(original, Options);

        var roundTripped = JsonSerializer.Deserialize<StageTermination>(json, Options);
        var typed = Assert.IsType<StageTermination.Skipped>(roundTripped);
        Assert.Null(typed.Reason);
    }

    [Fact]
    public void Custom_RoundTrips_PreservesReason()
    {
        StageTermination original = new StageTermination.Custom("Reconciled");
        var json = JsonSerializer.Serialize(original, Options);
        Assert.Contains("\"$kind\":\"Custom\"", json);

        var roundTripped = JsonSerializer.Deserialize<StageTermination>(json, Options);
        var typed = Assert.IsType<StageTermination.Custom>(roundTripped);
        Assert.Equal("Reconciled", typed.Reason);
    }

    /// <summary>
    /// Documents the documented limitation: <see cref="StageTermination.Custom.Properties"/>
    /// values are typed as <see cref="object"/> in C#, but JSON round-trip yields
    /// <see cref="JsonElement"/> values rather than the original concrete types
    /// (<c>int</c>, <c>string</c>, <c>bool</c>, etc.). Consumers needing type-safe
    /// properties should deserialize each value individually via
    /// <c>JsonElement.Deserialize&lt;T&gt;()</c>.
    /// </summary>
    [Fact]
    public void Custom_Properties_RoundTripYieldsJsonElement()
    {
        var props = new Dictionary<string, object?>
        {
            ["FindingCount"] = 4,
            ["Severity"] = "warn",
            ["IsTrue"] = true,
        };
        StageTermination original = new StageTermination.Custom("Reconciled", props);
        var json = JsonSerializer.Serialize(original, Options);

        var roundTripped = JsonSerializer.Deserialize<StageTermination>(json, Options);
        var typed = Assert.IsType<StageTermination.Custom>(roundTripped);
        Assert.NotNull(typed.Properties);
        Assert.Equal(3, typed.Properties!.Count);

        var findingCount = Assert.IsType<JsonElement>(typed.Properties["FindingCount"]);
        Assert.Equal(4, findingCount.GetInt32());

        var severity = Assert.IsType<JsonElement>(typed.Properties["Severity"]);
        Assert.Equal("warn", severity.GetString());

        var isTrue = Assert.IsType<JsonElement>(typed.Properties["IsTrue"]);
        Assert.True(isTrue.GetBoolean());
    }

    /// <summary>
    /// Bulletproof guarantee — every public sealed nested record under
    /// <see cref="StageTermination"/> MUST be registered in the
    /// <see cref="IStageTermination"/> polymorphism table. Adding a new framework
    /// case without also adding the corresponding <c>[JsonDerivedType]</c> entry
    /// fails this test loudly. Catches the design risk we accepted when we made
    /// the JSON registration a per-attribute additive contract — if the framework
    /// ever forgets to register a new case, every consumer's pipeline-result
    /// JSON serialization breaks, and this test is the canary.
    /// </summary>
    [Fact]
    public void IStageTermination_PolymorphismRegistry_CoversEveryFrameworkCase()
    {
        var frameworkCases = typeof(StageTermination)
            .GetNestedTypes(System.Reflection.BindingFlags.Public)
            .Where(t => t.IsSealed && typeof(StageTermination).IsAssignableFrom(t))
            .ToList();

        Assert.NotEmpty(frameworkCases);

        var registeredTypes = typeof(IStageTermination)
            .GetCustomAttributes(typeof(JsonDerivedTypeAttribute), inherit: false)
            .Cast<JsonDerivedTypeAttribute>()
            .Select(a => a.DerivedType)
            .ToHashSet();

        foreach (var frameworkCase in frameworkCases)
        {
            Assert.True(
                registeredTypes.Contains(frameworkCase),
                $"Framework StageTermination case '{frameworkCase.Name}' is missing a [JsonDerivedType] entry on IStageTermination. " +
                $"Add: [JsonDerivedType(typeof(StageTermination.{frameworkCase.Name}), nameof(StageTermination.{frameworkCase.Name}))] " +
                "to IStageTermination.cs.");
        }

        var staleRegistrations = registeredTypes
            .Where(t => !frameworkCases.Contains(t))
            .ToList();
        Assert.Empty(staleRegistrations);
    }

    /// <summary>
    /// Round-trips each framework case through the IStageTermination interface
    /// declared type (which is what consumers actually serialize when
    /// <see cref="IAgentStageResult.Termination"/> is the source). This complements
    /// the abstract-record-typed round trip tests above and proves the polymorphism
    /// registry on the interface is wired correctly for the runtime declared type
    /// callers will see in the wild.
    /// </summary>
    [Theory]
    [InlineData(typeof(StageTermination.Completed))]
    [InlineData(typeof(StageTermination.NaturalCompletion))]
    [InlineData(typeof(StageTermination.CompletedEarlyAfterToolCall))]
    [InlineData(typeof(StageTermination.Cancelled))]
    public void IStageTermination_RoundTrips_ParameterlessFrameworkCases(Type caseType)
    {
        IStageTermination original = (IStageTermination)Activator.CreateInstance(caseType)!;
        var json = JsonSerializer.Serialize(original, Options);
        Assert.Contains($"\"$kind\":\"{caseType.Name}\"", json);

        var roundTripped = JsonSerializer.Deserialize<IStageTermination>(json, Options);
        Assert.IsType(caseType, roundTripped);
    }

    [Fact]
    public void IStageTermination_RoundTrips_MaxIterationsReached_PreservesValues()
    {
        IStageTermination original = new StageTermination.MaxIterationsReached(Limit: 10, IterationsUsed: 7);
        var json = JsonSerializer.Serialize(original, Options);
        Assert.Contains("\"$kind\":\"MaxIterationsReached\"", json);

        var roundTripped = JsonSerializer.Deserialize<IStageTermination>(json, Options);
        var typed = Assert.IsType<StageTermination.MaxIterationsReached>(roundTripped);
        Assert.Equal(10, typed.Limit);
        Assert.Equal(7, typed.IterationsUsed);
    }

    /// <summary>
    /// Locks down the documented third-party JSON contract: a consumer-defined
    /// <see cref="IStageTermination"/> impl, registered via a
    /// <see cref="System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver"/>
    /// modifier, round-trips cleanly with the same <c>$kind</c> discriminator
    /// scheme as the framework cases. This is the contract
    /// <c>docs/stage-termination.md</c> describes and
    /// <c>src/Examples/AgentFramework/RfcPipelineApp</c> demonstrates.
    /// </summary>
    [Fact]
    public void IStageTermination_ThirdPartyTypeViaJsonTypeInfoResolverModifier_RoundTrips()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    info =>
                    {
                        if (info.Type == typeof(IStageTermination)
                            && info.PolymorphismOptions is { } poly)
                        {
                            poly.DerivedTypes.Add(new JsonDerivedType(
                                typeof(ConsumerDefinedTermination), "ConsumerDefinedTermination"));
                        }
                    },
                },
            },
        };

        IStageTermination original = new ConsumerDefinedTermination(Detail: "approved", Score: 42);

        var json = JsonSerializer.Serialize(original, options);
        Assert.Contains("\"$kind\":\"ConsumerDefinedTermination\"", json);

        var roundTripped = JsonSerializer.Deserialize<IStageTermination>(json, options);
        var typed = Assert.IsType<ConsumerDefinedTermination>(roundTripped);
        Assert.Equal("approved", typed.Detail);
        Assert.Equal(42, typed.Score);
    }

    /// <summary>
    /// Without the JsonTypeInfoResolver modifier, serializing a third-party
    /// IStageTermination instance against the canonical interface declared type
    /// throws NotSupportedException — same loud failure the framework cases have
    /// for unregistered types. Documents the consequence consumers face if they
    /// implement IStageTermination but forget to register their type.
    /// </summary>
    [Fact]
    public void IStageTermination_ThirdPartyTypeWithoutRegistration_ThrowsNotSupported()
    {
        IStageTermination unregistered = new ConsumerDefinedTermination(Detail: "x", Score: 0);

        Assert.Throws<NotSupportedException>(() =>
            JsonSerializer.Serialize(unregistered, Options));
    }

    /// <summary>
    /// Stand-in for any consumer-defined typed termination case. Implements
    /// <see cref="IStageTermination"/> directly (does NOT inherit from the
    /// framework's closed <see cref="StageTermination"/> hierarchy).
    /// </summary>
    private sealed record ConsumerDefinedTermination(string Detail, int Score) : IStageTermination
    {
        public string ToTagValue() => $"ConsumerDefinedTermination:{Detail}";
    }
}
