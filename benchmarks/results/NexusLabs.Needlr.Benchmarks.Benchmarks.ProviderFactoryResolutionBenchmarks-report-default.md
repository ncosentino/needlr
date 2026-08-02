
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_FuncFactory_PreResolved    |  7.761 ns | 1.3856 ns | 0.3598 ns |  1.00 |    0.06 |    1 | 0.0025 |      32 B |        1.00 |
 ManualDI_FuncFactory_WithResolution | 17.679 ns | 6.0731 ns | 1.5772 ns |  2.28 |    0.21 |    2 | 0.0025 |      32 B |        1.00 |
 DirectFactory_PreResolved           |  7.818 ns | 0.9404 ns | 0.2442 ns |  1.01 |    0.05 |    1 | 0.0025 |      32 B |        1.00 |
 DirectFactory_WithResolution        | 18.120 ns | 2.6324 ns | 0.4074 ns |  2.34 |    0.11 |    2 | 0.0025 |      32 B |        1.00 |
 Provider_FactoryShorthand           |  8.314 ns | 4.6730 ns | 1.2136 ns |  1.07 |    0.15 |    1 | 0.0025 |      32 B |        1.00 |
 Provider_FactoryInterface           |  7.682 ns | 2.0690 ns | 0.3202 ns |  0.99 |    0.06 |    1 | 0.0025 |      32 B |        1.00 |
 Provider_MixedShorthand             |  8.533 ns | 1.0974 ns | 0.2850 ns |  1.10 |    0.06 |    1 | 0.0025 |      32 B |        1.00 |
