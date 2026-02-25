using System.Diagnostics.CodeAnalysis;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Runtime bootstrap registry for source-generated Agent Framework components.
/// </summary>
/// <remarks>
/// The source generator emits a <c>[ModuleInitializer]</c> in the host assembly that calls
/// <see cref="Register"/> with the generated type providers.
/// <c>UsingAgentFramework()</c> checks this bootstrap
/// and auto-populates function types, groups, and agent types without requiring any explicit
/// <c>Add*FromGenerated()</c> calls.
/// </remarks>
public static class AgentFrameworkGeneratedBootstrap
{
    private sealed class Registration
    {
        public Registration(
            Func<IReadOnlyList<Type>> functionTypes,
            Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> groupTypes,
            Func<IReadOnlyList<Type>> agentTypes,
            Func<IReadOnlyDictionary<Type, IReadOnlyList<(Type TargetType, string? HandoffReason)>>> handoffTopology,
            Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> groupChatGroups,
            Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> sequentialTopology)
        {
            FunctionTypes = functionTypes;
            GroupTypes = groupTypes;
            AgentTypes = agentTypes;
            HandoffTopology = handoffTopology;
            GroupChatGroups = groupChatGroups;
            SequentialTopology = sequentialTopology;
        }

        public Func<IReadOnlyList<Type>> FunctionTypes { get; }
        public Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> GroupTypes { get; }
        public Func<IReadOnlyList<Type>> AgentTypes { get; }
        public Func<IReadOnlyDictionary<Type, IReadOnlyList<(Type TargetType, string? HandoffReason)>>> HandoffTopology { get; }
        public Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> GroupChatGroups { get; }
        public Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> SequentialTopology { get; }
    }

    private static readonly object _gate = new();
    private static readonly List<Registration> _registrations = [];
    private static readonly AsyncLocal<Registration?> _asyncLocalOverride = new();

    private static (
        Func<IReadOnlyList<Type>> Functions,
        Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> Groups,
        Func<IReadOnlyList<Type>> Agents,
        Func<IReadOnlyDictionary<Type, IReadOnlyList<(Type TargetType, string? HandoffReason)>>> HandoffTopology,
        Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> GroupChatGroups,
        Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> SequentialTopology)? _cachedCombined;

    /// <summary>
    /// Registers the generated type providers for this assembly.
    /// Called automatically by the generator-emitted <c>[ModuleInitializer]</c>.
    /// </summary>
    public static void Register(
        Func<IReadOnlyList<Type>> functionTypes,
        Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> groupTypes,
        Func<IReadOnlyList<Type>> agentTypes,
        Func<IReadOnlyDictionary<Type, IReadOnlyList<(Type TargetType, string? HandoffReason)>>> handoffTopology,
        Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> groupChatGroups,
        Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>>? sequentialTopology = null)
    {
        ArgumentNullException.ThrowIfNull(functionTypes);
        ArgumentNullException.ThrowIfNull(groupTypes);
        ArgumentNullException.ThrowIfNull(agentTypes);
        ArgumentNullException.ThrowIfNull(handoffTopology);
        ArgumentNullException.ThrowIfNull(groupChatGroups);
        sequentialTopology ??= static () => new Dictionary<string, IReadOnlyList<Type>>();

        lock (_gate)
        {
            _registrations.Add(new Registration(functionTypes, groupTypes, agentTypes, handoffTopology, groupChatGroups, sequentialTopology));
            _cachedCombined = null;
        }
    }

    /// <summary>
    /// Gets the combined function type provider from all registered assemblies.
    /// </summary>
    public static bool TryGetFunctionTypes([NotNullWhen(true)] out Func<IReadOnlyList<Type>>? provider)
    {
        var local = _asyncLocalOverride.Value;
        if (local is not null)
        {
            provider = local.FunctionTypes;
            return true;
        }

        lock (_gate)
        {
            if (_registrations.Count == 0)
            {
                provider = null;
                return false;
            }

            EnsureCombined();
            provider = _cachedCombined!.Value.Functions;
            return true;
        }
    }

