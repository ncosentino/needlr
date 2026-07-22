namespace GeneratedConstructorExample;

/// <summary>
/// The default <see cref="IUserRepository"/> implementation. Automatically
/// registered by Needlr's source generator because it is a non-abstract class
/// with no constructor parameters.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    /// <inheritdoc />
    public string Describe() => "UserRepository(in-memory)";
}
