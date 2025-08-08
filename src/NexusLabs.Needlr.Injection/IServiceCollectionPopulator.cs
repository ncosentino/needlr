using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

[DoNotAutoRegister]
public interface IServiceCollectionPopulator
{
    IServiceCollection RegisterToServiceCollection(
        IServiceCollection services,
        IConfiguration config,
        IReadOnlyList<Assembly> candidateAssemblies);
}