    /// <summary>
    /// Gets the combined function group provider from all registered assemblies.
    /// </summary>
    public static bool TryGetGroupTypes([NotNullWhen(true)] out Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>>? provider)
    {
        var local = _asyncLocalOverride.Value;
        if (local is not null)
        {
            provider = local.GroupTypes;
            return true;
        }

        lock (_gate)
        {
            if (_registrations.Count == 0)
            {
                provider = null;
                return false;
            }

            EnsureCombined();
            provider = _cachedCombined!.Value.Groups;
            return true;
        }
    }

    /// <summary>
    /// Gets the combined agent type provider from all registered assemblies.
    /// </summary>
    public static bool TryGetAgentTypes([NotNullWhen(true)] out Func<IReadOnlyList<Type>>? provider)
    {
        var local = _asyncLocalOverride.Value;
        if (local is not null)
        {
            provider = local.AgentTypes;
            return true;
        }

        lock (_gate)
        {
            if (_registrations.Count == 0)
            {
                provider = null;
                return false;
            }

            EnsureCombined();
            provider = _cachedCombined!.Value.Agents;
            return true;
        }
    }

    /// <summary>
    /// Gets the combined handoff topology provider from all registered assemblies.
    /// </summary>
    public static bool TryGetHandoffTopology(
        [NotNullWhen(true)] out Func<IReadOnlyDictionary<Type, IReadOnlyList<(Type TargetType, string? HandoffReason)>>>? provider)
    {
        var local = _asyncLocalOverride.Value;
        if (local is not null)
        {
            provider = local.HandoffTopology;
            return true;
        }

        lock (_gate)
        {
            if (_registrations.Count == 0)
            {
                provider = null;
                return false;
            }

            EnsureCombined();
            provider = _cachedCombined!.Value.HandoffTopology;
            return true;
        }
    }

    /// <summary>
    /// Gets the combined group chat groups provider from all registered assemblies.
    /// </summary>
    public static bool TryGetGroupChatGroups(
        [NotNullWhen(true)] out Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>>? provider)
    {
        var local = _asyncLocalOverride.Value;
        if (local is not null)
        {
            provider = local.GroupChatGroups;
            return true;
        }

        lock (_gate)
        {
            if (_registrations.Count == 0)
            {
                provider = null;
                return false;
            }

            EnsureCombined();
            provider = _cachedCombined!.Value.GroupChatGroups;
            return true;
        }
    }

    /// <summary>
    /// Gets the combined sequential topology provider from all registered assemblies.
    /// </summary>
    public static bool TryGetSequentialTopology(
        [NotNullWhen(true)] out Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>>? provider)
    {
        var local = _asyncLocalOverride.Value;
        if (local is not null)
        {
            provider = local.SequentialTopology;
            return true;
        }

        lock (_gate)
        {
            if (_registrations.Count == 0)
            {
                provider = null;
                return false;
            }

            EnsureCombined();
            provider = _cachedCombined!.Value.SequentialTopology;
            return true;
        }
    }

    /// <summary>
    /// Creates a test-scoped override that replaces bootstrap discovery for the current async context.
    /// Dispose the returned scope to restore the previous state.
    /// </summary>
    internal static IDisposable BeginTestScope(
        Func<IReadOnlyList<Type>> functionTypes,
        Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> groupTypes,
        Func<IReadOnlyList<Type>> agentTypes,
        Func<IReadOnlyDictionary<Type, IReadOnlyList<(Type TargetType, string? HandoffReason)>>>? handoffTopology = null,
        Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>>? groupChatGroups = null,
        Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>>? sequentialTopology = null)
    {
        handoffTopology ??= static () => new Dictionary<Type, IReadOnlyList<(Type, string?)>>();
        groupChatGroups ??= static () => new Dictionary<string, IReadOnlyList<Type>>();
        sequentialTopology ??= static () => new Dictionary<string, IReadOnlyList<Type>>();
        var prior = _asyncLocalOverride.Value;
        _asyncLocalOverride.Value = new Registration(functionTypes, groupTypes, agentTypes, handoffTopology, groupChatGroups, sequentialTopology);
        return new Scope(prior);
    }

