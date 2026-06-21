
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |---------:|---------:|---------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_FuncFactory_PreResolved    | 13.40 ns | 7.730 ns | 2.008 ns |  1.02 |    0.19 |    1 | 0.0019 |      32 B |        1.00 |
 ManualDI_FuncFactory_WithResolution | 19.30 ns | 0.190 ns | 0.029 ns |  1.46 |    0.18 |    1 | 0.0019 |      32 B |        1.00 |
 DirectFactory_PreResolved           | 12.92 ns | 3.725 ns | 0.967 ns |  0.98 |    0.14 |    1 | 0.0019 |      32 B |        1.00 |
 DirectFactory_WithResolution        | 22.98 ns | 0.358 ns | 0.093 ns |  1.74 |    0.22 |    1 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryShorthand           | 15.59 ns | 1.961 ns | 0.509 ns |  1.18 |    0.15 |    1 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryInterface           | 17.73 ns | 3.100 ns | 0.805 ns |  1.34 |    0.18 |    1 | 0.0019 |      32 B |        1.00 |
 Provider_MixedShorthand             | 17.66 ns | 9.535 ns | 2.476 ns |  1.34 |    0.24 |    1 | 0.0019 |      32 B |        1.00 |
