using Microsoft.Extensions.DependencyInjection;

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
}