using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Testing;

/// <summary>
/// Test harness for invoking <c>[AgentFunction]</c>-decorated tool methods through their
/// source-generated <see cref="AIFunction"/> wrapper, with the same plumbing
/// <see cref="Microsoft.Extensions.AI.FunctionInvokingChatClient"/> uses in production.
/// </summary>
/// <remarks>
/// <para>
/// The runner exists to remove the boilerplate consumers face when testing tools they wrote
/// for Needlr's Agent Framework integration: build a service provider, register the tool, look
/// up the source-generated <see cref="IAIFunctionProvider"/>, find the right
/// <see cref="AIFunction"/> by name, build <see cref="AIFunctionArguments"/>, establish an
/// ambient <see cref="IAgentExecutionContext"/> so the tool can read
/// <see cref="IAgentExecutionContextAccessor.Current"/>, and invoke.
/// </para>
/// <para>
/// By default the runner resolves only via the source-generated <see cref="IAIFunctionProvider"/>
/// registered by the <c>[ModuleInitializer]</c> the Needlr Agent Framework generator emits in
/// the consuming assembly. This is the same path production agents take. To exercise the
/// reflection-based <see cref="AIFunctionFactory"/> path instead, call
/// <see cref="GetFunctionAllowingReflection{TTool}(string)"/> explicitly — that method carries
/// <see cref="RequiresUnreferencedCodeAttribute"/> annotations because it is incompatible with
/// NativeAOT.
/// </para>
/// <para>
/// Instances are immutable: each <c>With*</c> method returns a new runner with the configuration
/// applied. Each <see cref="InvokeAsync{TTool}(string, Action{AIFunctionArguments}?, CancellationToken)"/>
/// call creates a fresh <see cref="IServiceScope"/> (when an <see cref="IServiceScopeFactory"/>
/// is available) so tools with scoped dependencies behave correctly across invocations.
/// </para>
/// </remarks>
/// <example>
/// Minimal test, full bring-your-own service provider:
/// <code>
/// var sp = new ServiceCollection()
///     .AddAgentFrameworkAccessors()
///     .AddSingleton&lt;GrepTool&gt;()
///     .BuildServiceProvider();
///
/// var runner = new ToolInvocationRunner(sp)
///     .WithWorkspace(ws =&gt; ws.TryWriteFile("a.txt", "hi"));
///
/// var result = await runner.InvokeAsync&lt;GrepTool&gt;("grep_files", a =&gt;
/// {
///     a["pattern"] = "hi";
///     a["path"]    = "/";
/// });
///
/// result.AssertSuccess();
/// result.AssertResultContains("a.txt");
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed class ToolInvocationRunner
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Action<AgentExecutionContextBuilder>? _contextConfigurator;
    private readonly IReadOnlyList<Type>? _limitedToolTypes;

    /// <summary>
    /// Creates a runner over an already-built <see cref="IServiceProvider"/>. Use this when the
    /// test fixture builds its own DI container (typical for Syringe-based test fixtures, or for
    /// tests that need to share a provider across multiple invocations).
    /// </summary>
    /// <param name="serviceProvider">
    /// Provider that has the tool type and its dependencies registered. The Needlr accessors
    /// (<see cref="IAgentExecutionContextAccessor"/>, <see cref="Diagnostics.IAgentDiagnosticsAccessor"/>)
    /// must also be registered — call
    /// <see cref="AgentFrameworkAccessorServiceCollectionExtensions.AddAgentFrameworkAccessors"/>
    /// or use the broader <c>UsingAgentFramework()</c> Syringe extension.
    /// </param>
    public ToolInvocationRunner(IServiceProvider serviceProvider)
        : this(serviceProvider, contextConfigurator: null, limitedToolTypes: null)
    {
    }

    private ToolInvocationRunner(
        IServiceProvider serviceProvider,
        Action<AgentExecutionContextBuilder>? contextConfigurator,
        IReadOnlyList<Type>? limitedToolTypes)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
        _contextConfigurator = contextConfigurator;
        _limitedToolTypes = limitedToolTypes;
    }

    /// <summary>
    /// Creates a runner over a fresh service provider with the Needlr accessors and
    /// <typeparamref name="TTool"/> registered as a singleton. Additional services can be added
    /// via the optional <paramref name="configureServices"/> callback.
    /// </summary>
    /// <typeparam name="TTool">The tool class to register.</typeparam>
    /// <param name="configureServices">
    /// Optional hook to register additional dependencies (e.g. <c>IHttpClientFactory</c>, options,
    /// fakes for scoped services).
    /// </param>
    public static ToolInvocationRunner CreateFor<TTool>(
        Action<IServiceCollection>? configureServices = null)
        where TTool : class
    {
        var services = new ServiceCollection().AddAgentFrameworkAccessors();
        services.AddSingleton<TTool>();
        configureServices?.Invoke(services);
        return new ToolInvocationRunner(services.BuildServiceProvider());
    }

    /// <summary>
    /// Creates a runner over a fresh service provider with only the Needlr accessors registered.
    /// Use this overload when registering multiple tools or when you want full control over the
    /// service collection setup.
    /// </summary>
    /// <param name="configureServices">
    /// Hook to register the tools and any dependencies they need.
    /// </param>
    public static ToolInvocationRunner Create(
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection().AddAgentFrameworkAccessors();
        configureServices?.Invoke(services);
        return new ToolInvocationRunner(services.BuildServiceProvider());
    }

    /// <summary>
    /// Whether a source-generated <see cref="IAIFunctionProvider"/> is currently registered with
    /// <see cref="AgentFrameworkGeneratedBootstrap"/>. Useful as a precondition assertion in
    /// tests that require the source generator to have run for at least one assembly in the
    /// process.
    /// </summary>
    /// <remarks>
    /// This property tells you only whether <em>any</em> generated provider exists globally; it
    /// does not tell you whether <em>your specific tool</em> is resolvable. Call
    /// <see cref="GetFunction{TTool}(string)"/> if you need to verify a specific function — the
    /// error message it throws when a type or method is missing is the canonical signal.
    /// </remarks>
    public bool IsGeneratedProviderAvailable
        => AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(out _);

    /// <summary>
    /// Throws when no source-generated <see cref="IAIFunctionProvider"/> is registered.
    /// Use this as a fail-fast guard in tests that depend on the generator output for at least
    /// one assembly in the process.
    /// </summary>
    /// <remarks>
    /// In practice this rarely throws because the Needlr Agent Framework assembly itself emits a
    /// (possibly empty) provider via <c>[ModuleInitializer]</c>, which registers as soon as
    /// <c>NexusLabs.Needlr.AgentFramework.dll</c> loads. The check is still useful as a sanity
    /// guard in environments where modules may not have initialized yet (e.g. some custom
    /// AssemblyLoadContext scenarios).
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no generated provider is available, with guidance on how to enable it.
    /// </exception>
    public void AssertGeneratedProviderAvailable()
    {
        if (!IsGeneratedProviderAvailable)
        {
            throw new InvalidOperationException(
                "No source-generated IAIFunctionProvider is registered. " +
                "The Needlr Agent Framework source generator did not run for the assembly under test. " +
                "Add a project reference to NexusLabs.Needlr.AgentFramework.Generators with " +
                "OutputItemType=\"Analyzer\" and ReferenceOutputAssembly=\"false\", or call " +
                "GetFunctionAllowingReflection<T>() to opt into the reflection fallback.");
        }
    }

    /// <summary>
    /// Returns a new runner that establishes an <see cref="IAgentExecutionContext"/> built by
    /// <paramref name="configure"/> for the duration of each <c>InvokeAsync</c> call.
    /// </summary>
    /// <remarks>
    /// Successive calls replace the previously-configured context (immutable copy semantics).
    /// </remarks>
    public ToolInvocationRunner WithExecutionContext(Action<AgentExecutionContextBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return new ToolInvocationRunner(_serviceProvider, configure, _limitedToolTypes);
    }

    /// <summary>
    /// Convenience: returns a new runner that creates a fresh <see cref="InMemoryWorkspace"/>,
    /// runs <paramref name="seed"/> against it, and attaches it to the execution context.
    /// </summary>
    public ToolInvocationRunner WithWorkspace(Action<IWorkspace> seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        return WithExecutionContext(c => c.WithWorkspace(seed));
    }

    /// <summary>
    /// Convenience: returns a new runner that attaches the supplied <paramref name="workspace"/>
    /// to the execution context.
    /// </summary>
    public ToolInvocationRunner WithWorkspace(IWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return WithExecutionContext(c => c.WithWorkspace(workspace));
    }

    /// <summary>
    /// Returns a new runner that scopes
    /// <see cref="AgentFrameworkGeneratedBootstrap"/> to expose only
    /// <paramref name="toolTypes"/> during function resolution. Useful in consumer test
    /// projects that contain many <c>[AgentFunction]</c> types and want to assert behavior
    /// against a specific subset without disturbing other tests.
    /// </summary>
    /// <param name="toolTypes">
    /// The set of tool types visible to the source-generated provider during this runner's
    /// invocations. Must contain at least one type.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="toolTypes"/> is empty.
    /// </exception>
    public ToolInvocationRunner LimitToTools(params Type[] toolTypes)
    {
        ArgumentNullException.ThrowIfNull(toolTypes);
        if (toolTypes.Length == 0)
        {
            throw new ArgumentException(
                "At least one tool type must be supplied. To clear a previous LimitToTools, " +
                "construct a new runner instead.",
                nameof(toolTypes));
        }
        return new ToolInvocationRunner(_serviceProvider, _contextConfigurator, toolTypes);
    }

    /// <summary>
    /// Resolves an <see cref="AIFunction"/> for <typeparamref name="TTool"/> by method name via
    /// the source-generated <see cref="IAIFunctionProvider"/>.
    /// </summary>
    /// <typeparam name="TTool">The tool class declaring the <c>[AgentFunction]</c> method.</typeparam>
    /// <param name="methodName">
    /// The function name as exposed to the LLM (defaults to the C# method name when the
    /// source generator emits the wrapper).
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no generated provider is registered, or when no function with that name is
    /// found for the type.
    /// </exception>
    public AIFunction GetFunction<TTool>(string methodName) where TTool : class
        => GetFunction(typeof(TTool), methodName);

    /// <summary>
    /// Resolves an <see cref="AIFunction"/> for <paramref name="toolType"/> by method name via
    /// the source-generated <see cref="IAIFunctionProvider"/>.
    /// </summary>
    public AIFunction GetFunction(Type toolType, string methodName)
    {
        ArgumentNullException.ThrowIfNull(toolType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        using var bootstrapScope = BeginBootstrapScopeIfLimited();

        if (!AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(out var provider))
        {
            throw new InvalidOperationException(
                $"Cannot resolve [AgentFunction] '{methodName}' on '{toolType.Name}': " +
                "no source-generated IAIFunctionProvider is registered. " +
                "The Needlr Agent Framework source generator did not run for the assembly " +
                $"containing '{toolType.Name}'. Either add a project reference to " +
                "NexusLabs.Needlr.AgentFramework.Generators (Analyzer, no output assembly), " +
                "or call GetFunctionAllowingReflection<T>() to opt into the reflection fallback.");
        }

        using var serviceScope = _serviceProvider.GetService<IServiceScopeFactory>()?.CreateScope();
        var resolutionServices = serviceScope?.ServiceProvider ?? _serviceProvider;

        if (!provider.TryGetFunctions(toolType, resolutionServices, out var functions))
        {
            throw new InvalidOperationException(
                $"The source-generated IAIFunctionProvider has no functions for '{toolType.Name}'. " +
                "Ensure the type is decorated with [AgentFunctionGroup] (or referenced by a " +
                "[NeedlrAiAgent] FunctionTypes argument) so the generator picks it up.");
        }

        var function = functions.FirstOrDefault(f => f.Name == methodName);
        if (function is null)
        {
            var available = string.Join(", ", functions!.Select(f => $"'{f.Name}'"));
            throw new InvalidOperationException(
                $"No [AgentFunction] named '{methodName}' on '{toolType.Name}'. " +
                $"Available functions on this type: {(available.Length == 0 ? "<none>" : available)}.");
        }

        return function;
    }

    /// <summary>
    /// Resolves an <see cref="AIFunction"/> for <typeparamref name="TTool"/> by method name,
    /// preferring the source-generated provider but falling back to reflection-based discovery
    /// via <see cref="AIFunctionFactory.Create(MethodInfo, object?, AIFunctionFactoryOptions?)"/>
    /// when the generator output is not available.
    /// </summary>
    /// <remarks>
    /// This method is incompatible with NativeAOT because the reflection branch dynamically
    /// generates marshalling code. Tests targeting AOT must use <see cref="GetFunction{TTool}(string)"/>
    /// instead.
    /// </remarks>
    [RequiresUnreferencedCode("Reflection-based AIFunction discovery requires unreferenced code access.")]
    [RequiresDynamicCode("Reflection-based AIFunction discovery requires dynamic code generation.")]
    public AIFunction GetFunctionAllowingReflection<TTool>(string methodName) where TTool : class
        => GetFunctionAllowingReflection(typeof(TTool), methodName);

    /// <summary>
    /// Resolves an <see cref="AIFunction"/> for <paramref name="toolType"/> by method name,
    /// preferring the source-generated provider but falling back to reflection-based discovery
    /// via <see cref="AIFunctionFactory.Create(MethodInfo, object?, AIFunctionFactoryOptions?)"/>
    /// when the generator output is not available.
    /// </summary>
    [RequiresUnreferencedCode("Reflection-based AIFunction discovery requires unreferenced code access.")]
    [RequiresDynamicCode("Reflection-based AIFunction discovery requires dynamic code generation.")]
    public AIFunction GetFunctionAllowingReflection(Type toolType, string methodName)
    {
        ArgumentNullException.ThrowIfNull(toolType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        using var bootstrapScope = BeginBootstrapScopeIfLimited();

        if (AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(out var provider))
        {
            using var serviceScope = _serviceProvider.GetService<IServiceScopeFactory>()?.CreateScope();
            var resolutionServices = serviceScope?.ServiceProvider ?? _serviceProvider;

            if (provider.TryGetFunctions(toolType, resolutionServices, out var generated))
            {
                var generatedFn = generated.FirstOrDefault(f => f.Name == methodName);
                if (generatedFn is not null)
                {
                    return generatedFn;
                }
            }
        }

        var isStatic = toolType.IsAbstract && toolType.IsSealed;
        var bindingFlags = isStatic
            ? BindingFlags.Public | BindingFlags.Static
            : BindingFlags.Public | BindingFlags.Instance;

        var method = toolType.GetMethods(bindingFlags)
            .FirstOrDefault(m =>
                m.Name == methodName &&
                m.IsDefined(typeof(AgentFunctionAttribute), inherit: true))
            ?? throw new InvalidOperationException(
                $"Reflection fallback could not find a method named '{methodName}' decorated " +
                $"with [AgentFunction] on '{toolType.Name}'.");

        object? instance = isStatic
            ? null
            : ActivatorUtilities.CreateInstance(_serviceProvider, toolType);

        return AIFunctionFactory.Create(method, target: instance);
    }

    /// <summary>
    /// Resolves the source-generated <see cref="AIFunction"/> for the given tool method, builds
    /// an <see cref="AIFunctionArguments"/> via <paramref name="configureArgs"/>, establishes
    /// the configured execution context, and invokes the function. Captures any thrown exception
    /// into the returned <see cref="ToolInvocationResult"/> rather than propagating.
    /// </summary>
    /// <typeparam name="TTool">The tool class declaring the <c>[AgentFunction]</c> method.</typeparam>
    /// <param name="methodName">The function name as exposed to the LLM.</param>
    /// <param name="configureArgs">
    /// Optional hook to populate <see cref="AIFunctionArguments"/>. Pass <see langword="null"/> to
    /// invoke with no arguments.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token forwarded to <see cref="AIFunction.InvokeAsync"/>. Tools that accept a
    /// <see cref="CancellationToken"/> parameter receive this token via the source-generated wrapper.
    /// </param>
    public Task<ToolInvocationResult> InvokeAsync<TTool>(
        string methodName,
        Action<AIFunctionArguments>? configureArgs = null,
        CancellationToken cancellationToken = default)
        where TTool : class
        => InvokeAsync(typeof(TTool), methodName, configureArgs, cancellationToken);

    /// <summary>
    /// Same as <see cref="InvokeAsync{TTool}(string, Action{AIFunctionArguments}?, CancellationToken)"/>
    /// but accepts a pre-built dictionary of arguments. Convenient when the test already has a
    /// <see cref="IReadOnlyDictionary{TKey, TValue}"/> of args ready (e.g. captured from a prior
    /// run or shared across multiple invocations).
    /// </summary>
    public Task<ToolInvocationResult> InvokeAsync<TTool>(
        string methodName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
        where TTool : class
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return InvokeAsync<TTool>(
            methodName,
            args =>
            {
                foreach (var pair in arguments)
                {
                    args[pair.Key] = pair.Value;
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Resolves and invokes a function for <paramref name="toolType"/>. Identical to the generic
    /// overload but accepts the type as a parameter for callers who only have a runtime
    /// <see cref="Type"/>.
    /// </summary>
    public async Task<ToolInvocationResult> InvokeAsync(
        Type toolType,
        string methodName,
        Action<AIFunctionArguments>? configureArgs = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        var contextBuilder = new AgentExecutionContextBuilder();
        _contextConfigurator?.Invoke(contextBuilder);
        var executionContext = contextBuilder.Build();
        var workspace = contextBuilder.Workspace;

        AIFunction function;
        try
        {
            function = GetFunction(toolType, methodName);
        }
        catch (Exception resolutionException)
        {
            return new ToolInvocationResult(
                ReturnValue: null,
                Exception: resolutionException,
                FunctionSource: ToolFunctionSource.Generated,
                Workspace: workspace,
                Duration: TimeSpan.Zero);
        }

        var args = new AIFunctionArguments();
        configureArgs?.Invoke(args);

        var accessor = _serviceProvider.GetRequiredService<IAgentExecutionContextAccessor>();
        var stopwatch = Stopwatch.StartNew();
        Exception? invocationException = null;
        object? returnValue = null;

        using (accessor.BeginScope(executionContext))
        {
            try
            {
                returnValue = await function.InvokeAsync(args, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                invocationException = ex;
            }
        }

        stopwatch.Stop();

        return new ToolInvocationResult(
            ReturnValue: returnValue,
            Exception: invocationException,
            FunctionSource: ToolFunctionSource.Generated,
            Workspace: workspace,
            Duration: stopwatch.Elapsed);
    }

    private IDisposable? BeginBootstrapScopeIfLimited()
    {
        if (_limitedToolTypes is null)
        {
            return null;
        }

        var types = _limitedToolTypes;
        return AgentFrameworkGeneratedBootstrap.BeginTestScope(
            functionTypes: () => types,
            groupTypes: static () => new Dictionary<string, IReadOnlyList<Type>>(),
            agentTypes: static () => []);
    }
}
