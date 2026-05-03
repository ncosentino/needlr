
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_FuncFactory_PreResolved    | 13.52 ns | 4.794 ns | 0.742 ns |  1.00 |    0.07 |    1 | 0.0019 |      32 B |        1.00 |
 ManualDI_FuncFactory_WithResolution | 18.87 ns | 0.092 ns | 0.014 ns |  1.40 |    0.07 |    1 | 0.0019 |      32 B |        1.00 |
 DirectFactory_PreResolved           | 11.04 ns | 1.914 ns | 0.497 ns |  0.82 |    0.05 |    1 | 0.0019 |      32 B |        1.00 |
 DirectFactory_WithResolution        | 22.67 ns | 0.448 ns | 0.069 ns |  1.68 |    0.08 |    1 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryShorthand           | 17.51 ns | 5.215 ns | 1.354 ns |  1.30 |    0.11 |    1 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryInterface           | 14.75 ns | 3.617 ns | 0.939 ns |  1.09 |    0.08 |    1 | 0.0019 |      32 B |        1.00 |
 Provider_MixedShorthand             | 13.80 ns | 3.248 ns | 0.844 ns |  1.02 |    0.08 |    1 | 0.0019 |      32 B |        1.00 |
