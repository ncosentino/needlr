
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_FuncFactory_PreResolved    | 11.802 ns | 4.2460 ns | 1.1027 ns |  1.01 |    0.12 |    2 | 0.0019 |      32 B |        1.00 |
 ManualDI_FuncFactory_WithResolution | 18.257 ns | 0.5150 ns | 0.1337 ns |  1.56 |    0.13 |    3 | 0.0019 |      32 B |        1.00 |
 DirectFactory_PreResolved           |  8.911 ns | 1.6271 ns | 0.4226 ns |  0.76 |    0.07 |    1 | 0.0019 |      32 B |        1.00 |
 DirectFactory_WithResolution        | 21.700 ns | 0.5044 ns | 0.0781 ns |  1.85 |    0.15 |    3 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryShorthand           | 15.824 ns | 2.5800 ns | 0.6700 ns |  1.35 |    0.12 |    3 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryInterface           | 17.589 ns | 4.0707 ns | 0.6299 ns |  1.50 |    0.13 |    3 | 0.0019 |      32 B |        1.00 |
 Provider_MixedShorthand             | 17.106 ns | 5.1150 ns | 1.3284 ns |  1.46 |    0.16 |    3 | 0.0019 |      32 B |        1.00 |