    private static void EnsureCombined()
    {
        if (_cachedCombined.HasValue)
            return;

        var functionProviders = _registrations.Select(r => r.FunctionTypes).ToArray();
        var groupProviders = _registrations.Select(r => r.GroupTypes).ToArray();
        var agentProviders = _registrations.Select(r => r.AgentTypes).ToArray();
        var topologyProviders = _registrations.Select(r => r.HandoffTopology).ToArray();
        var groupChatProviders = _registrations.Select(r => r.GroupChatGroups).ToArray();
        var sequentialProviders = _registrations.Select(r => r.SequentialTopology).ToArray();

        Func<IReadOnlyList<Type>> combinedFunctions = () =>
        {
            var result = new List<Type>();
            var seen = new HashSet<Type>();
            foreach (var p in functionProviders)
                foreach (var t in p())
                    if (seen.Add(t))
                        result.Add(t);
            return result;
        };

        Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> combinedGroups = () =>
        {
            var merged = new Dictionary<string, List<Type>>();
            foreach (var p in groupProviders)
                foreach (var (key, types) in p())
                {
                    if (!merged.TryGetValue(key, out var list))
                        merged[key] = list = [];
                    foreach (var t in types)
                        if (!list.Contains(t))
                            list.Add(t);
                }
            return merged.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<Type>)kv.Value.AsReadOnly());
        };

        Func<IReadOnlyList<Type>> combinedAgents = () =>
        {
            var result = new List<Type>();
            var seen = new HashSet<Type>();
            foreach (var p in agentProviders)
                foreach (var t in p())
                    if (seen.Add(t))
                        result.Add(t);
            return result;
        };

        Func<IReadOnlyDictionary<Type, IReadOnlyList<(Type TargetType, string? HandoffReason)>>> combinedTopology = () =>
        {
            var merged = new Dictionary<Type, List<(Type, string?)>>();
            foreach (var p in topologyProviders)
                foreach (var (agentType, targets) in p())
                {
                    if (!merged.TryGetValue(agentType, out var list))
                        merged[agentType] = list = [];
                    foreach (var target in targets)
                        if (!list.Contains(target))
                            list.Add(target);
                }
            return merged.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<(Type, string?)>)kv.Value.AsReadOnly());
        };

        Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> combinedGroupChatGroups = () =>
        {
            var merged = new Dictionary<string, List<Type>>();
            foreach (var p in groupChatProviders)
                foreach (var (key, types) in p())
                {
                    if (!merged.TryGetValue(key, out var list))
                        merged[key] = list = [];
                    foreach (var t in types)
                        if (!list.Contains(t))
                            list.Add(t);
                }
            return merged.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<Type>)kv.Value.AsReadOnly());
        };

        Func<IReadOnlyDictionary<string, IReadOnlyList<Type>>> combinedSequentialTopology = () =>
        {
            var merged = new Dictionary<string, List<Type>>();
            foreach (var p in sequentialProviders)
                foreach (var (key, types) in p())
                {
                    if (!merged.TryGetValue(key, out var list))
                        merged[key] = list = [];
                    foreach (var t in types)
                        if (!list.Contains(t))
                            list.Add(t);
                }
            return merged.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<Type>)kv.Value.AsReadOnly());
        };

        _cachedCombined = (combinedFunctions, combinedGroups, combinedAgents, combinedTopology, combinedGroupChatGroups, combinedSequentialTopology);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Registration? _prior;

        public Scope(Registration? prior) => _prior = prior;

        public void Dispose() => _asyncLocalOverride.Value = _prior;
    }
}
