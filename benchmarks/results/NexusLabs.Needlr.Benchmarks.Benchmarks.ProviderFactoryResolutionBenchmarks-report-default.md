
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_FuncFactory_PreResolved    | 13.995 ns | 2.8594 ns | 0.7426 ns |  1.00 |    0.07 |    2 | 0.0019 |      32 B |        1.00 |
 ManualDI_FuncFactory_WithResolution | 19.453 ns | 0.1348 ns | 0.0209 ns |  1.39 |    0.07 |    2 | 0.0019 |      32 B |        1.00 |
 DirectFactory_PreResolved           |  9.376 ns | 0.3402 ns | 0.0526 ns |  0.67 |    0.03 |    1 | 0.0019 |      32 B |        1.00 |
 DirectFactory_WithResolution        | 22.815 ns | 0.1861 ns | 0.0483 ns |  1.63 |    0.08 |    2 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryShorthand           | 15.843 ns | 7.1098 ns | 1.8464 ns |  1.13 |    0.13 |    2 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryInterface           | 14.001 ns | 5.4529 ns | 1.4161 ns |  1.00 |    0.10 |    2 | 0.0019 |      32 B |        1.00 |
 Provider_MixedShorthand             | 19.544 ns | 3.8306 ns | 0.9948 ns |  1.40 |    0.09 |    2 | 0.0019 |      32 B |        1.00 |
