// GeneratorCoexistenceApp
//
// PURPOSE: Proves that Needlr's source generators and MAF's Workflows.Generators
//          coexist in the same compilation without conflict. This project serves as
//          living documentation — if a future MAF or Needlr release introduces a
//          generator collision, this project's build will break.
//
// WHAT'S HERE:
//   - Needlr generators: [NeedlrAiAgent], [AgentFunctionGroup], [AgentFunction]
//   - MAF generators:    [MessageHandler] on an Executor class
//   - Both generators run at compile time in the same project

using GeneratorCoexistenceApp;

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Generator Coexistence Example                              ║");
Console.WriteLine("║  Needlr generators + MAF Workflows.Generators in one build  ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// ── Needlr generator output verification ──
Console.WriteLine("── Needlr Source Generator Output ──");

var agentType = typeof(DataAssistant);
Console.WriteLine($"  [NeedlrAiAgent] DataAssistant registered: {agentType.FullName}");

var functionType = typeof(DataFunctions);
Console.WriteLine($"  [AgentFunctionGroup] DataFunctions registered: {functionType.FullName}");

// Verify the source-generated AIFunction provider exists
var providerType = agentType.Assembly.GetTypes()
    .FirstOrDefault(t => t.Name == "GeneratedAIFunctionProvider");
Console.WriteLine($"  GeneratedAIFunctionProvider emitted: {providerType is not null}");

// Verify the source-generated agent registry exists
var registryType = agentType.Assembly.GetTypes()
    .FirstOrDefault(t => t.Name == "AgentFrameworkFunctionRegistry");
Console.WriteLine($"  AgentFrameworkFunctionRegistry emitted: {registryType is not null}");

// Verify the source-generated bootstrap exists
var bootstrapType = agentType.Assembly.GetTypes()
    .FirstOrDefault(t => t.Name == "NeedlrAgentFrameworkBootstrap");
Console.WriteLine($"  NeedlrAgentFrameworkBootstrap emitted: {bootstrapType is not null}");

Console.WriteLine();

// ── MAF generator output verification ──
Console.WriteLine("── MAF Workflows.Generators Output ──");

var executorType = typeof(GeneratorCoexistenceApp.Executors.EchoExecutor);
Console.WriteLine($"  [MessageHandler] EchoExecutor registered: {executorType.FullName}");

// MAF's generator emits a ConfigureRoutes method on the partial class
var configureRoutes = executorType.GetMethod(
    "ConfigureRoutes",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
Console.WriteLine($"  ConfigureRoutes generated: {configureRoutes is not null}");

Console.WriteLine();

// ── Generated file inventory ──
Console.WriteLine("── Generated Files (check obj/Generated/) ──");
Console.WriteLine("  Needlr emits:");
Console.WriteLine("    - NeedlrAgentFrameworkBootstrap.g.cs");
Console.WriteLine("    - AgentFrameworkFunctions.g.cs");
Console.WriteLine("    - GeneratedAIFunctionProvider.g.cs");
Console.WriteLine("    - AgentRegistry.g.cs");
Console.WriteLine("    - AgentFunctionGroupRegistry.g.cs");
Console.WriteLine("  MAF emits:");
Console.WriteLine("    - EchoExecutor route configuration (in obj/Generated/)");

Console.WriteLine();
Console.WriteLine("✅ Both generators compiled successfully in the same project.");
Console.WriteLine("   No file name collisions, no attribute conflicts, no DI conflicts.");
