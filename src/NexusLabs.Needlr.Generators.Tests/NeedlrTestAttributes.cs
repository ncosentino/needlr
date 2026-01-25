namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Provides C# source code for Needlr attributes that can be embedded in generator tests.
/// This ensures generator tests use the same attribute definitions as the real package.
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
    /// Interceptor types: IMethodInterceptor, IMethodInvocation, InterceptAttribute, MethodInvocation.
    /// </summary>
    public const string Interceptors = @"
namespace NexusLabs.Needlr
{
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

    /// <summary>
    /// Default implementation of IMethodInvocation used by generated interceptor proxy classes.
    /// </summary>
    public sealed class MethodInvocation : IMethodInvocation
    {
        private readonly System.Func<System.Threading.Tasks.ValueTask<object?>> _proceed;
        
        public MethodInvocation(
            object target,
            System.Reflection.MethodInfo method,
            object?[] arguments,
            System.Func<System.Threading.Tasks.ValueTask<object?>> proceed)
        {
            Target = target;
            Method = method;
            Arguments = arguments;
            GenericArguments = System.Type.EmptyTypes;
            _proceed = proceed;
        }
        
        public object Target { get; }
        public System.Reflection.MethodInfo Method { get; }
        public object?[] Arguments { get; }
        public System.Type[] GenericArguments { get; }
        
        public System.Threading.Tasks.ValueTask<object?> ProceedAsync() => _proceed();
    }
}";

    /// <summary>
    /// DecoratorFor attribute for decorator pattern support.
    /// </summary>
    public const string Decorators = @"
namespace NexusLabs.Needlr
{
    /// <summary>
    /// Marks a class as a decorator for another type.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class DecoratorForAttribute : System.Attribute
    {
        public DecoratorForAttribute(System.Type decoratedType) => DecoratedType = decoratedType;
        public System.Type DecoratedType { get; }
        public int Order { get; set; } = 0;
    }

    /// <summary>
    /// Generic version of DecoratorForAttribute.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class DecoratorForAttribute<T> : System.Attribute where T : class
    {
        public System.Type DecoratedType => typeof(T);
        public int Order { get; set; } = 0;
    }
}";

    /// <summary>
    /// Core attributes plus interceptors (for interceptor tests).
    /// </summary>
    public const string CoreWithInterceptors = Core + Interceptors;

    /// <summary>
    /// All attributes (Core + Interceptors + Decorators).
    /// </summary>
    public const string All = Core + Interceptors + Decorators;
}
