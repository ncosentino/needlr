using System;

namespace NexusLabs.Needlr.Injection.Tests;

public class ConcreteClass { }

public abstract class AbstractTestClass { }

public interface IFilterTestInterface { }

public class GenericClass<T> { }

public struct TestStruct { }

public enum TestEnum { Value1, Value2 }

public class TestException : Exception { }

public class TestAttribute : Attribute { }

public record TestRecord(string Name);

public class OuterClass
{
    public class NestedClass { }
}