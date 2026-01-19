using Xunit;

namespace NexusLabs.Needlr.Injection.Scrutor.Tests;

public sealed class SyringeScrutorExtensionsTests
{
    [Fact]
    public void UsingScrutorTypeRegistrar_ReturnsSyringeWithScrutorRegistrar()
    {
        // Arrange
        var syringe = new Syringe();

        // Act
        var result = syringe.UsingScrutorTypeRegistrar();

        // Assert
        Assert.NotNull(result);
        Assert.NotSame(syringe, result); // Should return new instance
    }

    [Fact]
    public void UsingScrutorTypeRegistrar_ThrowsOnNullSyringe()
    {
        // Arrange
        Syringe? syringe = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => syringe!.UsingScrutorTypeRegistrar());
    }

    [Fact]
    public void UsingScrutorTypeRegistrar_CanBeChainedWithOtherMethods()
    {
        // Arrange & Act
        var syringe = new Syringe()
            .UsingScrutorTypeRegistrar()
            .UsingAdditionalAssemblies([typeof(SyringeScrutorExtensionsTests).Assembly]);

        // Assert
        Assert.NotNull(syringe);
    }

    [Fact]
    public void UsingScrutorTypeRegistrar_MultipleCallsSucceed()
    {
        // Arrange
        var syringe = new Syringe();

        // Act - calling multiple times should just replace the registrar
        var result = syringe
            .UsingScrutorTypeRegistrar()
            .UsingScrutorTypeRegistrar();

        // Assert
        Assert.NotNull(result);
    }
}
