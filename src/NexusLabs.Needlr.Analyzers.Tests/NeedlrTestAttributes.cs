namespace NexusLabs.Needlr.Analyzers.Tests;

/// <summary>
/// Provides C# source code for Needlr attributes that can be embedded in analyzer tests.
/// This ensures analyzer tests use the same attribute definitions as the real package.
/// </summary>
internal static class NeedlrTestAttributes
{
    /// <summary>
    /// Core Needlr attributes: lifetime attributes, DoNotInject, DoNotAutoRegister.
    /// </summary>
    public const string Core = @"
namespace NexusLabs.Needlr
{
    /// <summary>
    /// Specifies that the decorated class should be registered with Singleton lifetime.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SingletonAttribute : System.Attribute { }

    /// <summary>
    /// Specifies that the decorated class should be registered with Scoped lifetime.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ScopedAttribute : System.Attribute { }

    /// <summary>
    /// Specifies that the decorated class should be registered with Transient lifetime.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TransientAttribute : System.Attribute { }

    /// <summary>
    /// Marks a type to be excluded from automatic dependency injection registration.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    public sealed class DoNotInjectAttribute : System.Attribute { }

    /// <summary>
    /// Marks a type to not be automatically registered via Needlr.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    public sealed class DoNotAutoRegisterAttribute : System.Attribute { }
}";

    /// <summary>
    /// Keyed service attribute.
    /// </summary>
    public const string Keyed = @"
namespace NexusLabs.Needlr
{
    /// <summary>
    /// Marks a class for keyed service registration.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class KeyedAttribute : System.Attribute
    {
        public KeyedAttribute(string key) => Key = key;
        public string Key { get; }
    }
}";

    /// <summary>
    /// Interceptor types: IMethodInterceptor, IMethodInvocation, InterceptAttribute.
    /// Also includes DoNotAutoRegister which is used by IMethodInterceptor.
    /// </summary>
    public const string Interceptors = @"
namespace NexusLabs.Needlr
{
    /// <summary>
    /// Marks a type to not be automatically registered via Needlr.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    public sealed class DoNotAutoRegisterAttribute : System.Attribute { }

    /// <summary>
    /// Intercepts method invocations on a service.
    /// </summary>
    [DoNotAutoRegister]
    public interface IMethodInterceptor
    {
        System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation);
    }

    /// <summary>
    /// Represents a method invocation that is being intercepted.
    /// </summary>
    public interface IMethodInvocation
    {
        object Target { get; }
        System.Reflection.MethodInfo Method { get; }
        object?[] Arguments { get; }
        System.Type[] GenericArguments { get; }
        System.Threading.Tasks.ValueTask<object?> ProceedAsync();
    }

    /// <summary>
    /// Applies an interceptor to a class or method.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class InterceptAttribute : System.Attribute
    {
        public InterceptAttribute(System.Type interceptorType) => InterceptorType = interceptorType;
        public System.Type InterceptorType { get; }
        public int Order { get; set; } = 0;
    }

    /// <summary>
    /// Generic version of InterceptAttribute.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class InterceptAttribute<TInterceptor> : System.Attribute
        where TInterceptor : class, IMethodInterceptor
    {
        public System.Type InterceptorType => typeof(TInterceptor);
        public int Order { get; set; } = 0;
    }
}";

    /// <summary>
    /// Generator assembly attribute.
    /// </summary>
    public const string GenerateTypeRegistry = @"
namespace NexusLabs.Needlr.Generators
{
    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public sealed class GenerateTypeRegistryAttribute : System.Attribute
    {
        public string[]? IncludeNamespacePrefixes { get; set; }
        public bool IncludeSelf { get; set; } = true;
    }
}";

    /// <summary>
    /// Factory generation attributes.
    /// </summary>
    public const string Factory = @"
namespace NexusLabs.Needlr.Generators
{
    [System.Flags]
    public enum FactoryGenerationMode
    {
        Func = 1,
        Interface = 2,
        All = Func | Interface
    }

    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class GenerateFactoryAttribute : System.Attribute
    {
        public FactoryGenerationMode Mode { get; set; } = FactoryGenerationMode.All;
    }

    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class GenerateFactoryAttribute<TInterface> : System.Attribute
        where TInterface : class
    {
        public FactoryGenerationMode Mode { get; set; } = FactoryGenerationMode.All;
    }
}";

    /// <summary>
    /// RegisterAs attribute for explicit interface registration.
    /// </summary>
    public const string RegisterAs = @"
namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class RegisterAsAttribute<TInterface> : System.Attribute
        where TInterface : class
    {
        public System.Type InterfaceType => typeof(TInterface);
    }
}";

    /// <summary>
    /// All core attributes combined (Core + Keyed + GenerateTypeRegistry).
    /// </summary>
    public const string All = Core + Keyed + GenerateTypeRegistry;

    /// <summary>
    /// All attributes including interceptors.
    /// </summary>
    public const string AllWithInterceptors = Core + Keyed + Interceptors + GenerateTypeRegistry;

    /// <summary>
    /// All attributes including factory.
    /// </summary>
    public const string AllWithFactory = Core + Keyed + Factory + GenerateTypeRegistry;

    /// <summary>
    /// All attributes including RegisterAs.
    /// </summary>
    public const string AllWithRegisterAs = Core + Keyed + RegisterAs + GenerateTypeRegistry;

    /// <summary>
    /// Generated-constructor feature attributes: GenerateConstructor, ConstructorGuard,
    /// ConstructorIgnore, ConstructorGuardKind, and ConstructorNullGuardMode, matching
    /// the real package's public shapes.
    /// </summary>
    public const string GeneratedConstructor = @"
namespace NexusLabs.Needlr.Generators
{
    public enum ConstructorNullGuardMode
    {
        None = 0,
        NonNullableReferences = 1,
    }

    public enum ConstructorGuardKind
    {
        None = 0,
        NotNull = 1,
        NotNullOrEmpty = 2,
        NotNullOrWhiteSpace = 3,
    }

    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class GenerateConstructorAttribute : System.Attribute
    {
        public GenerateConstructorAttribute() : this(ConstructorNullGuardMode.None) { }
        public GenerateConstructorAttribute(ConstructorNullGuardMode mode) => Mode = mode;
        public ConstructorNullGuardMode Mode { get; }
    }

    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public sealed class ConstructorGuardAttribute : System.Attribute
    {
        public ConstructorGuardAttribute(ConstructorGuardKind kind) => Kind = kind;
        public ConstructorGuardAttribute(System.Type guardType) => GuardType = guardType;
        public ConstructorGuardAttribute(System.Type guardType, string methodName)
        {
            GuardType = guardType;
            MethodName = methodName;
        }

        public ConstructorGuardKind Kind { get; }
        public System.Type? GuardType { get; }
        public string? MethodName { get; }
    }

    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class ConstructorIgnoreAttribute : System.Attribute
    {
    }
}";

    /// <summary>
    /// All attributes including the generated-constructor feature.
    /// </summary>
    public const string AllWithGeneratedConstructor = Core + Keyed + GenerateTypeRegistry + GeneratedConstructor;
}
