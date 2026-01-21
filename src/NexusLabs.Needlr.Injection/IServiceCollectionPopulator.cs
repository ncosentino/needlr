using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Defines a populator that registers discovered types into a service collection.
/// Handles type scanning, filtering, and registration with appropriate service lifetimes.
/// </summary>
[DoNotAutoRegister]
public interface IServiceCollectionPopulator
{
    IServiceCollection RegisterToServiceCollection(
        IServiceCollection services,
        IConfiguration config,
        IReadOnlyList<Assembly> candidateAssemblies);
}
