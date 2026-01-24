using Microsoft.Extensions.DependencyInjection;

using System.Text;

namespace NexusLabs.Needlr;

/// <summary>
/// Represents a lifetime mismatch where a longer-lived service depends on a shorter-lived service.
/// This is also known as a "captive dependency" and can lead to unexpected behavior.
/// </summary>
/// <param name="ConsumerServiceType">The service type that has the dependency.</param>
/// <param name="ConsumerImplementationType">The implementation type that has the dependency.</param>
/// <param name="ConsumerLifetime">The lifetime of the consumer service.</param>
/// <param name="DependencyServiceType">The service type being depended upon.</param>
/// <param name="DependencyLifetime">The lifetime of the dependency.</param>
public sealed record LifetimeMismatch(
    Type ConsumerServiceType,
    Type? ConsumerImplementationType,
    ServiceLifetime ConsumerLifetime,
    Type DependencyServiceType,
    ServiceLifetime DependencyLifetime)
{
    /// <summary>
    /// Returns a detailed, formatted string representation of this mismatch
    /// suitable for debugging and diagnostics.
    /// </summary>
    public string ToDetailedString()
    {
        var sb = new StringBuilder();
        var consumerName = ConsumerImplementationType?.Name ?? ConsumerServiceType.Name;
        
        sb.AppendLine($"┌─ Lifetime Mismatch");
        sb.AppendLine($"│  {ConsumerServiceType.Name} ({ConsumerLifetime})");
        sb.AppendLine($"│    └─ depends on ─▶ {DependencyServiceType.Name} ({DependencyLifetime})");
        sb.AppendLine($"│");
        sb.AppendLine($"│  Problem: {ConsumerLifetime} service will capture {DependencyLifetime} dependency");
        sb.AppendLine($"│  Fix: Change {ConsumerServiceType.Name} to {DependencyLifetime},");
        sb.AppendLine($"│       or change {DependencyServiceType.Name} to {ConsumerLifetime},");
        sb.AppendLine($"│       or inject IServiceScopeFactory instead.");
        sb.Append($"└─");
        
        return sb.ToString();
    }
}
