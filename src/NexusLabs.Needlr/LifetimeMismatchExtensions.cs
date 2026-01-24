using Microsoft.Extensions.DependencyInjection;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr;

/// <summary>
/// Extension methods for detecting lifetime mismatches (captive dependencies) in service registrations.
/// </summary>
public static class LifetimeMismatchExtensions
{
    /// <summary>
    /// Detects lifetime mismatches (captive dependencies) in the service collection.
    /// A lifetime mismatch occurs when a longer-lived service depends on a shorter-lived service.
    /// </summary>
    /// <param name="services">The service collection to analyze.</param>
    /// <returns>A list of detected lifetime mismatches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services is null.</exception>
    /// <remarks>
    /// <para>Lifetime hierarchy (from longest to shortest):</para>
    /// <list type="bullet">
    /// <item>Singleton (lives for entire application lifetime)</item>
    /// <item>Scoped (lives for the scope/request lifetime)</item>
    /// <item>Transient (new instance every time)</item>
    /// </list>
    /// <para>
    /// A mismatch occurs when a service with a longer lifetime depends on a service with a shorter lifetime.
    /// For example, a Singleton depending on a Scoped service will "capture" the scoped instance,
    /// causing it to live longer than intended.
    /// </para>
    /// <para>
    /// Factory registrations cannot be analyzed and are skipped.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<LifetimeMismatch> DetectLifetimeMismatches(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        var mismatches = new List<LifetimeMismatch>();
        
        // Build a lookup of service type -> lifetime
        var lifetimeLookup = BuildLifetimeLookup(services);
        
        foreach (var descriptor in services)
        {
            // Skip factory registrations - we can't analyze their dependencies
            if (descriptor.ImplementationType is null)
            {
                continue;
            }
            
            var consumerLifetime = descriptor.Lifetime;
            
            // Transient services can depend on anything without causing captive dependencies
            if (consumerLifetime == ServiceLifetime.Transient)
            {
                continue;
            }
            
            // Analyze constructor dependencies
            // Note: descriptor.ImplementationType from MS.DI doesn't have DynamicallyAccessedMembers annotation,
            // but constructor metadata is typically preserved for DI-registered types.
#pragma warning disable IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call
            var dependencies = GetConstructorDependencies(descriptor.ImplementationType);
#pragma warning restore IL2072
            
            foreach (var dependencyType in dependencies)
            {
                if (!lifetimeLookup.TryGetValue(dependencyType, out var dependencyLifetime))
                {
                    // Dependency not registered - skip
                    continue;
                }
                
                if (IsLifetimeMismatch(consumerLifetime, dependencyLifetime))
                {
                    mismatches.Add(new LifetimeMismatch(
                        ConsumerServiceType: descriptor.ServiceType,
                        ConsumerImplementationType: descriptor.ImplementationType,
                        ConsumerLifetime: consumerLifetime,
                        DependencyServiceType: dependencyType,
                        DependencyLifetime: dependencyLifetime));
                }
            }
        }
        
        return mismatches;
    }

    private static Dictionary<Type, ServiceLifetime> BuildLifetimeLookup(IServiceCollection services)
    {
        var lookup = new Dictionary<Type, ServiceLifetime>();
        
        foreach (var descriptor in services)
        {
            // Use the last registration for a given service type (mimics DI container behavior)
            lookup[descriptor.ServiceType] = descriptor.Lifetime;
        }
        
        return lookup;
    }

    private static IEnumerable<Type> GetConstructorDependencies(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
    {
        // Get the constructor that the DI container would use (longest parameter list)
        var constructors = implementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        
        if (constructors.Length == 0)
        {
            yield break;
        }
        
        // DI container typically uses the constructor with the most parameters
        var constructor = constructors
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        
        foreach (var parameter in constructor.GetParameters())
        {
            yield return parameter.ParameterType;
        }
    }

    /// <summary>
    /// Determines if there is a lifetime mismatch between a consumer and its dependency.
    /// </summary>
    /// <param name="consumerLifetime">The lifetime of the consuming service.</param>
    /// <param name="dependencyLifetime">The lifetime of the dependency.</param>
    /// <returns>True if there is a mismatch (consumer lives longer than dependency).</returns>
    private static bool IsLifetimeMismatch(ServiceLifetime consumerLifetime, ServiceLifetime dependencyLifetime)
    {
        // Lifetime "rank" - higher number = longer lifetime
        static int GetLifetimeRank(ServiceLifetime lifetime) => lifetime switch
        {
            ServiceLifetime.Transient => 0,
            ServiceLifetime.Scoped => 1,
            ServiceLifetime.Singleton => 2,
            _ => 0
        };
        
        var consumerRank = GetLifetimeRank(consumerLifetime);
        var dependencyRank = GetLifetimeRank(dependencyLifetime);
        
        // Mismatch if consumer lives longer than dependency
        return consumerRank > dependencyRank;
    }
}
