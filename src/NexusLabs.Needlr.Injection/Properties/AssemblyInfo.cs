using System.Runtime.CompilerServices;

// Allow related projects to access internal members for ConfiguredSyringe initialization
[assembly: InternalsVisibleTo("NexusLabs.Needlr.Injection.Reflection")]
[assembly: InternalsVisibleTo("NexusLabs.Needlr.Injection.SourceGen")]
[assembly: InternalsVisibleTo("NexusLabs.Needlr.Injection.Bundle")]
[assembly: InternalsVisibleTo("NexusLabs.Needlr.AspNet")]
[assembly: InternalsVisibleTo("NexusLabs.Needlr.Hosting")]
[assembly: InternalsVisibleTo("NexusLabs.Needlr.SemanticKernel")]
[assembly: InternalsVisibleTo("NexusLabs.Needlr.Injection.Tests")]
[assembly: InternalsVisibleTo("NexusLabs.Needlr.IntegrationTests")]
