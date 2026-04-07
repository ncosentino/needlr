using NexusLabs.Needlr.AgentFramework.Providers;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Simulates a premium geography API that is currently unavailable.
/// Demonstrates <see cref="ProviderUnavailableException"/> triggering fallback.
/// </summary>
public sealed class PremiumGeographyProvider : ITieredProvider<string, IReadOnlyList<string>>
{
    public string Name => "PremiumGeoAPI";
    public int Priority => 1; // tried first
    public bool IsEnabled => true;

    public Task<IReadOnlyList<string>> ExecuteAsync(string query, CancellationToken cancellationToken) =>
        throw new ProviderUnavailableException(Name, "Premium API quota exhausted");
}

/// <summary>
/// Local in-memory geography data. Always succeeds — used as the fallback provider.
/// </summary>
public sealed class LocalGeographyProvider : ITieredProvider<string, IReadOnlyList<string>>
{
    public string Name => "LocalData";
    public int Priority => 100; // fallback
    public bool IsEnabled => true;

    public Task<IReadOnlyList<string>> ExecuteAsync(string query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(["Canada", "United Kingdom", "Japan"]);
}
