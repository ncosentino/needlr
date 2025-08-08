using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

[DoNotAutoRegister]
public interface IServiceProviderBuilder
{
    IServiceProvider Build(
        IConfiguration config);
    
    IServiceProvider Build(
        IServiceCollection services, 
        IConfiguration config);
    
    void ConfigurePostBuildServiceCollectionPlugins(
        IServiceProvider provider, 
        IConfiguration config);
    
    IReadOnlyList<Assembly> GetCandidateAssemblies();
}
