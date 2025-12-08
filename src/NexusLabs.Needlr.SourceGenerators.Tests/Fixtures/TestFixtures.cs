namespace NexusLabs.Needlr.SourceGenerators.Tests.Fixtures;

// ===== Simple Classes =====

/// <summary>
/// Simple concrete class with parameterless constructor.
/// SHOULD be registered as singleton.
/// </summary>
public class SimpleClass
{
}

/// <summary>
/// Simple class with a public parameterless constructor.
/// SHOULD be registered as singleton.
/// </summary>
public class ClassWithPublicParameterlessConstructor
{
    public ClassWithPublicParameterlessConstructor()
    {
    }
}

// ===== Injectable Dependencies =====

public interface ISimpleService
{
}

public interface IOtherService
{
}

/// <summary>
/// Class with injectable class parameter.
/// SHOULD be registered as singleton.
/// </summary>
public class ClassWithInjectableClassParameter
{
    public ClassWithInjectableClassParameter(SimpleClass dependency)
    {
    }
}

/// <summary>
/// Class with injectable interface parameter.
/// SHOULD be registered as singleton.
/// </summary>
public class ClassWithInjectableInterfaceParameter
{
    public ClassWithInjectableInterfaceParameter(ISimpleService service)
    {
    }
}

/// <summary>
/// Class implementing interface with injectable dependencies.
/// SHOULD be registered as singleton (as self and as ISimpleService).
/// </summary>
public class ServiceImplementation : ISimpleService
{
    public ServiceImplementation(SimpleClass dependency)
    {
    }
}

/// <summary>
/// Class implementing multiple interfaces.
/// SHOULD be registered as singleton (as self and as all interfaces).
/// </summary>
public class MultiInterfaceImplementation : ISimpleService, IOtherService
{
}

// ===== DoNotAutoRegister Attribute =====

/// <summary>
/// Class marked with DoNotAutoRegister.
/// SHOULD NOT be registered.
/// </summary>
[DoNotAutoRegister]
public class ClassWithDoNotAutoRegister
{
}

/// <summary>
/// Interface marked with DoNotAutoRegister.
/// SHOULD NOT be registered (and implementers should inherit this).
/// </summary>
[DoNotAutoRegister]
public interface IDoNotAutoRegisterService
{
}

/// <summary>
/// Class implementing interface marked with DoNotAutoRegister.
/// SHOULD NOT be registered (inherits attribute from interface).
/// </summary>
public class ImplementsDoNotAutoRegisterInterface : IDoNotAutoRegisterService
{
}

// ===== DoNotInject Attribute =====

/// <summary>
/// Class marked with DoNotInject but has parameterless constructor.
/// SHOULD be registered as singleton (DoNotInject only affects constructors).
/// </summary>
[DoNotInject]
public class ClassWithDoNotInject
{
    public ClassWithDoNotInject()
    {
    }
}

/// <summary>
/// Class marked with DoNotInject and has only constructor with parameters.
/// SHOULD NOT be registered (no valid constructor).
/// </summary>
[DoNotInject]
public class ClassWithDoNotInjectNoParameterlessConstructor
{
    public ClassWithDoNotInjectNoParameterlessConstructor(string value)
    {
    }
}

// ===== Types That Should Not Be Registered =====

/// <summary>
/// Abstract class.
/// SHOULD NOT be registered.
/// </summary>
public abstract class AbstractClass
{
}

/// <summary>
/// Interface.
/// SHOULD NOT be registered.
/// </summary>
public interface ITestInterface
{
}

/// <summary>
/// Generic type definition.
/// SHOULD NOT be registered.
/// </summary>
public class GenericClassDefinition<T>
{
}

/// <summary>
/// Nested class.
/// SHOULD NOT be registered.
/// </summary>
public class OuterClassForNesting
{
    public class NestedClass
    {
    }
}

/// <summary>
/// Record type.
/// SHOULD NOT be registered.
/// </summary>
public record TestRecord(string Name);

// ===== Non-Injectable Constructor Parameters =====

/// <summary>
/// Class with value type parameter.
/// SHOULD NOT be registered (no valid injectable constructor).
/// </summary>
public class ClassWithValueTypeParameter
{
    public ClassWithValueTypeParameter(int value)
    {
    }
}

/// <summary>
/// Class with string parameter.
/// SHOULD NOT be registered (no valid injectable constructor).
/// </summary>
public class ClassWithStringParameter
{
    public ClassWithStringParameter(string value)
    {
    }
}

/// <summary>
/// Class with delegate parameter.
/// SHOULD NOT be registered (no valid injectable constructor).
/// </summary>
public class ClassWithDelegateParameter
{
    public ClassWithDelegateParameter(Action callback)
    {
    }
}

/// <summary>
/// Class with both injectable and non-injectable parameters.
/// SHOULD NOT be registered (has non-injectable parameter).
/// </summary>
public class ClassWithMixedParameters
{
    public ClassWithMixedParameters(SimpleClass service, int value)
    {
    }
}

/// <summary>
/// Class with parameterless constructor AND constructor with value type parameter.
/// SHOULD be registered as singleton (has valid parameterless constructor).
/// </summary>
public class ClassWithMultipleConstructors
{
    public ClassWithMultipleConstructors()
    {
    }

    public ClassWithMultipleConstructors(int value)
    {
    }
}

// ===== Internal Classes =====

/// <summary>
/// Internal class with parameterless constructor.
/// SHOULD be registered as singleton.
/// </summary>
internal class InternalClass
{
}

// ===== Private Constructors =====

/// <summary>
/// Class with only private constructor.
/// SHOULD NOT be registered (no accessible constructor).
/// </summary>
public class ClassWithPrivateConstructor
{
    private ClassWithPrivateConstructor()
    {
    }
}

/// <summary>
/// Class with private parameterless constructor and public constructor with parameters.
/// SHOULD be registered as singleton (has public injectable constructor).
/// </summary>
public class ClassWithPrivateParameterlessAndPublicInjectable
{
    private ClassWithPrivateParameterlessAndPublicInjectable()
    {
    }

    public ClassWithPrivateParameterlessAndPublicInjectable(SimpleClass dependency)
    {
    }
}

// ===== Lifetime Scenarios =====
// Note: DefaultTypeFilterer only returns true for IsInjectableSingletonType
// Scoped and Transient would need custom ITypeFilterer implementations

/// <summary>
/// Class that should be registered as singleton (default behavior).
/// </summary>
public class SingletonClass
{
}

// ===== Edge Cases =====

/// <summary>
/// Class with constructor parameter that is the same type (circular).
/// SHOULD NOT be registered (circular dependency).
/// </summary>
public class ClassWithCircularConstructorParameter
{
    public ClassWithCircularConstructorParameter(ClassWithCircularConstructorParameter self)
    {
    }
}

/// <summary>
/// Class with all injectable parameters.
/// SHOULD be registered as singleton.
/// </summary>
public class ClassWithAllInjectableParameters
{
    public ClassWithAllInjectableParameters(
        ISimpleService service,
        SimpleClass concreteClass)
    {
    }
}

/// <summary>
/// Class with complex interface hierarchy.
/// SHOULD be registered as singleton (as self and all interfaces).
/// </summary>
public interface IBaseInterface
{
}

public interface IDerivedInterface : IBaseInterface
{
}

public class ClassImplementingDerivedInterface : IDerivedInterface
{
    public ClassImplementingDerivedInterface()
    {
    }
}
