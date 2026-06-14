```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean      | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |----------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_FuncFactory_PreResolved    | 13.798 ns | 2.2993 ns | 0.5971 ns |  1.00 |    0.06 |    2 | 0.0019 |      32 B |        1.00 |
| ManualDI_FuncFactory_WithResolution | 19.926 ns | 1.4290 ns | 0.3711 ns |  1.45 |    0.06 |    2 | 0.0019 |      32 B |        1.00 |
| DirectFactory_PreResolved           | 13.315 ns | 3.9853 ns | 1.0350 ns |  0.97 |    0.08 |    2 | 0.0019 |      32 B |        1.00 |
| DirectFactory_WithResolution        | 22.604 ns | 0.2959 ns | 0.0458 ns |  1.64 |    0.06 |    2 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryShorthand           |  7.843 ns | 0.5103 ns | 0.1325 ns |  0.57 |    0.02 |    1 | 0.0019 |      32 B |        1.00 |
| Provider_FactoryInterface           | 17.863 ns | 2.6524 ns | 0.4105 ns |  1.30 |    0.06 |    2 | 0.0019 |      32 B |        1.00 |
| Provider_MixedShorthand             | 15.965 ns | 2.6517 ns | 0.6886 ns |  1.16 |    0.06 |    2 | 0.0019 |      32 B |        1.00 |
