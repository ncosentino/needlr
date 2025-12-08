# Source Generation Feature Roadmap

## Overview

Needlr is introducing C# Source Generators to eliminate runtime reflection for type registration. This will significantly improve startup performance while maintaining full behavioral parity with the existing reflection-based registration system.

## Motivation

Currently, Needlr uses reflection at runtime to:
- Scan assemblies with `assembly.GetTypes()`
- Check for attributes like `[DoNotAutoRegister]` and `[DoNotInject]`
- Inspect constructors to determine if types are injectable
- Register types in the dependency injection container

Since all registerable types are known at compile time, we can use C# Source Generators to generate the registration code at compile time, eliminating the runtime overhead of reflection and assembly scanning.

## Benefits

### Performance Improvements
- **Faster Startup**: No runtime assembly scanning or type reflection
- **Reduced Memory**: No need to cache reflection metadata
- **AOT Compatible**: Source-generated code works with ahead-of-time compilation scenarios

### Development Experience
- **Compile-time Errors**: Issues with type registration discovered at build time
- **Better Tooling**: Generated code can be inspected and debugged
- **Explicit Dependencies**: All registered types visible in generated code

## Implementation Phases

### Phase 1: Infrastructure and Parity Tests ✅ (Current)

**Status**: Complete

**Components**:
- `NexusLabs.Needlr.SourceGenerators` project with skeleton generator
- `NexusLabs.Needlr.SourceGenerators.Tests` project with comprehensive test suite
- `SourceGeneratedTypeRegistrar` class for using generated code
- `UsingSourceGeneratedTypeRegistrar` extension method
- Baseline tests documenting current reflection behavior
- Test fixtures covering all registration scenarios

**Deliverables**:
- Foundation for source generator development
- Comprehensive test infrastructure
- Behavioral documentation via tests

### Phase 2: Source Generator Implementation (Planned)

**Objectives**:
- Implement full source generation logic
- Generate registration code for all discovered types
- Apply same filtering logic as reflection-based registrars
- Handle all edge cases (nested classes, generics, attributes, etc.)

**Components**:
- Syntax receivers for discovering registerable types
- Code generation logic for service registrations
- Proper handling of lifetimes (Singleton, Scoped, Transient)
- Interface registration generation

**Acceptance Criteria**:
- All parity tests pass
- Generated code matches reflection-based behavior
- Performance benchmarks show improvement

### Phase 3: Advanced Features (Future)

**Potential Features**:
- Compile-time validation of dependency graphs
- Detection of circular dependencies
- Optimization of registration order
- Custom lifetime attributes
- Registration profiles/groups

### Phase 4: Migration and Documentation (Future)

**Objectives**:
- Update examples to use source generation
- Migration guide for existing users
- Performance comparison documentation
- Best practices guide

## Usage

### Basic Usage (Phase 2+)

Once implemented, usage will look like:

```csharp
// Generated code will be automatically created by the source generator
var serviceProvider = new Syringe()
    .UsingSourceGeneratedTypeRegistrar(GeneratedRegistrations.Register)
    .UsingDefaultTypeFilterer()
    .UsingDefaultAssemblyProvider()
    .BuildServiceProvider();
```

### Current Phase 1 Usage

In Phase 1, the infrastructure is in place but no code is generated yet:

```csharp
// Manual registration for testing infrastructure
var serviceProvider = new Syringe()
    .UsingSourceGeneratedTypeRegistrar(services =>
    {
        // Manually add registrations for testing
        services.AddSingleton<MyService>();
    })
    .UsingDefaultTypeFilterer()
    .UsingDefaultAssemblyProvider()
    .BuildServiceProvider();
```

## Test Coverage

The test suite covers:
- ✅ Simple classes with parameterless constructors
- ✅ Classes with injectable dependencies
- ✅ `[DoNotAutoRegister]` attribute behavior
- ✅ `[DoNotInject]` attribute behavior
- ✅ Abstract classes (not registered)
- ✅ Interfaces (not registered)
- ✅ Generic type definitions (not registered)
- ✅ Nested classes (not registered)
- ✅ Record types (not registered)
- ✅ Non-injectable constructor parameters
- ✅ Multiple interface implementations
- ✅ Internal classes
- ✅ Private constructors
- ✅ Singleton lifetime scenarios
- ✅ DefaultTypeRegistrar vs ScrutorTypeRegistrar differences

## Design Decisions

### Why Two Projects?

- **SourceGenerators**: Must target `netstandard2.0` for Roslyn compatibility
- **SourceGenerators.Tests**: Targets `net9.0` for full testing capabilities

### Why Phase 1 First?

Establishing the test infrastructure first ensures:
1. Clear documentation of expected behavior
2. Regression protection during development
3. Confidence that generated code matches reflection behavior
4. Easier debugging and validation

### Compatibility

The source generator approach is designed to:
- Maintain 100% behavioral parity with reflection-based registration
- Work alongside existing registration methods
- Allow gradual migration
- Support all existing features and attributes

## Contributing

When implementing Phase 2 and beyond:

1. **Run Baseline Tests**: Ensure all baseline tests pass
2. **Verify Parity**: Compare generated code against reflection results
3. **Performance Test**: Benchmark against reflection-based approach
4. **Update Documentation**: Keep this file current with progress

## Related Files

- `/src/NexusLabs.Needlr.SourceGenerators/` - Generator implementation
- `/src/NexusLabs.Needlr.SourceGenerators.Tests/` - Test suite
- `/src/NexusLabs.Needlr.Injection/TypeRegistrars/SourceGeneratedTypeRegistrar.cs` - Runtime component
- `/src/NexusLabs.Needlr.Injection/SyringeExtensions.cs` - Extension methods

## References

- [C# Source Generators Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)
- [Incremental Generators](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md)
- [Scrutor Library](https://github.com/khellang/Scrutor) - Current alternative approach
