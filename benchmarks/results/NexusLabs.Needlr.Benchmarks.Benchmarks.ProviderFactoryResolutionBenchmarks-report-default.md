
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_FuncFactory_PreResolved    |  7.861 ns | 0.8049 ns | 0.1246 ns |  1.00 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
 ManualDI_FuncFactory_WithResolution | 20.371 ns | 0.9680 ns | 0.2514 ns |  2.59 |    0.05 |    3 | 0.0019 |      32 B |        1.00 |
 DirectFactory_PreResolved           | 13.200 ns | 4.9344 ns | 1.2814 ns |  1.68 |    0.15 |    2 | 0.0019 |      32 B |        1.00 |
 DirectFactory_WithResolution        | 22.058 ns | 2.1652 ns | 0.5623 ns |  2.81 |    0.08 |    3 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryShorthand           | 17.266 ns | 5.1272 ns | 1.3315 ns |  2.20 |    0.16 |    3 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryInterface           | 17.677 ns | 1.9911 ns | 0.5171 ns |  2.25 |    0.07 |    3 | 0.0019 |      32 B |        1.00 |
 Provider_MixedShorthand             | 19.396 ns | 5.9118 ns | 1.5353 ns |  2.47 |    0.18 |    3 | 0.0019 |      32 B |        1.00 |
