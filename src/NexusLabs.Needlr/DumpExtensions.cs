using Microsoft.Extensions.DependencyInjection;

using System.Text;

namespace NexusLabs.Needlr;

/// <summary>
/// Extension methods for dumping service registration information for debugging.
/// </summary>
public static class DumpExtensions
{
    /// <summary>
    /// Returns a formatted string representation of all service registrations
    /// in the service collection for debugging purposes.
    /// </summary>
    /// <param name="services">The service collection to dump.</param>
    /// <param name="options">Optional dump options for filtering and formatting.</param>
    /// <returns>A formatted string with registration details.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services is null.</exception>
    public static string Dump(this IServiceCollection services, DumpOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        options ??= DumpOptions.Default;
        var sb = new StringBuilder();
        
        var filteredDescriptors = services.AsEnumerable();
        
        // Apply lifetime filter
        if (options.LifetimeFilter.HasValue)
        {
            filteredDescriptors = filteredDescriptors.Where(d => d.Lifetime == options.LifetimeFilter.Value);
        }
        
        // Apply service type filter
        if (options.ServiceTypeFilter is not null)
        {
            filteredDescriptors = filteredDescriptors.Where(d => options.ServiceTypeFilter(d.ServiceType));
        }
        
        var descriptorList = filteredDescriptors.ToList();
        
        sb.AppendLine($"═══ Service Registrations ({descriptorList.Count} registrations) ═══");
        sb.AppendLine();
        
        if (descriptorList.Count == 0)
        {
            sb.AppendLine("  (no registrations match the filter)");
            return sb.ToString();
        }
        
        if (options.GroupByLifetime)
        {
            DumpGroupedByLifetime(sb, descriptorList);
        }
        else
        {
            DumpFlat(sb, descriptorList);
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Returns a formatted string representation of all service registrations
    /// in the service provider for debugging purposes.
    /// </summary>
    /// <param name="serviceProvider">The service provider to dump.</param>
    /// <param name="options">Optional dump options for filtering and formatting.</param>
    /// <returns>A formatted string with registration details.</returns>
    /// <exception cref="ArgumentNullException">Thrown when serviceProvider is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the service collection is not accessible.</exception>
    public static string Dump(this IServiceProvider serviceProvider, DumpOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        
        var serviceCollection = serviceProvider.GetServiceCollection();
        return serviceCollection.Dump(options);
    }

    private static void DumpGroupedByLifetime(StringBuilder sb, List<ServiceDescriptor> descriptors)
    {
        var groups = descriptors
            .GroupBy(d => d.Lifetime)
            .OrderBy(g => g.Key);
        
        foreach (var group in groups)
        {
            sb.AppendLine($"── {group.Key} ──");
            sb.AppendLine();
            
            foreach (var descriptor in group.OrderBy(d => d.ServiceType.Name))
            {
                var info = new ServiceRegistrationInfo(descriptor);
                sb.AppendLine(info.ToDetailedString());
                sb.AppendLine();
            }
        }
    }

    private static void DumpFlat(StringBuilder sb, List<ServiceDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors.OrderBy(d => d.ServiceType.Name))
        {
            var info = new ServiceRegistrationInfo(descriptor);
            sb.AppendLine(info.ToDetailedString());
            sb.AppendLine();
        }
    }
}
