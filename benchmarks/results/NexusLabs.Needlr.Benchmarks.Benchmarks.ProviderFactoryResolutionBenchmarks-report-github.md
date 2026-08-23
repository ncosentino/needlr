```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_FuncFactory_PreResolved    | 10.848 ns | 4.0514 ns | 1.0521 ns |  1.01 |    0.13 |    2 | 0.0019 |      32 B |        1.00 |
| ManualDI_FuncFactory_WithResolution | 18.569 ns | 0.5444 ns | 0.1414 ns |  1.73 |    0.16 |    3 | 0.0019 |      32 B |        1.00 |
| DirectFactory_PreResolved           |  8.439 ns | 0.2098 ns | 0.0545 ns |  0.78 |    0.07 |    1 | 0.0019 |      32 B |        1.00 |
| DirectFactory_WithResolution        | 21.161 ns | 0.8265 ns | 0.2146 ns |  1.97 |    0.19 |    3 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryShorthand           | 15.311 ns | 3.5265 ns | 0.9158 ns |  1.42 |    0.15 |    3 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryInterface           | 17.337 ns | 5.3351 ns | 1.3855 ns |  1.61 |    0.19 |    3 | 0.0019 |      32 B |        1.00 |
| Provider_MixedShorthand             | 17.091 ns | 2.5571 ns | 0.6641 ns |  1.59 |    0.16 |    3 | 0.0019 |      32 B |        1.00 |
