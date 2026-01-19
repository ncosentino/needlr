// Disable unused parameter warnings - these services exist for DI benchmarking
#pragma warning disable CS9113

using NexusLabs.Needlr;

namespace NexusLabs.Needlr.Benchmarks.TestTypes;

// ============================================================================
// Simple services (no dependencies) - 10 types
// ============================================================================

public interface ISimpleService1 { string GetValue(); }
public interface ISimpleService2 { string GetValue(); }
public interface ISimpleService3 { string GetValue(); }
public interface ISimpleService4 { string GetValue(); }
public interface ISimpleService5 { string GetValue(); }
public interface ISimpleService6 { string GetValue(); }
public interface ISimpleService7 { string GetValue(); }
public interface ISimpleService8 { string GetValue(); }
public interface ISimpleService9 { string GetValue(); }
public interface ISimpleService10 { string GetValue(); }

public sealed class SimpleService1 : ISimpleService1 { public string GetValue() => "1"; }
public sealed class SimpleService2 : ISimpleService2 { public string GetValue() => "2"; }
public sealed class SimpleService3 : ISimpleService3 { public string GetValue() => "3"; }
public sealed class SimpleService4 : ISimpleService4 { public string GetValue() => "4"; }
public sealed class SimpleService5 : ISimpleService5 { public string GetValue() => "5"; }
public sealed class SimpleService6 : ISimpleService6 { public string GetValue() => "6"; }
public sealed class SimpleService7 : ISimpleService7 { public string GetValue() => "7"; }
public sealed class SimpleService8 : ISimpleService8 { public string GetValue() => "8"; }
public sealed class SimpleService9 : ISimpleService9 { public string GetValue() => "9"; }
public sealed class SimpleService10 : ISimpleService10 { public string GetValue() => "10"; }

// ============================================================================
// Services with dependencies (1-3 deps) - 20 types
// ============================================================================

public interface IDependentService1 { }
public interface IDependentService2 { }
public interface IDependentService3 { }
public interface IDependentService4 { }
public interface IDependentService5 { }
public interface IDependentService6 { }
public interface IDependentService7 { }
public interface IDependentService8 { }
public interface IDependentService9 { }
public interface IDependentService10 { }
public interface IDependentService11 { }
public interface IDependentService12 { }
public interface IDependentService13 { }
public interface IDependentService14 { }
public interface IDependentService15 { }
public interface IDependentService16 { }
public interface IDependentService17 { }
public interface IDependentService18 { }
public interface IDependentService19 { }
public interface IDependentService20 { }

// 1 dependency
public sealed class DependentService1(ISimpleService1 dep) : IDependentService1 { }
public sealed class DependentService2(ISimpleService2 dep) : IDependentService2 { }
public sealed class DependentService3(ISimpleService3 dep) : IDependentService3 { }
public sealed class DependentService4(ISimpleService4 dep) : IDependentService4 { }
public sealed class DependentService5(ISimpleService5 dep) : IDependentService5 { }

// 2 dependencies
public sealed class DependentService6(ISimpleService1 dep1, ISimpleService2 dep2) : IDependentService6 { }
public sealed class DependentService7(ISimpleService2 dep1, ISimpleService3 dep2) : IDependentService7 { }
public sealed class DependentService8(ISimpleService3 dep1, ISimpleService4 dep2) : IDependentService8 { }
public sealed class DependentService9(ISimpleService4 dep1, ISimpleService5 dep2) : IDependentService9 { }
public sealed class DependentService10(ISimpleService5 dep1, ISimpleService6 dep2) : IDependentService10 { }

// 3 dependencies
public sealed class DependentService11(ISimpleService1 dep1, ISimpleService2 dep2, ISimpleService3 dep3) : IDependentService11 { }
public sealed class DependentService12(ISimpleService2 dep1, ISimpleService3 dep2, ISimpleService4 dep3) : IDependentService12 { }
public sealed class DependentService13(ISimpleService3 dep1, ISimpleService4 dep2, ISimpleService5 dep3) : IDependentService13 { }
public sealed class DependentService14(ISimpleService4 dep1, ISimpleService5 dep2, ISimpleService6 dep3) : IDependentService14 { }
public sealed class DependentService15(ISimpleService5 dep1, ISimpleService6 dep2, ISimpleService7 dep3) : IDependentService15 { }

// Mixed dependencies (using dependent services)
public sealed class DependentService16(IDependentService1 dep) : IDependentService16 { }
public sealed class DependentService17(IDependentService2 dep1, IDependentService3 dep2) : IDependentService17 { }
public sealed class DependentService18(IDependentService4 dep1, IDependentService5 dep2, ISimpleService1 dep3) : IDependentService18 { }
public sealed class DependentService19(IDependentService6 dep1, ISimpleService7 dep2) : IDependentService19 { }
public sealed class DependentService20(IDependentService7 dep1, IDependentService8 dep2, IDependentService9 dep3) : IDependentService20 { }

// ============================================================================
// Services with multiple interfaces - 10 types
// ============================================================================

public interface IMultiInterface1A { }
public interface IMultiInterface1B { }
public interface IMultiInterface2A { }
public interface IMultiInterface2B { }
public interface IMultiInterface3A { }
public interface IMultiInterface3B { }
public interface IMultiInterface4A { }
public interface IMultiInterface4B { }
public interface IMultiInterface5A { }
public interface IMultiInterface5B { }

public sealed class MultiInterfaceService1 : IMultiInterface1A, IMultiInterface1B { }
public sealed class MultiInterfaceService2 : IMultiInterface2A, IMultiInterface2B { }
public sealed class MultiInterfaceService3 : IMultiInterface3A, IMultiInterface3B { }
public sealed class MultiInterfaceService4 : IMultiInterface4A, IMultiInterface4B { }
public sealed class MultiInterfaceService5 : IMultiInterface5A, IMultiInterface5B { }

public sealed class MultiInterfaceService6(ISimpleService1 dep) : IMultiInterface1A, IMultiInterface2A { }
public sealed class MultiInterfaceService7(ISimpleService2 dep) : IMultiInterface2B, IMultiInterface3A { }
public sealed class MultiInterfaceService8(ISimpleService3 dep) : IMultiInterface3B, IMultiInterface4A { }
public sealed class MultiInterfaceService9(ISimpleService4 dep) : IMultiInterface4B, IMultiInterface5A { }
public sealed class MultiInterfaceService10(ISimpleService5 dep) : IMultiInterface5B, IMultiInterface1A { }

// ============================================================================
// Services marked as DoNotAutoRegister - 5 types (for filtering benchmarks)
// ============================================================================

public interface IManualService1 { }
public interface IManualService2 { }
public interface IManualService3 { }
public interface IManualService4 { }
public interface IManualService5 { }

[DoNotAutoRegister]
public sealed class ManualService1 : IManualService1 { }

[DoNotAutoRegister]
public sealed class ManualService2 : IManualService2 { }

[DoNotAutoRegister]
public sealed class ManualService3 : IManualService3 { }

[DoNotAutoRegister]
public sealed class ManualService4 : IManualService4 { }

[DoNotAutoRegister]
public sealed class ManualService5 : IManualService5 { }

// ============================================================================
// Standalone services (no interface) - 5 types
// ============================================================================

public sealed class StandaloneService1 { public string Name => "Standalone1"; }
public sealed class StandaloneService2 { public string Name => "Standalone2"; }
public sealed class StandaloneService3 { public string Name => "Standalone3"; }
public sealed class StandaloneService4 { public string Name => "Standalone4"; }
public sealed class StandaloneService5 { public string Name => "Standalone5"; }
