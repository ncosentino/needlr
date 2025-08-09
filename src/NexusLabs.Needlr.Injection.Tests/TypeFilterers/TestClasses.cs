using NexusLabs.Needlr;

namespace NexusLabs.Needlr.Injection.Tests.TypeFilterers;

public class SimpleClass { }

public abstract class AbstractClass { }

public interface ITestInterface { }

[DoNotInject]
public class DoNotInjectClass { }

public class ClassWithDependencies
{
    public ClassWithDependencies(SimpleClass dependency) { }
}

public class SelfReferencingClass
{
    public SelfReferencingClass(SelfReferencingClass self) { }
}

public class ClassWithValueTypeConstructor
{
    public ClassWithValueTypeConstructor(int value) { }
}

public class ClassWithStringConstructor
{
    public ClassWithStringConstructor(string value) { }
}

public class ClassWithDelegateConstructor
{
    public ClassWithDelegateConstructor(Action action) { }
}

public class ClassWithPrivateConstructor
{
    private ClassWithPrivateConstructor() { }
}

public class ClassWithMultipleConstructors
{
    public ClassWithMultipleConstructors() { }
    public ClassWithMultipleConstructors(SimpleClass dependency) { }
}

public class ShortClass { }