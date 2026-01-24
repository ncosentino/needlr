using Microsoft.Extensions.DependencyInjection;

using System.Text;

namespace NexusLabs.Needlr;

public readonly record struct ServiceRegistrationInfo(
    ServiceDescriptor ServiceDescriptor)
{
    /// <summary>
    /// Gets the service (abstraction/contract) <see cref="Type"/> that was registered.
    /// This is the key used by the DI container for resolution requests.
    /// </summary>
    public Type ServiceType => ServiceDescriptor.ServiceType;
    
    /// <summary>
    /// Gets the concrete implementation <see cref="Type"/>, if the registration
    /// was made with an implementation type. Returns <see langword="null"/> when:
    /// <list type="bullet">
    ///   <item>The registration uses an <see cref="ServiceDescriptor.ImplementationFactory"/>.</item>
    ///   <item>The registration supplies a pre-built <see cref="ServiceDescriptor.ImplementationInstance"/>.</item>
    ///   <item>The registration represents an open generic without a concrete close (rare in reflection scenarios).</item>
    /// </list>
    /// </summary>
    public Type? ImplementationType => ServiceDescriptor.ImplementationType;

    /// <summary>
    /// Gets the lifetime (<see cref="ServiceLifetime.Singleton"/>,
    /// <see cref="ServiceLifetime.Scoped"/>, or <see cref="ServiceLifetime.Transient"/>)
    /// associated with the registration.
    /// </summary>
    public ServiceLifetime Lifetime => ServiceDescriptor.Lifetime;

    /// <summary>
    /// Indicates whether the registration was configured with a factory delegate
    /// (<see cref="ServiceDescriptor.ImplementationFactory"/>). When <see langword="true"/>,
    /// <see cref="ImplementationType"/> will be <see langword="null"/>.
    /// </summary>
    public bool HasFactory => ServiceDescriptor.ImplementationFactory is not null;

    /// <summary>
    /// Indicates whether the registration was configured with a pre-constructed
    /// instance (<see cref="ServiceDescriptor.ImplementationInstance"/>). When
    /// <see langword="true"/>, <see cref="ImplementationType"/> will typically be
    /// <see langword="null"/> (unless metadata reflects the instance's type indirectly).
    /// </summary>
    public bool HasInstance => ServiceDescriptor.ImplementationInstance is not null;

    /// <summary>
    /// Returns a detailed, formatted string representation of this registration
    /// suitable for debugging and diagnostics.
    /// </summary>
    /// <returns>A multi-line formatted string with registration details.</returns>
    public string ToDetailedString()
    {
        var sb = new StringBuilder();
        var serviceTypeName = FormatTypeName(ServiceType);
        
        sb.AppendLine($"┌─ {serviceTypeName}");
        sb.AppendLine($"│  Lifetime: {Lifetime}");
        
        if (HasInstance)
        {
            var instanceType = ServiceDescriptor.ImplementationInstance?.GetType();
            sb.AppendLine($"│  Implementation: Instance ({FormatTypeName(instanceType)})");
        }
        else if (HasFactory)
        {
            sb.AppendLine($"│  Implementation: Factory");
        }
        else if (ImplementationType is not null)
        {
            sb.AppendLine($"│  Implementation: {FormatTypeName(ImplementationType)}");
        }
        
        sb.Append($"└─");
        
        return sb.ToString();
    }

    private static string FormatTypeName(Type? type)
    {
        if (type is null)
        {
            return "<unknown>";
        }

        if (!type.IsGenericType)
        {
            return type.Name;
        }

        if (type.IsGenericTypeDefinition)
        {
            // Open generic: IGenericService<> 
            var baseName = type.Name.Split('`')[0];
            return $"{baseName}<>";
        }

        // Closed generic: IGenericService<String>
        var genericBaseName = type.Name.Split('`')[0];
        var genericArgs = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
        return $"{genericBaseName}<{genericArgs}>";
    }
}