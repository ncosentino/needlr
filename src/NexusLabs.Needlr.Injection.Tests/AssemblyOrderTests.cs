using NexusLabs.Needlr.Injection.AssemblyOrdering;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests;

public sealed class AssemblyOrderTests
{
    [Fact]
    public void Create_ReturnsNewBuilder()
    {
        // Act
        var builder = AssemblyOrder.Create();

        // Assert
        Assert.NotNull(builder);
    }

    [Fact]
    public void Create_WithConfigure_AppliesConfiguration()
    {
        // Arrange
        var configureExecuted = false;

        // Act
        var builder = AssemblyOrder.Create(b =>
        {
            b.By(a => a.Name.Contains("Test"));
            configureExecuted = true;
        });

        // Assert
        Assert.True(configureExecuted);
        Assert.NotNull(builder);
    }

    [Fact]
    public void Create_WithNullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        Action<AssemblyOrderBuilder> configure = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => AssemblyOrder.Create(configure));
    }

    [Fact]
    public void LibTestEntry_CreatesValidBuilder()
    {
        // Act
        var builder = AssemblyOrder.LibTestEntry();

        // Assert
        Assert.NotNull(builder);
    }

    [Fact]
    public void TestsLast_CreatesValidBuilder()
    {
        // Act
        var builder = AssemblyOrder.TestsLast();

        // Assert
        Assert.NotNull(builder);
    }

    [Fact]
    public void TestsLast_SortsTestAssembliesLast()
    {
        // Arrange
        var builder = AssemblyOrder.TestsLast();
        var names = new[] { "MyApp.Tests.dll", "MyApp.Core.dll", "MyApp.Services.dll" };

        // Act
        var sorted = builder.SortNames(names);

        // Assert
        Assert.Equal("MyApp.Core.dll", sorted[0]);
        Assert.Equal("MyApp.Services.dll", sorted[1]);
        Assert.Equal("MyApp.Tests.dll", sorted[2]);
    }

    [Fact]
    public void Alphabetical_CreatesValidBuilder()
    {
        // Act
        var builder = AssemblyOrder.Alphabetical();

        // Assert
        Assert.NotNull(builder);
    }

    [Fact]
    public void Alphabetical_SortsAlphabetically()
    {
        // Arrange
        var builder = AssemblyOrder.Alphabetical();
        var names = new[] { "Zebra.dll", "Alpha.dll", "Beta.dll" };

        // Act
        var sorted = builder.SortNames(names);

        // Assert
        Assert.Equal("Alpha.dll", sorted[0]);
        Assert.Equal("Beta.dll", sorted[1]);
        Assert.Equal("Zebra.dll", sorted[2]);
    }
}
