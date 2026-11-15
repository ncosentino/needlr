
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_FuncFactory_PreResolved    | 13.806 ns | 5.6855 ns | 1.4765 ns |  1.01 |    0.14 |    2 | 0.0019 |      32 B |        1.00 |
 ManualDI_FuncFactory_WithResolution | 19.770 ns | 0.4670 ns | 0.1213 ns |  1.45 |    0.15 |    2 | 0.0019 |      32 B |        1.00 |
 DirectFactory_PreResolved           |  9.415 ns | 0.2295 ns | 0.0355 ns |  0.69 |    0.07 |    1 | 0.0019 |      32 B |        1.00 |
 DirectFactory_WithResolution        | 22.182 ns | 0.2993 ns | 0.0463 ns |  1.62 |    0.16 |    2 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryShorthand           | 18.101 ns | 3.7221 ns | 0.9666 ns |  1.32 |    0.15 |    2 | 0.0019 |      32 B |        1.00 |
 Provider_FactoryInterface           | 18.628 ns | 4.2450 ns | 1.1024 ns |  1.36 |    0.16 |    2 | 0.0019 |      32 B |        1.00 |
 Provider_MixedShorthand             | 17.088 ns | 1.9335 ns | 0.5021 ns |  1.25 |    0.13 |    2 | 0.0019 |      32 B |        1.00 |
