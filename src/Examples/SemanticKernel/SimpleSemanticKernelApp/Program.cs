using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.SemanticKernel;

var kernel = new Syringe()
    .UsingReflection()
    .UsingSemanticKernel(syringe => syringe
        .Configure(opts =>
        {
            // NOTE: this configures Azure OpenAI as the LLM provider, but you
            // can use whatever you want here where there's support in Semantic
            // Kernel. Feel free to try another provider!
            var config = opts.ServiceProvider.GetRequiredService<IConfiguration>();
            var azureOpenAiSection = config.GetSection("AzureOpenAI");
            opts.KernelBuilder.AddAzureOpenAIChatCompletion(
                deploymentName: azureOpenAiSection.GetValue<string>("DeploymentName")
                    ?? throw new InvalidOperationException("No deployment name set"),
                endpoint: azureOpenAiSection.GetValue<string>("Endpoint")
                    ?? throw new InvalidOperationException("No endpoint set"),
                apiKey: azureOpenAiSection.GetValue<string>("ApiKey")
                    ?? throw new InvalidOperationException("No API key set"));
        })
        .AddSemanticKernelPluginsFromProvider()
        .AddSemanticKernelPluginsFromAssemblies())
    .BuildServiceProvider(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
        .Build())
    .GetRequiredService<IKernelFactory>()
    .CreateKernel();

await kernel.AskAsync("What are Nick's favorite cities?");
await kernel.AskAsync("What countries has Nick lived in?");
await kernel.AskAsync("What are Nick's favorite icecreams?");
