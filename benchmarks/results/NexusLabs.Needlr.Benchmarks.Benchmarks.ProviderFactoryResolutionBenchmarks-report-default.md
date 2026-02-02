
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_FuncFactory_PreResolved    | 10.27 ns | 1.912 ns | 0.497 ns |  1.00 |    0.06 |    1 | 0.0019 |      32 B |        1.00 |
 ManualDI_FuncFactory_WithResolution | 18.78 ns | 0.436 ns | 0.113 ns |  1.83 |    0.08 |    2 | 0.0019 |      32 B |        1.00 |
 DirectFactory_PreResolved           | 11.03 ns | 5.503 ns | 1.429 ns |  1.08 |    0.14 |    1 | 0.0019 |      32 B |        1.00 |
 DirectFactory_WithResolution        | 19.78 ns | 0.249 ns | 0.065 ns |  1.93 |    0.09 |    2 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryShorthand           | 17.63 ns | 3.653 ns | 0.949 ns |  1.72 |    0.11 |    2 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryInterface           | 16.33 ns | 4.175 ns | 1.084 ns |  1.59 |    0.12 |    2 | 0.0019 |      32 B |        1.00 |
 Provider_MixedShorthand             | 17.67 ns | 4.875 ns | 1.266 ns |  1.72 |    0.14 |    2 | 0.0019 |      32 B |        1.00 |
