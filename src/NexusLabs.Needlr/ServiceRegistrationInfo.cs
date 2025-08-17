using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Needlr;

/// <summary>
/// Represents information about a service registration.
/// </summary>
/// <param name="ServiceType">The service type that was registered.</param>
/// <param name="ImplementationType">The implementation type, if available.</param>
/// <param name="Lifetime">The service lifetime.</param>
/// <param name="HasFactory">Whether the registration uses a factory method.</param>
/// <param name="HasInstance">Whether the registration uses a pre-created instance.</param>
public readonly record struct ServiceRegistrationInfo(
    Type ServiceType,
    Type? ImplementationType,
    ServiceLifetime Lifetime,
    bool HasFactory,
    bool HasInstance);